#nullable enable
using System;

namespace ColorBlocks;

/// <summary>Thrown when an official (or other integrity-gated) level fails hash/schema checks.</summary>
public sealed class LevelIntegrityException : Exception
{
    public string LevelId { get; }
    public string UserMessage { get; }

    public LevelIntegrityException(string levelId, string userMessage, string? technicalDetail = null)
        : base(technicalDetail ?? userMessage)
    {
        LevelId = levelId;
        UserMessage = userMessage;
    }
}
