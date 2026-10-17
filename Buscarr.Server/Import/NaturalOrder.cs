namespace Buscarr.Server.Import;

public sealed class NaturalOrder : IComparer<string?>
{
    public static readonly NaturalOrder Instance = new();

    public int Compare(string? x, string? y)
    {
        var a = x ?? "";
        var b = y ?? "";
        var i = 0;
        var j = 0;

        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                var numberA = TakeNumber(a, ref i);
                var numberB = TakeNumber(b, ref j);

                if (numberA != numberB)
                {
                    return numberA.CompareTo(numberB);
                }

                continue;
            }

            var letterA = char.ToUpperInvariant(a[i]);
            var letterB = char.ToUpperInvariant(b[j]);

            if (letterA != letterB)
            {
                return letterA.CompareTo(letterB);
            }

            i++;
            j++;
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }

    private static long TakeNumber(string text, ref int index)
    {
        var value = 0L;

        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            value = value * 10 + (text[index] - '0');
            index++;
        }

        return value;
    }
}
