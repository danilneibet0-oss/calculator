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

    static string Parsed(string text)
    {
        decimal v;
        return CalcEngine.TryParseNumber(text, out v) ? CalcEngine.Format(v) : "none";
    }

    static void ClipboardTests()
    {
        Check("parse plain", Parsed("123"), "123");
        Check("parse spaces", Parsed("  1 234,5  "), "1234.5");
        Check("parse nbsp", Parsed("1 234"), "1234");
        Check("parse comma decimal", Parsed("3,14"), "3.14");
        Check("parse dot decimal", Parsed("3.14"), "3.14");
        Check("parse us thousands", Parsed("1,234.56"), "1234.56");
        Check("parse eu thousands", Parsed("1.234,56"), "1234.56");
        Check("parse many commas", Parsed("1,234,567"), "1234567");
        Check("parse many dots", Parsed("1.234.567"), "1234567");
        Check("parse negative", Parsed("-7"), "-7");
        Check("parse plus", Parsed("+7"), "7");
        Check("parse leading dot", Parsed(".5"), "0.5");
        Check("parse text rejected", Parsed("abc"), "none");
        Check("parse mixed rejected", Parsed("12abc"), "none");
        Check("parse exponent rejected", Parsed("1e5"), "none");
        Check("parse empty rejected", Parsed("   "), "none");
        Check("parse null rejected", Parsed(null), "none");
        Check("parse lone separator rejected", Parsed(","), "none");
        Check("parse too big rejected", Parsed(new string('9', 40)), "none");

        var e = new CalcEngine();
        Check("paste ok", e.Paste("1 234,5").ToString(), "True");
        Check("paste shows number", e.DisplayText, "1 234,5");
        e.Digit('6');
        Check("digit appends to pasted number", e.DisplayText, "1 234,56");
        Check("paste bad text", e.Paste("hello").ToString(), "False");
        Check("bad paste keeps display", e.DisplayText, "1 234,56");

        e.Clear(); e.Digit('5'); e.Operator('+'); e.Paste("3"); e.Evaluate();
        Check("paste as operand", e.DisplayText, "8");
        e.Paste("9");
        Check("paste after equals starts new calc", e.DisplayText, "9");
        Check("paste after equals clears expression", e.Expression, "");

        e.Clear(); e.Digit('5'); e.Operator('/'); e.Digit('0'); e.Evaluate();
        Check("copy text empty on error", e.CopyText, "");
        Check("paste recovers from error", e.Paste("4").ToString(), "True");
        Check("paste after error", e.DisplayText, "4");

        e.Clear(); e.Digit('1'); e.Digit('2'); e.Digit('3'); e.Digit('4'); e.Decimal(); e.Digit('5');
        Check("copy text ungrouped", e.CopyText, "1234,5");
        e.Negate();
        Check("copy text negative", e.CopyText, "-1234,5");

        e = new CalcEngine();
        e.Digit('2'); e.Operator('+'); e.Digit('3'); e.Evaluate();
        e.Digit('4'); e.Operator('*'); e.Digit('5'); e.Evaluate();
        Check("history before removal", e.History.Count.ToString(), "2");
        HistoryItem older = e.History[1];
        e.RemoveHistory(older);
        Check("history item removed", e.History.Count.ToString(), "1");
        Check("remaining history item", e.History[0].Result, "20");
        int v = e.HistoryVersion;
        e.RemoveHistory(older);
        Check("removing twice is harmless", e.HistoryVersion.ToString(), v.ToString());
    }

    static void SettingsTests()
    {
        var s = Settings.Parse(null);
        Check("settings default bounds", s.HasBounds.ToString(), "False");
        Check("settings default topmost", s.TopMost.ToString(), "False");

        var src = new Settings { HasBounds = true, Left = -1200, Top = 80, Width = 340, Height = 520, TopMost = true, HistoryOpen = true };
        var back = Settings.Parse(src.Serialize());
        Check("settings roundtrip bounds", back.HasBounds + " " + back.Left + " " + back.Top + " " + back.Width + " " + back.Height,
              "True -1200 80 340 520");
        Check("settings roundtrip topmost", back.TopMost.ToString(), "True");
        Check("settings roundtrip history", back.HistoryOpen.ToString(), "True");

        var noBounds = Settings.Parse(new Settings { TopMost = true }.Serialize());
        Check("settings without bounds", noBounds.HasBounds.ToString(), "False");
        Check("settings topmost kept", noBounds.TopMost.ToString(), "True");

        Check("settings garbage ignored", Settings.Parse("left=abc\r\nwidth=\r\n???\r\ntopmost=1\r\n").TopMost.ToString(), "True");
        Check("settings tiny window rejected", Settings.Parse("left=0\ntop=0\nwidth=10\nheight=10\n").HasBounds.ToString(), "False");
        Check("settings huge window rejected", Settings.Parse("left=0\ntop=0\nwidth=99999\nheight=500\n").HasBounds.ToString(), "False");
        Check("settings partial bounds rejected", Settings.Parse("left=0\ntop=0\nwidth=300\n").HasBounds.ToString(), "False");
        Check("settings CRLF", Settings.Parse("left=5\r\ntop=6\r\nwidth=300\r\nheight=400\r\n").Top.ToString(), "6");
    }

    static void MemoryTests()
    {
        var e = new CalcEngine();
        Check("memory initially empty", e.HasMemory.ToString(), "False");
        e.MemoryRecall();
        Check("recall empty memory does nothing", e.DisplayText, "0");

        e.Digit('5'); e.MemoryStore();
        Check("MS sets memory", e.HasMemory.ToString(), "True");
        Check("MS keeps display", e.DisplayText, "5");

        e.Digit('3'); // starts a new number after MS
        Check("digit after MS starts new number", e.DisplayText, "3");
        e.MemoryAdd();
        e.Clear();
        e.MemoryRecall();
        Check("M+ adds", e.DisplayText, "8");

        e.Digit('2'); e.MemorySubtract();
        e.Clear();
        e.MemoryRecall();
        Check("M- subtracts", e.DisplayText, "6");

        // recall as the right-hand operand: 10 + MR = 16
        e.Clear();
        e.Digit('1'); e.Digit('0'); e.Operator('+'); e.MemoryRecall(); e.Evaluate();
        Check("MR as operand", e.DisplayText, "16");

        // recall then continue with an operator
        e.Clear(); e.MemoryRecall(); e.Operator('*'); e.Digit('2'); e.Evaluate();
        Check("MR then operator", e.DisplayText, "12");

        e.Clear();
        Check("memory survives C", e.HasMemory.ToString(), "True");
        e.MemoryClear();
        Check("MC clears", e.HasMemory.ToString(), "False");

        e.Digit('4'); e.MemoryAdd(); // M+ with empty memory starts from zero
        e.Clear(); e.MemoryRecall();
        Check("M+ on empty memory", e.DisplayText, "4");

        e.Clear(); e.Digit('5'); e.Operator('/'); e.Digit('0'); e.Evaluate();
        e.MemoryStore();
        e.Clear(); e.MemoryRecall();
        Check("MS ignored while in error", e.DisplayText, "4");
        e.Digit('1'); e.Operator('/'); e.Digit('0'); e.Evaluate();
        e.MemoryRecall();
        Check("MR recovers from error", e.DisplayText, "4");
    }

    static void HistoryTests()
    {
        var e = new CalcEngine();
        Check("history empty", e.History.Count.ToString(), "0");

        e.Digit('2'); e.Operator('+'); e.Digit('3'); e.Evaluate();
        Check("history count", e.History.Count.ToString(), "1");
        Check("history expression", e.History[0].Expression, "2 + 3 =");
        Check("history result", e.History[0].Result, "5");

        e.Evaluate(); // repeat last operation: 5 + 3
        Check("history repeat", e.History[0].Result, "8");
        Check("history newest first", e.History[1].Result, "5");

        int v = e.HistoryVersion;
        e.Digit('4'); e.Square();
        Check("unary not recorded", e.History.Count.ToString(), "2");
        Check("version unchanged", e.HistoryVersion.ToString(), v.ToString());

        e.Clear();
        e.Digit('5'); e.Operator('/'); e.Digit('0'); e.Evaluate();
        Check("error not recorded", e.History.Count.ToString(), "2");

        e.Clear();
        e.Recall(e.History[1]); // "2 + 3 =" -> 5
        Check("recall display", e.DisplayText, "5");
        Check("recall expression", e.Expression, "2 + 3 =");
        e.Operator('*'); e.Digit('2'); e.Evaluate();
        Check("recall then continue", e.DisplayText, "10");
        e.Recall(e.History[e.History.Count - 1]);
        e.Digit('7');
        Check("digit after recall starts new number", e.DisplayText, "7");

        e.ClearHistory();
        Check("history cleared", e.History.Count.ToString(), "0");

        for (int i = 0; i < 150; i++) { e.Clear(); e.Digit('1'); e.Operator('+'); e.Digit('1'); e.Evaluate(); }
        Check("history capped", e.History.Count.ToString(), "100");
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
        HistoryTests();
        MemoryTests();
        SettingsTests();
        ClipboardTests();
        failed += ExprTests.Run();
        Console.WriteLine(failed == 0 ? "All tests passed" : failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
