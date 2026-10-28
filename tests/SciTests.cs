using System;
using Calculator;

/// <summary>Tests for the scientific-mode input engine.</summary>
static class SciTests
{
    static int failed;

    static void Eq(string name, string actual, string expected)
    {
        if (actual == expected) return;
        failed++;
        Console.WriteLine("FAIL {0}: expected '{1}', got '{2}'", name, expected, actual);
    }

    /// <summary>Plays a space-separated list of keys, e.g. "1 2 + 3 =".</summary>
    static SciEngine K(string spec)
    {
        var e = new SciEngine();
        foreach (string key in spec.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (key.Length == 1 && char.IsDigit(key[0])) { e.Digit(key[0]); continue; }
            switch (key)
            {
                case "C": e.Clear(); break;
                default: throw new ArgumentException("unknown key " + key);
            }
        }
        return e;
    }

    static void Digits()
    {
        Eq("empty shows zero", K("").DisplayText, "0");
        Eq("one digit", K("7").DisplayText, "7");
        Eq("several digits", K("1 2 3").DisplayText, "123");
        Eq("leading zero is replaced", K("0 5").DisplayText, "5");
        Eq("zeros collapse", K("0 0 0").DisplayText, "0");
        Eq("digit limit", K("1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8").Buffer, "1234567890123456");
        Eq("clear", K("1 2 C").DisplayText, "0");
        Eq("buffer", K("4 2").Buffer, "42");
    }

    public static int Run()
    {
        failed = 0;
        Digits();
        return failed;
    }
}
