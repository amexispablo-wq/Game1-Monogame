using System;
using ColorBlocks.Developer.GameplayBenchmark;

if (args.Length > 0 && string.Equals(args[0], "--benchmark", StringComparison.OrdinalIgnoreCase))
{
    Environment.Exit(BenchmarkHeadlessRunner.Execute(args));
}

using var game = new ColorBlocks.ColorBlocksGame();
game.Run();
