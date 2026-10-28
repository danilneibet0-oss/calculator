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
                case "=": e.Evaluate(); break;
                case ".": e.Decimal(); break;
                case "sin": case "cos": case "tan": case "asin": case "acos": case "atan":
                case "ln": case "log": case "exp": case "abs":
                    e.Function(key); break;
                case "sqrt": e.Function("√"); break;
                case "cbrt": e.Function("∛"); break;
                case "sqr": e.Square(); break;
                case "inv": e.Reciprocal(); break;
                case "ten": e.TenPower(); break;
                case "pi": e.Constant("π"); break;
                case "e": e.Constant("e"); break;
                case "!": e.Factorial(); break;
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

    static void Errors()
    {
        Eq("divide by zero", K("5 / 0 =").DisplayText, "Деление на ноль невозможно");
        Eq("zero over zero", K("0 / 0 =").DisplayText, "Результат не определён");
        Eq("root of negative", K("sqrt - 4 =").DisplayText, "Недопустимый ввод");
        Eq("fractional power of negative", K("( - 8 ) ^ ( 1 / 3 ) =").DisplayText, "Недопустимый ввод");
        Eq("log of zero", K("ln 0 =").DisplayText, "Недопустимый ввод");
        Eq("factorial overflow", K("1 7 1 ! =").DisplayText, "Переполнение");
        Eq("syntax error", K("( =").DisplayText, "Ошибка в выражении");
        Eq("sign alone", K("- =").DisplayText, "Ошибка в выражении");
        Eq("flag is set", K("5 / 0 =").HasError.ToString(), "True");
        Eq("flag is not set by a good result", K("5 / 1 =").HasError.ToString(), "False");
        Eq("expression line is kept", K("5 / 0 =").Expression, "5÷0 =");
        Eq("clear recovers", K("5 / 0 = C").DisplayText, "0");
        Eq("clear resets the flag", K("5 / 0 = C").HasError.ToString(), "False");
        Eq("equals again changes nothing", K("5 / 0 = =").DisplayText, "Деление на ноль невозможно");
    }

    static void Evaluation()
    {
        CalcEngine.DecimalSeparator = ",";
        Eq("sum", K("1 + 2 =").DisplayText, "3");
        Eq("expression line", K("1 + 2 =").Expression, "1+2 =");
        Eq("open group is closed", K("( 1 + 2 =").DisplayText, "3");
        Eq("open group in the expression line", K("( 1 + 2 =").Expression, "(1+2) =");
        Eq("trailing operator dropped", K("5 + =").DisplayText, "5");
        Eq("trailing operator in the expression line", K("5 + =").Expression, "5 =");
        Eq("function", K("2 sin 3 0 =").DisplayText, "1");
        Eq("function with open call", K("sin 3 0 =").DisplayText, "0,5");
        Eq("binary noise removed", K("0 . 1 + 0 . 2 =").DisplayText, "0,3");
        Eq("one third", K("1 / 3 =").DisplayText, "0,333333333333333");
        Eq("third times three", K("1 / 3 * 3 =").DisplayText, "1");
        Eq("implicit pi", K("2 pi =").DisplayText, "6,28318530717959");
        Eq("negative result", K("3 - 5 =").DisplayText, "-2");
        Eq("negative result buffer uses the minus sign", K("3 - 5 =").Buffer, "−2");
        Eq("grouped", K("1 2 3 4 5 6 * 1 0 =").DisplayText, "1 234 560");
        Eq("exponent display", K("1 2 3 4 5 6 7 * 1 0 0 0 0 0 0 0 0 0 0 0 =").DisplayText, "1,234567e+17");
        Eq("exponent buffer can be typed on", K("1 2 3 4 5 6 7 * 1 0 0 0 0 0 0 0 0 0 0 0 =").Buffer, "1.234567×10^(17)");
        Eq("small exponent buffer", K("1 / 8 0 0 0 0 0 0 0 =").Buffer, "1.25×10^(−8)");
        Eq("equals on empty does nothing", K("=").DisplayText, "0");
    }

    static void Functions()
    {
        Eq("sin opens a call", K("sin").Buffer, "sin(");
        Eq("argument and close", K("sin 3 0 )").Buffer, "sin(30)");
        Eq("sqrt sign", K("sqrt").Buffer, "√(");
        Eq("cbrt sign", K("cbrt").Buffer, "∛(");
        Eq("number before function", K("2 sin").Buffer, "2sin(");
        Eq("nested calls", K("ln exp 5 ) )").Buffer, "ln(exp(5))");
        Eq("every name", K("asin acos atan ln log exp abs").Buffer, "asin(acos(atan(ln(log(exp(abs(");
        Eq("square", K("5 sqr").Buffer, "5^2");
        Eq("square of a group", K("( 2 + 3 ) sqr").Buffer, "(2+3)^2");
        Eq("square needs an operand", K("sqr").Buffer, "");
        Eq("reciprocal wraps the number", K("4 inv").Buffer, "1÷(4)");
        Eq("reciprocal after operator", K("2 + 4 inv").Buffer, "2+1÷(4)");
        Eq("reciprocal on empty", K("inv").Buffer, "1÷(");
        Eq("reciprocal of a group is ignored", K("( 1 ) inv").Buffer, "(1)");
        Eq("ten power", K("ten").Buffer, "10^(");
        Eq("ten power after number multiplies", K("2 ten").Buffer, "2×10^(");
        try { new SciEngine().Function("nope"); failed++; Console.WriteLine("FAIL unknown function accepted"); }
        catch (ArgumentException) { }
    }

    static void ConstantsAndFactorial()
    {
        Eq("pi", K("pi").Buffer, "π");
        Eq("e", K("e").Buffer, "e");
        Eq("number then pi", K("2 pi").Buffer, "2π");
        Eq("pi then number", K("pi 2").Buffer, "π2");
        Eq("factorial of a digit", K("5 !").Buffer, "5!");
        Eq("factorial twice", K("3 ! !").Buffer, "3!!");
        Eq("factorial of a group", K("( 3 ) !").Buffer, "(3)!");
        Eq("factorial of a constant", K("e !").Buffer, "e!");
        Eq("factorial needs an operand", K("!").Buffer, "");
        Eq("factorial after operator", K("5 + !").Buffer, "5+");
        Eq("factorial after open paren", K("( !").Buffer, "(");
        Eq("factorial after point", K("5 . !").Buffer, "5.");
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
        ConstantsAndFactorial();
        Functions();
        Evaluation();
        Errors();
        return failed;
    }
}
