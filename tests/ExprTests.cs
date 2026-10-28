using System;
using Calculator;

/// <summary>Tests for the scientific-mode expression evaluator.</summary>
static class ExprTests
{
    static int failed;

    static void Eq(string name, double actual, double expected)
    {
        if (Math.Abs(actual - expected) <= 1e-9 * Math.Max(1.0, Math.Abs(expected))) return;
        failed++;
        Console.WriteLine("FAIL {0}: expected {1}, got {2}", name, expected, actual);
    }

    static double E(string text) { return Expr.Evaluate(text, AngleMode.Degrees); }

    /// <summary>Expects the expression to fail with exactly this message.</summary>
    static void Err(string name, string text, string message)
    {
        try
        {
            double v = E(text);
            failed++;
            Console.WriteLine("FAIL {0}: expected error '{1}', got {2}", name, message, v);
        }
        catch (ExprException ex)
        {
            if (ex.Message == message) return;
            failed++;
            Console.WriteLine("FAIL {0}: expected error '{1}', got '{2}'", name, message, ex.Message);
        }
    }

    static void Numbers()
    {
        Eq("integer", E("42"), 42);
        Eq("decimal point", E("3.14"), 3.14);
        Eq("decimal comma", E("3,14"), 3.14);
        Eq("leading dot", E(".5"), 0.5);
        Eq("spaces around", E("  7  "), 7);
        Err("empty", "", Expr.SyntaxError);
        Err("lone dot", ".", Expr.SyntaxError);
        Err("two numbers", "1 2", Expr.SyntaxError);
        Err("letters", "abc", Expr.SyntaxError);
    }

    static void AddSubtract()
    {
        Eq("add", E("1+2"), 3);
        Eq("subtract", E("10-4"), 6);
        Eq("chain add", E("1+2+3"), 6);
        Eq("chain subtract is left-assoc", E("5-2-1"), 2);
        Eq("spaces", E("1 + 2"), 3);
        Eq("unicode minus", E("2−1"), 1);
        Eq("decimals", E("0.1+0.2"), 0.3);
        Err("dangling plus", "1+", Expr.SyntaxError);
        Err("dangling minus", "1-", Expr.SyntaxError);
    }

    static void MultiplyDivide()
    {
        Eq("multiply", E("2*3"), 6);
        Eq("times sign", E("2×3"), 6);
        Eq("divide", E("6/3"), 2);
        Eq("divide sign", E("6÷4"), 1.5);
        Eq("precedence mul first", E("2+3*4"), 14);
        Eq("precedence mul last", E("2*3+4"), 10);
        Eq("divide is left-assoc", E("8/2/2"), 2);
        Eq("subtract with mul", E("10-2*3"), 4);
        Err("dangling times", "2*", Expr.SyntaxError);
    }

    static void DivisionByZero()
    {
        Err("divide by zero", "5/0", Expr.DivideByZero);
        Err("divide sign by zero", "1÷0", Expr.DivideByZero);
        Err("zero over zero", "0/0", Expr.Undefined);
        Err("zero in the middle", "2+3/(1-1)".Replace("(1-1)", "0"), Expr.DivideByZero);
        Eq("zero numerator", E("0/5"), 0);
    }

    static void Parentheses()
    {
        Eq("group first", E("(1+2)*3"), 9);
        Eq("double parens", E("((2))"), 2);
        Eq("group on the right", E("2*(3+4)"), 14);
        Eq("two groups", E("(1+2)-(3-4)"), 4);
        Err("missing close", "(1+2", Expr.SyntaxError);
        Err("extra close", "1+2)", Expr.SyntaxError);
        Err("empty parens", "()", Expr.SyntaxError);
    }

    static void UnarySigns()
    {
        Eq("negative number", E("-5"), -5);
        Eq("unicode minus sign", E("−5"), -5);
        Eq("double negative", E("--5"), 5);
        Eq("negative operand", E("2*-3"), -6);
        Eq("negate group", E("-(2+3)"), -5);
        Eq("unary plus", E("+4"), 4);
        Eq("minus minus with space", E("5- -3"), 8);
        Eq("negative first factor", E("-2*3"), -6);
        Eq("subtract negative", E("2--3"), 5);
    }

    static void Powers()
    {
        Eq("square", E("2^3"), 8);
        Eq("right associative", E("2^3^2"), 512);
        Eq("minus binds looser", E("-2^2"), -4);
        Eq("negative exponent", E("2^-1"), 0.5);
        Eq("negative base in parens", E("(-2)^2"), 4);
        Eq("power before multiply", E("2*3^2"), 18);
        Eq("fractional exponent", E("4^0.5"), 2);
        Err("zero to zero", "0^0", Expr.Undefined);
        Err("zero to negative", "0^-1", Expr.DivideByZero);
        Err("even root of negative", "(-8)^(1/3)", Expr.Invalid);
        Err("huge power", "10^400", Expr.Overflow);
    }

