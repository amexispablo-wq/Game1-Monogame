using System;

namespace ColorBlocks;

public readonly struct PartyMemberId : IEquatable<PartyMemberId>
{
    public PartyMemberId(int value) => Value = value;

    public int Value { get; }

    public bool Equals(PartyMemberId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is PartyMemberId other && Equals(other);

    public override int GetHashCode() => Value;

    public static bool operator ==(PartyMemberId left, PartyMemberId right) => left.Equals(right);

    public static bool operator !=(PartyMemberId left, PartyMemberId right) => !left.Equals(right);

    public override string ToString() => $"PartyMember#{Value}";
}
