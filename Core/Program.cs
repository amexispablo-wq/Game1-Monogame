using System;
using System.IO;
using ColorBlocks.Developer.GameplayBenchmark;

// Steam (and some shells) can launch with a cwd outside the install folder.
// Song.FromUri relative paths resolve against cwd — pin it to the exe directory.
try
{
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
}
catch
{
    // Best-effort; content resolution still probes BaseDirectory explicitly.
}

if (args.Length > 0 && string.Equals(args[0], "--benchmark", StringComparison.OrdinalIgnoreCase))
{
    Environment.Exit(BenchmarkHeadlessRunner.Execute(args));
}

using var game = new ColorBlocks.ColorBlocksGame();
game.Run();
