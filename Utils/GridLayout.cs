using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

public sealed class GridLayout
{
    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public Rectangle[] CellBounds { get; private set; }

    private GridLayout()
    {
        CellBounds = Array.Empty<Rectangle>();
    }

    public static GridLayout Create(int itemCount, int viewportWidth, int viewportHeight,
        int cellWidth, int cellHeight, int horizontalGap = 20, int verticalGap = 20)
    {
        var layout = new GridLayout();

        if (itemCount == 0)
        {
            layout.Columns = 0;
            layout.Rows = 0;
            layout.CellBounds = Array.Empty<Rectangle>();
            return layout;
        }

        // Calculate columns based on available space
        int availableWidth = viewportWidth - (horizontalGap * 2);
        int maxColumns = Math.Max(1, availableWidth / (cellWidth + horizontalGap));

        // Calculate rows
        layout.Columns = Math.Min(maxColumns, itemCount);
        layout.Rows = (itemCount + layout.Columns - 1) / layout.Columns;

        // Calculate total grid size
        int gridWidth = (layout.Columns * cellWidth) + ((layout.Columns - 1) * horizontalGap);
        int gridHeight = (layout.Rows * cellHeight) + ((layout.Rows - 1) * verticalGap);

        // Center grid on screen
        int startX = (viewportWidth - gridWidth) / 2;
        int startY = (viewportHeight - gridHeight) / 2;
        if (startY < 80)
            startY = 80;

        // Calculate cell bounds
        layout.CellBounds = new Rectangle[itemCount];
        for (int i = 0; i < itemCount; i++)
        {
            int row = i / layout.Columns;
            int col = i % layout.Columns;

            int x = startX + (col * (cellWidth + horizontalGap));
            int y = startY + (row * (cellHeight + verticalGap));

            layout.CellBounds[i] = new Rectangle(x, y, cellWidth, cellHeight);
        }

        return layout;
    }

    public int? GetCellAtPoint(Point point)
    {
        for (int i = 0; i < CellBounds.Length; i++)
        {
            if (CellBounds[i].Contains(point))
            {
                return i;
            }
        }

        return null;
    }
}
