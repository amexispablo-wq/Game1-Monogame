using System;

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

    /// <summary>Last cell of every grid row navigates Right to <paramref name="targetIndex"/>.</summary>
    public static void LinkGridRowEndsRightTo(
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
        for (int row = 0; row < rows; row++)
        {
            int rowStart = row * columns;
            int lastInRow = Math.Min(rowStart + columns - 1, gridCount - 1);
            graph.Link(gridStartIndex + lastInRow, NavigationDirection.Right, targetIndex);
        }
    }
}
