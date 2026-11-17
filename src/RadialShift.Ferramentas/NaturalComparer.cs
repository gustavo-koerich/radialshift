using System.Globalization;

namespace RadialShift.Ferramentas;

/// <summary>Ordem alfabética natural: "arquivo2" antes de "arquivo10". Ignora maiúsculas.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static NaturalComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                var si = i; while (i < x.Length && char.IsAsciiDigit(x[i])) i++;
                var sj = j; while (j < y.Length && char.IsAsciiDigit(y[j])) j++;
                var a = x.AsSpan(si, i - si).TrimStart('0');
                var b = y.AsSpan(sj, j - sj).TrimStart('0');
                if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
                var cmp = a.SequenceCompareTo(b);
                if (cmp != 0) return cmp;
            }
            else
            {
                var si = i; while (i < x.Length && !char.IsAsciiDigit(x[i])) i++;
                var sj = j; while (j < y.Length && !char.IsAsciiDigit(y[j])) j++;
                var cmp = string.Compare(x.Substring(si, i - si), y.Substring(sj, j - sj),
                    CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
                if (cmp != 0) return cmp;
            }
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }
}
