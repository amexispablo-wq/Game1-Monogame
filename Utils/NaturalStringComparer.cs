#nullable enable
using System;
using System.Collections.Generic;

namespace ColorBlocks;

/// <summary>
/// Case-insensitive alphanumeric compare: digit runs ordered numerically so "10" sorts after "2".
/// </summary>
public sealed class NaturalStringComparer : IComparer<string?>
{
    public static NaturalStringComparer Instance { get; } = new();

    int IComparer<string?>.Compare(string? x, string? y) => CompareStrings(x, y);

    public static int Compare(string? x, string? y) => CompareStrings(x, y);

    private static int CompareStrings(string? a, string? b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        if (a is null)
        {
            return -1;
        }

        if (b is null)
        {
            return 1;
        }

        int i = 0;
        int j = 0;
        while (i < a.Length && j < b.Length)
        {
            char ca = a[i];
            char cb = b[j];
            bool digitA = char.IsDigit(ca);
            bool digitB = char.IsDigit(cb);

            if (digitA && digitB)
            {
                long numA = ReadNumber(a, ref i);
                long numB = ReadNumber(b, ref j);
                int numberCompare = numA.CompareTo(numB);
                if (numberCompare != 0)
                {
                    return numberCompare;
                }

                continue;
            }

            int charCompare = char.ToUpperInvariant(ca).CompareTo(char.ToUpperInvariant(cb));
            if (charCompare != 0)
            {
                return charCompare;
            }

            i++;
            j++;
        }

        return a.Length.CompareTo(b.Length);
    }

    private static long ReadNumber(string s, ref int index)
    {
        long value = 0;
        while (index < s.Length && char.IsDigit(s[index]))
        {
            value = (value * 10) + (s[index] - '0');
            index++;
        }

        return value;
    }
}
