using System.IO;
using NAudio.Wave;

namespace AionixScribe;

public sealed class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private WaveFileWriter? _writer;

    public bool IsRecording { get; private set; }

    public void Start()
    {
        if (IsRecording) return;

        _buffer = new MemoryStream();
        _waveIn = new WaveInEvent
        {
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
