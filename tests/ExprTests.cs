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
        return failed;
    }
}
