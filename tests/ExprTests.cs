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

    public static int Run()
    {
        failed = 0;
        return failed;
    }
}
