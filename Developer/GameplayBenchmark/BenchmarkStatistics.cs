#nullable enable
using System.Collections.Generic;

namespace ColorBlocks.Developer.GameplayBenchmark;

public sealed class BenchmarkStatistics
{
    private readonly Dictionary<string, double> _values = new();

    public IReadOnlyDictionary<string, double> Values => _values;

    public void Set(string key, double value)
    {
        _values[key] = value;
    }

    public void SetMax(string key, double value)
    {
        if (!_values.TryGetValue(key, out double existing) || value > existing)
        {
            _values[key] = value;
        }
    }

    public void SetMin(string key, double value)
    {
        if (!_values.TryGetValue(key, out double existing) || value < existing)
        {
            _values[key] = value;
        }
    }

    public void AddSample(string key, double value)
    {
        string countKey = key + ".count";
        string sumKey = key + ".sum";
        _values[countKey] = _values.GetValueOrDefault(countKey) + 1d;
        _values[sumKey] = _values.GetValueOrDefault(sumKey) + value;
        _values[key + ".avg"] = _values[sumKey] / _values[countKey];
        SetMax(key + ".max", value);
        SetMin(key + ".min", value);
    }

    public bool TryGet(string key, out double value) => _values.TryGetValue(key, out value);
}
