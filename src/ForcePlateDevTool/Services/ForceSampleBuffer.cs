using ForcePlateDevTool.Models;

namespace ForcePlateDevTool.Services;

public sealed class ForceSampleBuffer
{
    private const int MaxSamples = 1200;
    private static readonly TimeSpan VisibleWindow =
        TimeSpan.FromSeconds(10);

    private readonly List<ForceSample> _samples = new();

    public int SampleCount => _samples.Count;

    public IReadOnlyList<ForceSample> Samples => _samples;

    public void Add(ForceSample sample)
    {
        _samples.Add(sample);

        DateTime cutoff = sample.Timestamp - VisibleWindow;

        while (_samples.Count > 0 &&
               (_samples[0].Timestamp < cutoff ||
                _samples.Count > MaxSamples))
        {
            _samples.RemoveAt(0);
        }
    }

    public void Clear()
    {
        _samples.Clear();
    }
}