    static void Constants()
    {
        Eq("pi symbol", E("π"), Math.PI);
        Eq("pi word", E("pi"), Math.PI);
        Eq("e", E("e"), Math.E);
        Eq("upper case", E("PI"), Math.PI);
        Eq("circle length", E("π*2"), 2 * Math.PI);
        Eq("e to the one", E("e^1"), Math.E);
        Err("unknown word", "x", Expr.SyntaxError);
        Err("unknown longer word", "foo", Expr.SyntaxError);
    }

    static double ER(string text) { return Expr.Evaluate(text, AngleMode.Radians); }
    static double EG(string text) { return Expr.Evaluate(text, AngleMode.Gradians); }

    static void TrigRadiansAndGradians()
    {
        Eq("rad sin pi/2", ER("sin(π/2)"), 1);
        Eq("rad sin pi is zero", ER("sin(π)"), 0);
        Eq("rad sin 2pi is zero", ER("sin(2π)".Replace("2π", "2*π")), 0);
        Eq("rad cos pi", ER("cos(π)"), -1);
        Eq("rad cos pi/2 is zero", ER("cos(π/2)"), 0);
        Eq("rad tan pi/4", ER("tan(π/4)"), 1);
        Eq("rad sin 1", ER("sin(1)"), 0.8414709848078965);
        Eq("rad cos 1", ER("cos(1)"), 0.5403023058681398);

        Eq("grad sin 100", EG("sin(100)"), 1);
        Eq("grad sin -100", EG("sin(-100)"), -1);
        Eq("grad cos 200", EG("cos(200)"), -1);
        Eq("grad cos 100 is exactly zero", EG("cos(100)"), 0);
        Eq("grad tan 50", EG("tan(50)"), 1);
        Eq("grad sin 50", EG("sin(50)"), 0.7071067811865476);
        Eq("grad full turn", EG("sin(400)"), 0);
        try { EG("tan(100)"); failed++; Console.WriteLine("FAIL grad tan 100 should be an error"); }
        catch (ExprException) { }

        // The same call gives different answers per mode.
        Eq("deg sin 1", E("sin(1)"), 0.01745240643728351);
    }

    static void ImplicitMultiplication()
    {
        Eq("number and pi", E("2π"), 2 * Math.PI);
        Eq("number and group", E("2(3+4)"), 14);
        Eq("two groups", E("(1+2)(3+4)"), 21);
        Eq("number and function", E("2sin(30)"), 1);
        Eq("number and e", E("3e"), 3 * Math.E);
        Eq("number and root sign", E("2√9"), 6);
        Eq("pi squared", E("ππ"), Math.PI * Math.PI);
        Eq("power then group", E("2^2(3)"), 12);
        Eq("group then power", E("2(3)^2"), 18);
        Eq("factorial then group", E("5!(2)"), 240);
        Eq("three groups", E("(2)(3)(4)"), 24);
        Err("space blocks it", "π π", Expr.SyntaxError);
        Err("space before group", "2 (3)", Expr.SyntaxError);
        Err("glued unknown word", "2modx", Expr.SyntaxError);
    }

    static void Logarithms()
    {
        Eq("ln e", E("ln(e)"), 1);
        Eq("ln 1", E("ln(1)"), 0);
        Eq("ln 10", E("ln(10)"), 2.302585092994046);
        Eq("log 100", E("log(100)"), 2);
        Eq("log 1000", E("log(1000)"), 3);
        Eq("log 0.001", E("log(0.001)"), -3);
        Eq("exp 0", E("exp(0)"), 1);
        Eq("exp 1", E("exp(1)"), Math.E);
        Eq("ln of exp", E("ln(exp(5))"), 5);
        Eq("log of power of ten", E("log(10^7)"), 7);
        Err("ln zero", "ln(0)", Expr.Invalid);
        Err("ln negative", "ln(-1)", Expr.Invalid);
        Err("log zero", "log(0)", Expr.Invalid);
        Err("log negative", "log(-5)", Expr.Invalid);
        Err("exp overflow", "exp(1000)", Expr.Overflow);
    }

