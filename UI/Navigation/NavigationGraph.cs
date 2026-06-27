using System;
using System.Collections.Generic;

namespace ColorBlocks;

public sealed class NavigationGraph
{
    private readonly List<NavigationNode> _nodes = new();

    public int NodeCount => _nodes.Count;

    public void Clear()
    {
        _nodes.Clear();
    }

    public int AddNode()
    {
        int index = _nodes.Count;
        _nodes.Add(new NavigationNode());
        return index;
    }

    public void Link(int from, NavigationDirection direction, int to)
    {
        if (from < 0 || from >= _nodes.Count || to < 0 || to >= _nodes.Count)
        {
            return;
        }

        _nodes[from].Set(direction, to);
    }

    public void LinkPair(int a, NavigationDirection dirFromA, int b, NavigationDirection dirFromB)
    {
        Link(a, dirFromA, b);
        Link(b, dirFromB, a);
    }

    public void LinkVertical(int from, int to)
    {
        LinkPair(from, NavigationDirection.Down, to, NavigationDirection.Up);
    }

    public void LinkHorizontal(int from, int to)
    {
        LinkPair(from, NavigationDirection.Right, to, NavigationDirection.Left);
    }

    public void WireGrid(int startIndex, int count, int columns)
    {
        if (count <= 0 || columns <= 0)
        {
            return;
        }

        int rows = (count + columns - 1) / columns;
        for (int i = 0; i < count; i++)
        {
            int index = startIndex + i;
            int row = i / columns;
            int col = i % columns;

            if (col > 0)
            {
                Link(index, NavigationDirection.Left, startIndex + i - 1);
            }

            if (col < columns - 1)
            {
                int rightNeighbor = i + 1;
                if (rightNeighbor < count && rightNeighbor / columns == row)
                {
                    Link(index, NavigationDirection.Right, startIndex + rightNeighbor);
                }
            }

            if (row > 0)
            {
                Link(index, NavigationDirection.Up, startIndex + i - columns);
            }

            if (row < rows - 1)
            {
                int downNeighbor = i + columns;
                if (downNeighbor < count)
                {
                    Link(index, NavigationDirection.Down, startIndex + downNeighbor);
                }
            }
        }
    }

    public void WireVerticalChain(IReadOnlyList<int> indices)
    {
        for (int i = 0; i < indices.Count - 1; i++)
        {
            LinkVertical(indices[i], indices[i + 1]);
        }
    }

    public int? GetNeighbor(int from, NavigationDirection direction)
    {
        if (from < 0 || from >= _nodes.Count)
        {
            return null;
        }

        return _nodes[from].Get(direction);
    }

    private sealed class NavigationNode
    {
        private readonly int?[] _neighbors = new int?[4];

        public int? Get(NavigationDirection direction) => _neighbors[(int)direction];

        public void Set(NavigationDirection direction, int to)
        {
            _neighbors[(int)direction] = to;
        }
    }
}
