using NAudio.Wave;

namespace Academy.Agent.Audio;

public sealed class AudioDataAvailableEventArgs : EventArgs
{
    public byte[] Buffer { get; init; } = [];
    public int BytesRecorded { get; init; }
    public WaveFormat WaveFormat { get; init; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
}