    static void InverseTrig()
    {
        Eq("asin 0.5", E("asin(0.5)"), 30);
        Eq("asin 1", E("asin(1)"), 90);
        Eq("asin -1", E("asin(-1)"), -90);
        Eq("acos 0.5", E("acos(0.5)"), 60);
        Eq("acos -1", E("acos(-1)"), 180);
        Eq("acos 1", E("acos(1)"), 0);
        Eq("atan 1", E("atan(1)"), 45);
        Eq("atan 0", E("atan(0)"), 0);
        Eq("atan of big number", E("atan(1000000000)"), 89.99999994270423);
        Eq("round trip", E("sin(asin(0.3))"), 0.3);
        Eq("rad asin 1", ER("asin(1)"), Math.PI / 2);
        Eq("rad atan 1", ER("atan(1)"), Math.PI / 4);
        Eq("grad atan 1", EG("atan(1)"), 50);
        Eq("grad asin 1", EG("asin(1)"), 100);
        Eq("grad acos 0", EG("acos(0)"), 100);
        Err("asin out of range", "asin(2)", Expr.Invalid);
        Err("acos out of range", "acos(-2)", Expr.Invalid);
    }

    static void TrigDegrees()
    {
        Eq("sin 30", E("sin(30)"), 0.5);
        Eq("sin 90", E("sin(90)"), 1);
        Eq("sin 180 is exactly zero", E("sin(180)"), 0);
        Eq("sin 270", E("sin(270)"), -1);
        Eq("sin 360", E("sin(360)"), 0);
        Eq("sin -90", E("sin(-90)"), -1);
        Eq("sin 450", E("sin(450)"), 1);
        Eq("cos 0", E("cos(0)"), 1);
        Eq("cos 60", E("cos(60)"), 0.5);
        Eq("cos 90 is exactly zero", E("cos(90)"), 0);
        Eq("cos 180", E("cos(180)"), -1);
        Eq("tan 45", E("tan(45)"), 1);
        Eq("tan 0", E("tan(0)"), 0);
        Eq("tan 180", E("tan(180)"), 0);
        Eq("pythagoras", E("sin(30)^2+cos(30)^2"), 1);
        Eq("argument expression", E("sin(15+15)"), 0.5);
        Err("tan 90", "tan(90)", Expr.Invalid);
        Err("tan 270", "tan(270)", Expr.Invalid);
    }

    static void Roots()
    {
        Eq("sqrt function", E("sqrt(16)"), 4);
        Eq("sqrt sign", E("√9"), 3);
        Eq("sqrt sign with parens", E("√(16)"), 4);
        Eq("sqrt twice", E("sqrt(2)*sqrt(2)"), 2);
        Eq("sqrt of expression", E("sqrt(3+6)"), 3);
        Eq("cbrt function", E("cbrt(27)"), 3);
        Eq("cbrt sign", E("∛8"), 2);
        Eq("cbrt negative", E("cbrt(-8)"), -2);
        Eq("abs", E("abs(-5)"), 5);
        Eq("abs of expression", E("abs(3-10)"), 7);
        Err("sqrt of negative", "sqrt(-1)", Expr.Invalid);
        Err("function without parens", "sqrt", Expr.SyntaxError);
        Err("unclosed function", "sqrt(4", Expr.SyntaxError);
        Err("unknown function", "foo(1)", Expr.SyntaxError);
    }

    static void Modulo()
    {
        Eq("basic", E("10 mod 3"), 1);
        Eq("no spaces", E("10mod4"), 2);
        Eq("upper case", E("7 MOD 2"), 1);
        Eq("binds like multiply", E("2+7 mod 4"), 5);
        Eq("sign of dividend", E("-7 mod 3"), -1);
        Eq("decimals", E("5.5 mod 2"), 1.5);
        Err("by zero", "5 mod 0", Expr.DivideByZero);
        Err("missing operand", "5 mod", Expr.SyntaxError);
    }

    static void Percent()
    {
        Eq("half", E("50%"), 0.5);
        Eq("percent of a number", E("200*10%"), 20);
        Eq("twice", E("50%%"), 0.005);
        Eq("of a group", E("(10+10)%"), 0.2);
        Eq("added", E("200+10%"), 200.1);
    }

    static void Factorials()
    {
        Eq("three", E("3!"), 6);
        Eq("zero", E("0!"), 1);
        Eq("five", E("5!"), 120);
        Eq("repeated", E("3!!"), 720);
        Eq("minus applies after", E("-3!"), -6);
        Eq("in exponent", E("2^3!"), 64);
        Eq("on the base", E("2!^2"), 4);
        Eq("largest finite", E("170!"), 7.257415615307994e306);
        Err("too large", "171!", Expr.Overflow);
        Err("negative", "(-1)!", Expr.Invalid);
        Err("fraction", "2.5!", Expr.Invalid);
    }

    public static int Run()
    {
        failed = 0;
        Numbers();
        AddSubtract();
        MultiplyDivide();
        DivisionByZero();
        Parentheses();
        UnarySigns();
        Powers();
        Constants();
        Factorials();
        Percent();
        Modulo();
        Roots();
        TrigDegrees();
        TrigRadiansAndGradians();
        InverseTrig();
        Logarithms();
        ImplicitMultiplication();
        return failed;
    }
}
