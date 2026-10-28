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

    public static int Run()
    {
        failed = 0;
        Numbers();
        AddSubtract();
        return failed;
    }
}
