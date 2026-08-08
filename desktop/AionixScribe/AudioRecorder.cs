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
}
