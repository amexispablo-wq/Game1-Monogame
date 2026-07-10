#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ColorBlocks.Developer.GameplayBenchmark.FuzzTesting;

namespace ColorBlocks.Developer.GameplayBenchmark;

public enum BenchmarkRunMode
{
    Idle,
    Menu,
    RunningAll,
    RunningCategory,
    RunningFuzz,
    RunningReproduce,
    Completed
}

public sealed class BenchmarkRunner
{
    private readonly Queue<BenchmarkScenario> _queue = new();
    private readonly List<BenchmarkResult> _results = new();
    private readonly Stopwatch _runStopwatch = new();
    private BenchmarkScenario? _currentScenario;
    private BenchmarkContext? _context;
    private BenchmarkRunMode _mode = BenchmarkRunMode.Idle;
    private int _fuzzRemaining;
    private int _fuzzBaseSeed;
    private int _completedCount;
    private int _totalCount;
    private string _statusMessage = string.Empty;
    private BenchmarkReport? _lastReport;

    public BenchmarkRunMode Mode => _mode;
    public bool IsMenuOpen { get; private set; }
    public bool IsDebugVisible { get; private set; }
    public BenchmarkScenario? CurrentScenario => _currentScenario;
    public BenchmarkContext? Context => _context;
    public IReadOnlyList<BenchmarkResult> Results => _results;
    public BenchmarkReport? LastReport => _lastReport;
    public string StatusMessage => _statusMessage;
    public int CompletedCount => _completedCount;
    public int TotalCount => _totalCount;
    public TimeSpan Elapsed => _runStopwatch.IsRunning ? _runStopwatch.Elapsed : TimeSpan.Zero;

    public TimeSpan EstimatedRemaining
    {
        get
        {
            if (_completedCount <= 0 || _totalCount <= 0)
            {
                return TimeSpan.Zero;
            }

            double avg = Elapsed.TotalSeconds / _completedCount;
            return TimeSpan.FromSeconds(avg * Math.Max(0, _totalCount - _completedCount));
        }
    }

    public void OpenMenu()
    {
        if (!DeveloperSettings.DeveloperMode)
        {
            return;
        }

        IsMenuOpen = true;
        _mode = BenchmarkRunMode.Menu;
    }

    public void CloseMenu()
    {
        IsMenuOpen = false;
        if (_mode == BenchmarkRunMode.Menu)
        {
            _mode = BenchmarkRunMode.Idle;
        }
    }

