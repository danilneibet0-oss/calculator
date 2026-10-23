using System;
using Calculator;

static class EngineTests
{
    static int failed;

    static void Check(string name, string actual, string expected)
    {
        if (actual == expected) return;
        failed++;
        Console.WriteLine("FAIL {0}: expected '{1}', got '{2}'", name, expected, actual);
    }

    static string Run(string keys)
    {
        var e = new CalcEngine();
        foreach (char k in keys)
        {
            if (char.IsDigit(k)) e.Digit(k);
            else switch (k)
            {
                case '.': e.Decimal(); break;
                case '+': case '-': case '*': case '/': e.Operator(k); break;
                case '=': e.Evaluate(); break;
                case 'n': e.Negate(); break;
                case '%': e.Percent(); break;
                case 'r': e.Sqrt(); break;
                case 's': e.Square(); break;
                case 'i': e.Reciprocal(); break;
                case 'b': e.Backspace(); break;
                case 'e': e.ClearEntry(); break;
                case 'c': e.Clear(); break;
            }
        }
        return e.DisplayText;
    }

    static int Main()
    {
        CalcEngine.DecimalSeparator = ",";
        Check("add", Run("2+3="), "5");
        Check("chain", Run("2+3*4="), "20");
        Check("decimal exact", Run("0.1+0.2="), "0,3");
        Check("repeat equals", Run("2+3=="), "8");
        Check("op then equals", Run("5+="), "10");
        Check("divide", Run("1/3="), "0,333333333333333");
        Check("div zero", Run("5/0="), "Деление на ноль невозможно");
        Check("zero div zero", Run("0/0="), "Результат не определён");
        Check("sqrt", Run("9r"), "3");
        Check("sqrt negative", Run("9nr"), "Недопустимый ввод");
        Check("square", Run("12s"), "144");
        Check("reciprocal", Run("4i"), "0,25");
        Check("negate", Run("5n"), "-5");
        Check("percent", Run("200+10%="), "220");
        Check("backspace", Run("123b"), "12");
        Check("backspace to zero", Run("1b"), "0");
        Check("CE", Run("5+7e3="), "8");
        Check("grouping", Run("1234567"), "1 234 567");
        Check("trailing dot", Run("5."), "5,");
        Check("digit limit", Run("12345678901234567890"), "1 234 567 890 123 456");
        Check("new calc after equals", Run("2+3=7"), "7");
        Check("recover from error", Run("5/0=7"), "7");
        Check("subtract negative", Run("3-5="), "-2");
        Check("ops change", Run("3+*4="), "12");
        Console.WriteLine(failed == 0 ? "All tests passed" : failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
