using System.Collections.Generic;
using System.Linq;

namespace Game1_Monogame;

public sealed class NetworkInputBuffer
{
    private static readonly IReadOnlyDictionary<int, PlayerInputState> EmptyInputs = new Dictionary<int, PlayerInputState>();

    private readonly Dictionary<long, Dictionary<int, PlayerInputState>> _frames = new();

    public int FrameCount => _frames.Count;
    public int TotalFramesStored { get; private set; }
    public int DroppedFrameCount { get; private set; }

    public int StoredInputCount
    {
        get
        {
            int count = 0;
            foreach (Dictionary<int, PlayerInputState> frame in _frames.Values)
            {
                count += frame.Count;
            }

            return count;
        }
    }

    public void StoreFrame(InputFrame frame)
    {
        Dictionary<int, PlayerInputState> inputMap = frame.ToInputMap();
        if (!_frames.ContainsKey(frame.Tick))
        {
            TotalFramesStored++;
        }

        _frames[frame.Tick] = inputMap;
    }

    public IReadOnlyDictionary<int, PlayerInputState> GetInputs(SimulationTick tick)
    {
        return _frames.TryGetValue(tick.Value, out Dictionary<int, PlayerInputState> inputs)
            ? inputs
            : EmptyInputs;
    }

    public PlayerInputState GetInput(SimulationTick tick, int networkId)
    {
        return _frames.TryGetValue(tick.Value, out Dictionary<int, PlayerInputState> inputs)
            && inputs.TryGetValue(networkId, out PlayerInputState input)
                ? input
                : PlayerInputState.Empty;
    }

    public void TrimBefore(SimulationTick newestTick, int keepTickCount)
    {
        long cutoffTick = newestTick.Value - keepTickCount;
        if (cutoffTick <= 0)
        {
            return;
        }

        foreach (long tick in _frames.Keys.ToList())
        {
            if (tick >= cutoffTick)
            {
                continue;
            }

            _frames.Remove(tick);
            DroppedFrameCount++;
        }
    }
}