    public void ToggleMenu()
    {
        if (IsMenuOpen)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    public void ToggleDebug()
    {
        if (!DeveloperSettings.DeveloperMode)
        {
            return;
        }

        IsDebugVisible = !IsDebugVisible;
    }

    public void StartAll()
    {
        StartScenarios(BenchmarkRegistry.All, BenchmarkRunMode.RunningAll);
    }

    public void StartCategory(BenchmarkCategory category)
    {
        if (category == BenchmarkCategory.Fuzz)
        {
            StartFuzz(BenchmarkSettings.Active.FuzzSimulationCount);
            return;
        }

        StartScenarios(BenchmarkRegistry.GetByCategory(category), BenchmarkRunMode.RunningCategory);
    }

    public void StartFuzz(int count)
    {
        BenchmarkSettings settings = BenchmarkSettings.Active;
        int seed = settings.UseFixedFuzzSeed ? settings.FuzzSeed : Environment.TickCount;
        StartFuzzInternal(count, seed);
    }

    public void StartReproduceSeed(int seed)
    {
        StartFuzzInternal(1, seed);
        _mode = BenchmarkRunMode.RunningReproduce;
    }

    public void Cancel()
    {
        _queue.Clear();
        _currentScenario = null;
        _context = null;
        _fuzzRemaining = 0;
        _mode = BenchmarkRunMode.Completed;
        _statusMessage = "Cancelled";
        FinalizeReport();
    }

    public void Update(double maxMilliseconds = 12d)
    {
        if (_mode is BenchmarkRunMode.Idle or BenchmarkRunMode.Menu or BenchmarkRunMode.Completed)
        {
            return;
        }

        Stopwatch slice = Stopwatch.StartNew();
        while (slice.Elapsed.TotalMilliseconds < maxMilliseconds)
        {
            if (!RunNextUnit())
            {
                _mode = BenchmarkRunMode.Completed;
                FinalizeReport();
                break;
            }
        }
    }

    private void StartScenarios(IEnumerable<BenchmarkScenario> scenarios, BenchmarkRunMode mode)
    {
        _results.Clear();
        _queue.Clear();
        foreach (BenchmarkScenario scenario in scenarios)
        {
            _queue.Enqueue(scenario);
        }

        _totalCount = _queue.Count;
        _completedCount = 0;
        _runStopwatch.Restart();
        _mode = _queue.Count > 0 ? mode : BenchmarkRunMode.Completed;
        _statusMessage = "Running benchmarks";
        _lastReport = null;
        IsMenuOpen = true;
    }

    private void StartFuzzInternal(int count, int seed)
    {
        _results.Clear();
        _queue.Clear();
        _fuzzRemaining = Math.Max(1, count);
        _fuzzBaseSeed = seed;
        _totalCount = _fuzzRemaining;
        _completedCount = 0;
        _runStopwatch.Restart();
        _mode = BenchmarkRunMode.RunningFuzz;
        _statusMessage = $"Fuzz x{_fuzzRemaining}";
        _lastReport = null;
        IsMenuOpen = true;
    }

    private bool RunNextUnit()
    {
        if (_mode == BenchmarkRunMode.RunningFuzz || _mode == BenchmarkRunMode.RunningReproduce)
        {
            return RunNextFuzz();
        }

        if (_currentScenario is null)
        {
            if (!_queue.TryDequeue(out BenchmarkScenario? next))
            {
                return false;
            }

            _currentScenario = next;
            _context = new BenchmarkContext(BenchmarkSettings.Active);
            _statusMessage = next.Name;
        }

        try
        {
            BenchmarkResult result = _currentScenario.Run(_context!);
            _results.Add(result);
        }
        catch (Exception ex)
        {
            _results.Add(new BenchmarkResult(
                _currentScenario.Id,
                _currentScenario.Name,
                _currentScenario.Category,
                BenchmarkVerdict.Fail,
                TimeSpan.Zero,
                new[] { BenchmarkAssertion.Fail("exception", ex.Message) },
                notes: ex.ToString()));
        }

        _completedCount++;
        _currentScenario = null;
        _context = null;
        return _queue.Count > 0 || _fuzzRemaining > 0;
    }

    private bool RunNextFuzz()
    {
        if (_fuzzRemaining <= 0)
        {
            return false;
        }

        int seed = _fuzzBaseSeed + (_totalCount - _fuzzRemaining);
        _context = new BenchmarkContext(BenchmarkSettings.Active, seed) { CurrentSeed = seed };
        _statusMessage = $"Fuzz seed {seed}";
        FuzzScenario scenario = FuzzGenerator.Create(seed);
        try
        {
            FuzzResult fuzzResult = FuzzScenarioRunner.Run(_context, scenario);
            _results.Add(fuzzResult.ToBenchmarkResult());
            if (!fuzzResult.Passed)
            {
                FuzzReplay.SaveFailure(seed, scenario, fuzzResult);
            }
        }
        catch (Exception ex)
        {
            _results.Add(new BenchmarkResult(
                $"fuzz.{seed}",
                $"Fuzz {seed}",
                BenchmarkCategory.Fuzz,
                BenchmarkVerdict.Fail,
                TimeSpan.Zero,
                new[] { BenchmarkAssertion.Fail("fuzz.exception", ex.Message) },
                notes: ex.ToString()));
        }

        _fuzzRemaining--;
        _completedCount++;
        _context = null;
        return _fuzzRemaining > 0;
    }

    private void FinalizeReport()
    {
        _runStopwatch.Stop();
        _lastReport = BenchmarkExporter.CreateReport(_results, _runStopwatch.Elapsed, CollectFuzzSeeds());
        BenchmarkExporter.Export(_lastReport);
        _statusMessage = $"Done P{_lastReport.PassCount} W{_lastReport.WarningCount} F{_lastReport.FailCount}";
    }

    private IEnumerable<int> CollectFuzzSeeds()
    {
        return _results
            .Where(result => result.Category == BenchmarkCategory.Fuzz)
            .Select(result => ParseSeed(result.ScenarioId))
            .Where(seed => seed.HasValue)
            .Select(seed => seed!.Value);
    }

    private static int? ParseSeed(string scenarioId)
    {
        if (!scenarioId.StartsWith("fuzz.", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(scenarioId.AsSpan(5), out int seed) ? seed : null;
    }
}
