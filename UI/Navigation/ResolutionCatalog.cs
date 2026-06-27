using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public static class ResolutionCatalog
{
    // Modern, commonly used resolutions only. Sorted by aspect ratio, then resolution.
    // Old 4:3 resolutions intentionally excluded.
    private static readonly (string Label, double Ratio, (int W, int H)[] Resolutions)[] Catalog =
    {
        ("16:9", 16.0 / 9.0, new[]
        {
            (1280, 720), (1366, 768), (1600, 900), (1920, 1080),
            (2560, 1440), (3840, 2160)
        }),
        ("16:10", 16.0 / 10.0, new[]
        {
            (1920, 1200)
        }),
        ("21:9", 21.0 / 9.0, new[]
        {
            (2560, 1080), (3440, 1440)
        })
    };

    public static List<Resolution> GetSupportedResolutions(GraphicsDevice? graphicsDevice = null)
    {
        HashSet<(int W, int H)> supported = GetMonitorResolutionSet(graphicsDevice);
        var results = new List<Resolution>();

        foreach ((_, _, (int W, int H)[] resolutions) in Catalog)
        {
            foreach ((int w, int h) in resolutions)
            {
                if (!supported.Contains((w, h)))
                {
                    continue;
                }

                if (results.Any(r => r.Width == w && r.Height == h))
                {
                    continue;
                }

                results.Add(new Resolution(w, h));
            }
        }

        if (results.Count == 0)
        {
            results.Add(new Resolution(1280, 720));
            results.Add(new Resolution(1920, 1080));
        }

        return results;
    }

    private static HashSet<(int W, int H)> GetMonitorResolutionSet(GraphicsDevice? graphicsDevice)
    {
        var set = new HashSet<(int W, int H)>();
        GraphicsAdapter adapter = graphicsDevice?.Adapter ?? GraphicsAdapter.DefaultAdapter;

        foreach (Microsoft.Xna.Framework.Graphics.DisplayMode mode in adapter.SupportedDisplayModes)
        {
            set.Add((mode.Width, mode.Height));
        }

        if (set.Count == 0)
        {
            foreach ((_, _, (int W, int H)[] resolutions) in Catalog)
            {
                foreach ((int w, int h) in resolutions)
                {
                    set.Add((w, h));
                }
            }
        }

        return set;
    }
}
