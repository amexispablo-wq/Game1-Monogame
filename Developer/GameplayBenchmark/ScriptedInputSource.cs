#nullable enable
using System.Collections.Generic;

namespace ColorBlocks.Developer.GameplayBenchmark;

public sealed class ScriptedInputSource : ILocalPlayerInputSource
{
    private readonly Dictionary<int, PlayerInputState> _inputs = new();
    private readonly Dictionary<int, PlayerInputState> _latched = new();

    public void SetInput(int networkId, PlayerInputState input)
    {
        _inputs[networkId] = input;
    }

    public void SetAll(PlayerInputState input)
    {
        _inputs.Clear();
        foreach (int key in _latched.Keys)
        {
            _inputs[key] = input;
        }
    }

    public void ClearAll()
    {
        _inputs.Clear();
        _latched.Clear();
    }

    public PlayerInputState GetPlayerInput(int networkId)
    {
        if (_inputs.TryGetValue(networkId, out PlayerInputState input))
        {
            _latched[networkId] = input;
            return input;
        }

        return _latched.GetValueOrDefault(networkId, PlayerInputState.Empty);
    }

    public void BindPlayers(IReadOnlyList<Player> players)
    {
        foreach (Player player in players)
        {
            if (!_inputs.ContainsKey(player.NetworkId))
            {
                _inputs[player.NetworkId] = PlayerInputState.Empty;
            }
        }
    }
}
