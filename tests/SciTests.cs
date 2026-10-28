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
                case ".": e.Decimal(); break;
                case "(": e.OpenParen(); break;
                case ")": e.CloseParen(); break;
                case "+": e.Operator("+"); break;
                case "-": e.Operator("−"); break;
                case "*": e.Operator("×"); break;
                case "/": e.Operator("÷"); break;
                case "^": e.Operator("^"); break;
                case "mod": e.Operator(" mod "); break;
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

    static void DecimalPoint()
    {
        CalcEngine.DecimalSeparator = ",";
        Eq("point after digits", K("3 . 1 4").DisplayText, "3,14");
        Eq("point first becomes 0.", K(".").Buffer, "0.");
        Eq("point first then digit", K(". 5").Buffer, "0.5");
        Eq("second point ignored", K("1 . 2 . 3").Buffer, "1.23");
        Eq("zero point zero", K("0 . 0 5").Buffer, "0.05");
        Eq("digit limit ignores the point", K("1 2 3 4 5 6 7 8 . 9 0 1 2 3 4 5 6 7").Buffer, "12345678.90123456");
    }

    static void Parentheses()
    {
        Eq("group", K("( 1 + 2 )").Buffer, "(1+2)");
        Eq("close without open", K("1 )").Buffer, "1");
        Eq("close right after open", K("( )").Buffer, "(");
        Eq("close after operator", K("( 1 + )").Buffer, "(1+");
        Eq("close after sign", K("( - )").Buffer, "(−");
        Eq("nested", K("( ( 1 + 2 ) * 3 )").Buffer, "((1+2)×3)");
        Eq("extra close ignored", K("( 1 ) )").Buffer, "(1)");
        Eq("open after digit keeps both", K("2 (").Buffer, "2(");
        Eq("minus allowed after open", K("( -").Buffer, "(−");
        Eq("other operator after open ignored", K("( *").Buffer, "(");
    }

    static void Operators()
    {
        Eq("plain", K("1 + 2").Buffer, "1+2");
        Eq("all symbols", K("1 + 2 - 3 * 4 / 5 ^ 6").Buffer, "1+2−3×4÷5^6");
        Eq("mod has spaces", K("7 mod 3").Buffer, "7 mod 3");
        Eq("second operator replaces the first", K("5 + *").Buffer, "5×");
        Eq("replace with minus", K("5 * -").Buffer, "5−");
        Eq("replace mod", K("5 mod +").Buffer, "5+");
        Eq("operator first gets a zero", K("*").Buffer, "0×");
        Eq("minus first is a sign", K("-").Buffer, "−");
        Eq("sign then operator ignored", K("- *").Buffer, "−");
        Eq("sign then digit", K("- 5").Buffer, "−5");
        Eq("after decimal point", K("2 . 5 +").Buffer, "2.5+");
    }

    public static int Run()
    {
        failed = 0;
        Digits();
        DecimalPoint();
        Operators();
        Parentheses();
        return failed;
    }
}
