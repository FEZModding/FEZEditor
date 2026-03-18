using System.Runtime.InteropServices;
using NVorbis;

namespace FezEditor.Structure;

public class VorbisSoundContainer : IDisposable
{
    private readonly VorbisReader _vorbisReader;
    private byte[]? _sampleBuffer = null;

    public bool Loaded => _sampleBuffer != null;


    public VorbisSoundContainer(Stream oggStream, bool leaveOpen)
    {
        _vorbisReader = new VorbisReader(oggStream, leaveOpen);
    }

    public void Load()
    {
        _vorbisReader.Initialize();

        if (_vorbisReader.TotalSamples == 0)
        {
            return;
        }

        var samplesCount = _vorbisReader.TotalSamples * _vorbisReader.Channels;
        var floatSampleBuffer = new float[samplesCount];
        var samplesRead = _vorbisReader.ReadSamples(floatSampleBuffer);

        _sampleBuffer = new byte[samplesCount * 2]; // 2 bytes per sample for 16-bit audio
        var shortSampleBuffer = MemoryMarshal.Cast<byte, short>(_sampleBuffer.AsSpan());

        for (var i = 0; i < samplesCount; i++)
        {
            shortSampleBuffer[i] = (short)(Math.Clamp(floatSampleBuffer[i], -1f, 1f) * short.MaxValue);
        }
    }

    public RSoundEffect CreateSoundEffectAsset()
    {
        if (!Loaded)
        {
            return new RSoundEffect();
        }

        return new RSoundEffect
        {
            ChannelCount = (short)_vorbisReader.Channels,
            SampleFrequency = _vorbisReader.SampleRate,
            BytesPerSecond = _vorbisReader.SampleRate * _vorbisReader.Channels * 2,
            BlockAlignment = (short)(_vorbisReader.Channels * 2),
            BitsPerSample = 16,
            DataChunk = _sampleBuffer!,
        };
    }

    public void Dispose()
    {
        _vorbisReader.Dispose();
        _sampleBuffer = null;
    }
}