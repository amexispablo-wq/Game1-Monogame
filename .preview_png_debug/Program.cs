using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

class Program
{
    static void Main()
    {
        var path = Path.Combine("..", "bin", "Debug", "net9.0", "Content", "LevelPreviews", "Level_1_level_1.png");
        if (!File.Exists(path))
        {
            Console.WriteLine($"Missing file: {path}");
            return;
        }

        using var image = new Bitmap(path);
        int width = image.Width;
        int height = image.Height;
        long total = (long)width * height;
        long transparent = 0;
        long black = 0;
        double sumBrightness = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Color p = image.GetPixel(x, y);
                if (p.A == 0) transparent++;
                if (p.A > 0 && p.R <= 8 && p.G <= 8 && p.B <= 8) black++;
                sumBrightness += (p.R + p.G + p.B) / 3.0;
            }
        }

        Console.WriteLine($"Path: {Path.GetFullPath(path)}");
        Console.WriteLine($"Size: {width} x {height}");
        Console.WriteLine($"Total pixels: {total}");
        Console.WriteLine($"Transparent pixels: {transparent}");
        Console.WriteLine($"Black pixels: {black}");
        Console.WriteLine($"Black %: {(double)black / total * 100:F2}");
        Console.WriteLine($"Avg brightness: {sumBrightness / total:F2}");
    }
}
