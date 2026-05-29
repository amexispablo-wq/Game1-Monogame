namespace ColorBlocks;

public sealed class NetworkIdAllocator
{
    private int _nextNetworkId = 1;

    public int Allocate()
    {
        return _nextNetworkId++;
    }

    public void Reserve(int networkId)
    {
        if (networkId >= _nextNetworkId)
        {
            _nextNetworkId = networkId + 1;
        }
    }
}
