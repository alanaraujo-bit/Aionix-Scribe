using System.IO;
using NAudio.Wave;

namespace AionixScribe;

public sealed class NoMicrophoneException : Exception
{
    public NoMicrophoneException() : base(
        "Nenhum microfone foi detectado pelo Windows. Verifique se ele está conectado, ligado e não está em modo de suspensão (comum em headsets sem fio).") { }
}

public sealed class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private WaveFileWriter? _writer;

    public bool IsRecording { get; private set; }

    /// Índice do dispositivo de entrada a usar (AudioSettings.SystemDefaultDeviceIndex/-1 = padrão
    /// do sistema, mapeado para 0). Settável a qualquer momento antes de Start().
    public int DeviceIndex { get; set; } = AudioSettings.SystemDefaultDeviceIndex;

    public void Start()
    {
        if (IsRecording) return;

        // Checagem explícita em vez de deixar o WaveInEvent falhar com uma exceção genérica —
        // "sem microfone" é um caso real e comum o suficiente (headsets sem fio dormem/desconectam)
        // para merecer uma mensagem específica em vez de um erro técnico cru (§29, §62).
        if (WaveInEvent.DeviceCount == 0)
        {
            throw new NoMicrophoneException();
        }

        // Dispositivo salvo pode ter sido removido/desconectado desde a última vez — cai para o
        // padrão do sistema em vez de deixar o NAudio lançar uma exceção genérica.
        var deviceNumber = DeviceIndex;
        if (deviceNumber < 0 || deviceNumber >= WaveInEvent.DeviceCount)
        {
            deviceNumber = 0;
        }

        _buffer = new MemoryStream();
        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz mono PCM — suficiente para voz, leve para upload
        };
        _writer = new WaveFileWriter(_buffer, _waveIn.WaveFormat);
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        IsRecording = true;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    /// Para a captura e retorna o WAV completo. Retorna null se não havia gravação em andamento.
    public byte[]? Stop()
    {
        if (!IsRecording || _waveIn == null || _writer == null || _buffer == null) return null;

        _waveIn.StopRecording();
        _waveIn.DataAvailable -= OnDataAvailable;
        _writer.Flush();

        var bytes = _buffer.ToArray();

        _writer.Dispose();
        _waveIn.Dispose();
        _buffer.Dispose();
        _writer = null;
        _waveIn = null;
        _buffer = null;
        IsRecording = false;

        return bytes;
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _waveIn?.Dispose();
        _buffer?.Dispose();
    }

    /// Gravação curta demais para conter fala (toque acidental no atalho, push-to-talk mal encostado).
    private const double MinimumSpeechSeconds = 0.6;

    /// Limiar de energia (RMS de amostras PCM 16 bits, escala 0-32767) abaixo do qual o áudio é só
    /// ruído de fundo. Deliberadamente conservador: fala normal fica na casa dos milhares, ruído de
    /// sala fica abaixo de ~50. Preferimos deixar passar um áudio duvidoso a engolir fala real baixa.
    private const double SilenceRmsThreshold = 90.0;

    /// Janela de análise de energia. Medir o RMS do buffer inteiro descartaria uma gravação longa em
    /// que a pessoa só falou no fim (o silêncio dilui a média) — o critério certo é "existe alguma
    /// janela curta com energia de fala", não "a média toda tem energia de fala".
    private const double RmsWindowSeconds = 0.1;

    /// Decide se o WAV vale uma chamada à IA. Um áudio vazio/curto/silencioso custaria tokens de
    /// entrada e, pela regra do D006, consumiria cota do usuário mesmo devolvendo "nenhuma fala
    /// detectada" — cortar aqui é o único ponto do fluxo onde essa chamada realmente pode ser evitada.
    /// Espera o formato produzido por Start(): PCM 16 bits, mono, 16 kHz. O tamanho do cabeçalho NÃO
    /// é assumido: o WaveFileWriter do NAudio grava um chunk `fmt ` de 18 bytes (com cbSize) em vez
    /// dos 16 bytes do layout canônico, então o clássico "pule 44 bytes" desalinharia cada amostra —
    /// e amostras desalinhadas leem silêncio como energia alta, o que faria o portão aprovar tudo em
    /// silêncio. Localizamos o chunk `data` de verdade.
    public static bool HasLikelySpeech(byte[] wav, out string reason)
    {
        const int bytesPerSecond = 16000 * 2; // 16 kHz * 16 bits mono

        if (!TryFindDataChunk(wav, out var dataStart, out var dataBytes) || dataBytes == 0)
        {
            reason = "áudio vazio";
            return false;
        }

        var seconds = (double)dataBytes / bytesPerSecond;
        if (seconds < MinimumSpeechSeconds)
        {
            reason = $"gravação curta demais ({seconds:F2}s)";
            return false;
        }

        var windowSamples = (int)(16000 * RmsWindowSeconds);
        var peakRms = 0.0;
        double sumSquares = 0;
        var samplesInWindow = 0;

        for (var i = dataStart; i + 1 < dataStart + dataBytes; i += 2)
        {
            double sample = (short)(wav[i] | (wav[i + 1] << 8));
            sumSquares += sample * sample;
            samplesInWindow++;

            if (samplesInWindow == windowSamples)
            {
                peakRms = Math.Max(peakRms, Math.Sqrt(sumSquares / samplesInWindow));
                sumSquares = 0;
                samplesInWindow = 0;
            }
        }

        // Sobra final menor que uma janela cheia: só conta se tiver massa suficiente para o RMS ser
        // representativo, senão um estalo isolado no fim aprovaria a gravação inteira.
        if (samplesInWindow > windowSamples / 2)
        {
            peakRms = Math.Max(peakRms, Math.Sqrt(sumSquares / samplesInWindow));
        }

        if (peakRms < SilenceRmsThreshold)
        {
            reason = $"apenas silêncio/ruído (pico RMS {peakRms:F1})";
            return false;
        }

        reason = "";
        return true;
    }

    /// Percorre os chunks RIFF até achar o `data`. Retorna false se o buffer não for um WAV válido.
    private static bool TryFindDataChunk(byte[] wav, out int dataStart, out int dataBytes)
    {
        dataStart = 0;
        dataBytes = 0;

        // "RIFF" + tamanho + "WAVE" = 12 bytes antes do primeiro chunk.
        if (wav.Length < 12 || wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F') return false;

        var pos = 12;
        while (pos + 8 <= wav.Length)
        {
            var size = wav[pos + 4] | (wav[pos + 5] << 8) | (wav[pos + 6] << 16) | (wav[pos + 7] << 24);
            if (size < 0) return false;

            if (wav[pos] == 'd' && wav[pos + 1] == 'a' && wav[pos + 2] == 't' && wav[pos + 3] == 'a')
            {
                dataStart = pos + 8;
                // O tamanho declarado pode passar do buffer real se a gravação foi interrompida —
                // usar o menor dos dois evita ler lixo além do fim.
                dataBytes = Math.Min(size, wav.Length - dataStart);
                return dataBytes > 0;
            }

            pos += 8 + size + (size % 2); // chunks são alinhados em 2 bytes
        }

        return false;
    }
}
