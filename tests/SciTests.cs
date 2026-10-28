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
                case "angle": e.CycleAngle(); break;
                case "neg": e.Negate(); break;
                case "bs": e.Backspace(); break;
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

    static void CurrentValue()
    {
        var e = K("2 + 3");
        Eq("value of an expression", V(e), "True 5");
        Eq("buffer is untouched", e.Buffer, "2+3");
        Eq("still not evaluated", e.Expression, "");
        Eq("trailing operator is ignored", V(K("2 +")), "True 2");
        Eq("open groups are closed", V(K("( 2 + 3")), "True 5");
        Eq("function in progress", V(K("sin 3 0")), "True 0.5");
        Eq("result after equals", V(K("2 + 3 =")), "True 5");
        Eq("empty has no value", V(K("")), "False 0");
        Eq("error has no value", V(K("5 / 0 =")), "False 0");
        Eq("non-integer factorial has no value", V(K("pi !")), "False 0");
        Eq("divide by zero has no value", V(K("5 / 0")), "False 0");
        Eq("it did not turn into an error", K("5 / 0").HasError.ToString(), "False");
    }

    static string V(SciEngine e)
    {
        double v;
        bool ok = e.TryGetValue(out v);
        return ok + " " + v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    static void Clipboard()
    {
        CalcEngine.DecimalSeparator = ",";
        Eq("copy the expression while typing", K("1 . 5 + 2").CopyText, "1,5+2");
        Eq("copy the result after equals", K("1 . 5 + 2 =").CopyText, "3,5");
        Eq("copy nothing on an error", K("5 / 0 =").CopyText, "");
        Eq("copy an exponent result", K("1 0 0 0 0 0 0 0 0 0 0 * 1 0 0 0 0 0 0 0 0 0 0 =").CopyText, "1e+20");

        var e = new SciEngine();
        Eq("paste returns true", e.Paste("12,5").ToString(), "True");
        Eq("paste into empty", e.Buffer, "12.5");
        e.Operator("+"); e.Paste("1 000");
        Eq("paste after an operator", e.Buffer, "12.5+1000");
        e.Paste("7");
        Eq("paste replaces the number being typed", e.Buffer, "12.5+7");
        e.Paste("-3");
        Eq("negative numbers are wrapped", e.Buffer, "12.5+(−3)");
        Eq("bad text is refused", e.Paste("abc").ToString(), "False");
        Eq("bad text changes nothing", e.Buffer, "12.5+(−3)");
        e.Evaluate();
        Eq("pasted expression evaluates", e.DisplayText, "9,5");
        e.Paste("4");
        Eq("paste after equals starts again", e.Buffer, "4");
        Eq("paste after an error starts again", K("5 / 0 =").Paste("8").ToString(), "True");
    }

    static void RecallFromHistory()
    {
        CalcEngine.DecimalSeparator = ",";
        var e = new SciEngine();
        e.Recall("2 + 3 =", "5");
        Eq("shows the result", e.DisplayText, "5");
        Eq("shows the stored expression line", e.Expression, "2 + 3 =");
        e.Operator("×"); e.Digit('2'); e.Evaluate();
        Eq("continues from it", e.DisplayText, "10");

        e.Recall("x =", "0.25");
        Eq("fraction", e.DisplayText, "0,25");
        Eq("fraction buffer", e.Buffer, "0.25");

        e.Recall("x =", "1e+20");
        Eq("exponent result", e.DisplayText, "1e+20");
        Eq("exponent buffer", e.Buffer, "1×10^(20)");
        e.Operator("÷"); e.Digit('2'); e.Evaluate();
        Eq("exponent result continues", e.DisplayText, "5e+19");

        e.Recall("x =", "-7");
        Eq("negative", e.DisplayText, "-7");
        e.Digit('3');
        Eq("a digit starts again", e.DisplayText, "3");
        Eq("and clears the expression line", e.Expression, "");

        var failedState = new SciEngine();
        failedState.Digit('5'); failedState.Operator("÷"); failedState.Digit('0'); failedState.Evaluate();
        failedState.Recall("1 + 1 =", "2");
        Eq("recall recovers from an error", failedState.DisplayText, "2");
    }

    static void CalculatedEvent()
    {
        CalcEngine.DecimalSeparator = ",";
        var e = new SciEngine();
        string expr = null, result = null;
        int count = 0;
        e.Calculated += delegate(string x, string r) { expr = x; result = r; count++; };

        e.Digit('1'); e.Operator("+"); e.Digit('2'); e.Evaluate();
        Eq("fires once", count.ToString(), "1");
        Eq("expression line", expr, "1+2 =");
        Eq("invariant result", result, "3");

        e.Clear(); e.Digit('1'); e.Decimal(); e.Digit('5'); e.Operator("×"); e.Digit('2'); e.Evaluate();
        Eq("uses the local separator in the expression", expr, "1,5×2 =");
        Eq("keeps the invariant separator in the result", result, "3");

        e.Clear(); e.Digit('1'); e.Operator("÷"); e.Digit('4'); e.Evaluate();
        Eq("fraction result", result, "0.25");

        e.Clear(); e.Digit('5'); e.Operator("÷"); e.Digit('0'); e.Evaluate();
        Eq("not fired on error", count.ToString(), "3");

        e.Clear(); e.Evaluate();
        Eq("not fired on empty", count.ToString(), "3");

        e.Clear(); e.Digit('9'); e.Evaluate(); e.Evaluate();
        Eq("a second equals does not repeat it", count.ToString(), "4");

        var big = new SciEngine();
        big.Calculated += delegate(string x, string r) { result = r; };
        foreach (char c in "10000000000") big.Digit(c);
        big.Operator("×");
        foreach (char c in "10000000000") big.Digit(c);
        big.Evaluate();
        Eq("exponent form", result, "1e+20");
    }

    static void AngleModes()
    {
        Eq("degrees by default", new SciEngine().AngleLabel, "DEG");
        Eq("sin 90 degrees", K("sin 9 0 ) =").DisplayText, "1");
        Eq("cycle to radians", K("angle").AngleLabel, "RAD");
        Eq("cycle to gradians", K("angle angle").AngleLabel, "GRAD");
        Eq("cycle back to degrees", K("angle angle angle").AngleLabel, "DEG");
        CalcEngine.DecimalSeparator = ",";
        Eq("sin 90 radians", K("angle sin 9 0 ) =").DisplayText, "0,893996663600558");
        Eq("sin 100 gradians", K("angle angle sin 1 0 0 ) =").DisplayText, "1");
        Eq("asin in radians", K("angle asin 1 ) =").DisplayText, "1,5707963267949");
        Eq("changing the mode keeps the shown result", K("sin 9 0 ) = angle").DisplayText, "1");
        Eq("mode applies to the next evaluation", K("sin 3 0 ) = angle C sin 3 0 ) =").DisplayText, "-0,988031624092862");
    }

    static void Negation()
    {
        Eq("negate", K("5 neg").Buffer, "−5");
        Eq("negate twice", K("5 neg neg").Buffer, "5");
        Eq("after plus", K("2 + 3 neg").Buffer, "2+−3");
        Eq("after minus", K("2 - 3 neg").Buffer, "2−−3");
        Eq("after minus twice", K("2 - 3 neg neg").Buffer, "2−3");
        Eq("after open paren", K("( 4 neg").Buffer, "(−4");
        Eq("after times with decimals", K("2 * 3 . 5 neg").Buffer, "2×−3.5");
        Eq("after mod", K("7 mod 3 neg").Buffer, "7 mod −3");
        Eq("on empty", K("neg").Buffer, "");
        Eq("after operator", K("5 + neg").Buffer, "5+");
        Eq("only the last number", K("1 2 + 3 4 neg").Buffer, "12+−34");
        Eq("result is negated", K("2 + 3 = neg").Buffer, "−5");
        Eq("negative result is flipped back", K("2 - 5 = neg").Buffer, "3");
        Eq("negated number evaluates", K("2 + 3 neg =").DisplayText, "-1");
        Eq("double minus evaluates", K("2 - 3 neg =").DisplayText, "5");
    }

    static void Backspace()
    {
        Eq("one digit", K("1 2 3 bs").Buffer, "12");
        Eq("last digit leaves empty", K("7 bs").Buffer, "");
        Eq("on empty does nothing", K("bs").Buffer, "");
        Eq("operator", K("1 + bs").Buffer, "1");
        Eq("mod is removed whole", K("7 mod bs").Buffer, "7");
        Eq("sin is removed whole", K("2 sin bs").Buffer, "2");
        Eq("asin is removed whole", K("asin bs").Buffer, "");
        Eq("sqrt is removed whole", K("sqrt bs").Buffer, "");
        Eq("cbrt is removed whole", K("cbrt bs").Buffer, "");
        Eq("ln is removed whole", K("ln bs").Buffer, "");
        Eq("paren is one character", K("( 1 ) bs").Buffer, "(1");
        Eq("decimal point", K("3 . bs").Buffer, "3");
        Eq("pi", K("2 pi bs").Buffer, "2");
        Eq("inside a function", K("sin 3 0 bs").Buffer, "sin(3");
        Eq("after equals it edits the result", K("1 0 + 5 = bs").Buffer, "1");
        Eq("after equals the expression line goes", K("1 0 + 5 = bs").Expression, "");
        Eq("while an error is shown nothing happens", K("5 / 0 = bs").DisplayText, "Деление на ноль невозможно");
    }

    static void AfterEquals()
    {
        CalcEngine.DecimalSeparator = ",";
        Eq("digit starts a new expression", K("2 + 3 = 7").DisplayText, "7");
        Eq("digit clears the expression line", K("2 + 3 = 7").Expression, "");
        Eq("decimal point starts a new expression", K("2 + 3 = .").Buffer, "0.");
        Eq("function starts a new expression", K("2 + 3 = sin").Buffer, "sin(");
        Eq("constant starts a new expression", K("2 + 3 = pi").Buffer, "π");
        Eq("open paren starts a new expression", K("2 + 3 = (").Buffer, "(");
        Eq("operator continues from the result", K("2 + 3 = * 4 =").DisplayText, "20");
        Eq("operator clears the expression line", K("2 + 3 = *").Expression, "");
        Eq("operator keeps the result in the buffer", K("2 + 3 = *").Buffer, "5×");
        Eq("factorial continues", K("2 + 1 = !").Buffer, "3!");
        Eq("square continues", K("2 + 3 = sqr =").DisplayText, "25");
        Eq("reciprocal continues", K("4 = inv =").DisplayText, "0,25");
        Eq("ten power continues", K("2 = ten 2 ) =").DisplayText, "200");
        Eq("exponent result continues", K("1 2 3 4 5 6 7 * 1 0 0 0 0 0 0 0 0 0 0 0 = * 2 =").DisplayText, "2,469134e+17");
        Eq("negative result continues", K("3 - 5 = * 2 =").DisplayText, "-4");
        Eq("operators are ignored while an error is shown", K("5 / 0 = +").Buffer, "5÷0");
        Eq("digit after an error starts again", K("5 / 0 = 8").DisplayText, "8");
        Eq("close paren after a result does nothing", K("2 + 3 = )").Buffer, "5");
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
        AfterEquals();
        Backspace();
        Negation();
        AngleModes();
        CalculatedEvent();
        RecallFromHistory();
        Clipboard();
        CurrentValue();
        return failed;
    }
}
