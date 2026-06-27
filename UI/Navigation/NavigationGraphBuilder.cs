using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public static class NavigationGraphBuilder
{
    public static void LinkGridBottomRowTo(
        NavigationGraph graph,
        int gridStartIndex,
        int gridCount,
        int columns,
        int targetIndex)
    {
        if (gridCount <= 0 || columns <= 0)
        {
            return;
        }

        int rows = (gridCount + columns - 1) / columns;
        int lastRowStart = gridStartIndex + ((rows - 1) * columns);
        int gridEnd = gridStartIndex + gridCount;

        for (int index = lastRowStart; index < gridEnd; index++)
        {
            graph.Link(index, NavigationDirection.Down, targetIndex);
        }

        graph.Link(targetIndex, NavigationDirection.Up, lastRowStart);
    }
}
