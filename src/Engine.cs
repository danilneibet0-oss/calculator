using System;
using System.Globalization;
using System.Text;

namespace Calculator
{
    /// <summary>One finished calculation shown in the history panel.</summary>
    public sealed class HistoryItem
    {
        public string Expression;
        public string Result;   // invariant representation, e.g. "-1234.5"
        public string ResultText { get { return CalcEngine.Group(Result); } }
    }

    /// <summary>Calculator logic without any UI. Numbers are kept as decimal for exact results.</summary>
    public sealed class CalcEngine
    {
        public static string DecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

        const int MaxDigits = 16;

        string entry = "0";          // invariant representation of the number being typed / shown
        string expression = "";
        string errorText;
        decimal? left;
        char op;
        bool newEntry = true;        // next digit starts a fresh number
        bool afterEquals;
        char lastOp;
        decimal lastRhs;

        const int MaxHistory = 100;
        readonly System.Collections.Generic.List<HistoryItem> history = new System.Collections.Generic.List<HistoryItem>();

        /// <summary>Finished calculations, newest first.</summary>
        public System.Collections.Generic.IList<HistoryItem> History { get { return history; } }
        /// <summary>Changes whenever History changes, so the UI knows when to redraw it.</summary>
        public int HistoryVersion { get; private set; }

        public void ClearHistory()
        {
            history.Clear();
            HistoryVersion++;
        }

        /// <summary>Puts a past result back on the display, ready to be used in a new calculation.</summary>
        public void Recall(HistoryItem item)
        {
            // Entries from the scientific mode may be in exponent form ("1e+20") or too large for decimal.
            decimal value;
            bool fits = decimal.TryParse(item.Result, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            errorText = null;
            left = null;
            op = '\0';
            lastOp = '\0';
            expression = item.Expression;
            newEntry = true;
            afterEquals = true;
            if (fits) entry = Format(value);
            else SetError("Переполнение");
        }

        void AddHistory(string expr, string result)
        {
            history.Insert(0, new HistoryItem { Expression = expr, Result = result });
            if (history.Count > MaxHistory) history.RemoveAt(history.Count - 1);
            HistoryVersion++;
        }

        public string Expression { get { return expression; } }
        public bool HasError { get { return errorText != null; } }
        public string DisplayText { get { return errorText ?? Group(entry); } }

        public void Digit(char d)
        {
            StartEntry();
            if (entry.Replace("-", "").Replace(".", "").Length >= MaxDigits) return;
            entry = entry == "0" ? d.ToString() : entry + d;
        }

        public void Decimal()
        {
            StartEntry();
            if (entry.IndexOf('.') < 0) entry += ".";
        }

        public void Backspace()
        {
            if (HasError || newEntry) return;
            entry = entry.Substring(0, entry.Length - 1);
            if (entry == "" || entry == "-") entry = "0";
        }

        public void ClearEntry()
        {
            if (HasError || afterEquals) { Clear(); return; }
            entry = "0";
            newEntry = false;
        }

        public void Clear()
        {
            entry = "0";
            expression = "";
            errorText = null;
            left = null;
            op = '\0';
            lastOp = '\0';
            newEntry = true;
            afterEquals = false;
        }

        public void Negate()
        {
            if (HasError) return;
            if (entry == "0") return;
            entry = entry.StartsWith("-") ? entry.Substring(1) : "-" + entry;
        }

        public void Operator(char c)
        {
            if (HasError) return;
            if (op != '\0' && !newEntry)
            {
                decimal res;
                if (!Apply(left.Value, op, Current(), out res)) return;
                left = res;
                entry = Format(res);
            }
            else if (op == '\0')
            {
                left = Current();
            }
            op = c;
            expression = Num(left.Value) + " " + Symbol(c);
            newEntry = true;
            afterEquals = false;
        }

        public void Evaluate()
        {
            if (HasError) return;
            decimal res;
            if (op != '\0')
            {
                decimal rhs = Current();
                expression = Num(left.Value) + " " + Symbol(op) + " " + Num(rhs) + " =";
                if (!Apply(left.Value, op, rhs, out res)) return;
                lastOp = op;
                lastRhs = rhs;
                op = '\0';
                left = null;
                AddHistory(expression, Format(res));
            }
            else if (afterEquals && lastOp != '\0')
            {
                decimal cur = Current();
                expression = Num(cur) + " " + Symbol(lastOp) + " " + Num(lastRhs) + " =";
                if (!Apply(cur, lastOp, lastRhs, out res)) return;
                AddHistory(expression, Format(res));
            }
            else
            {
                expression = Num(Current()) + " =";
                newEntry = true;
                afterEquals = true;
                return;
            }
            entry = Format(res);
            newEntry = true;
            afterEquals = true;
        }

        public void Percent()
        {
            if (HasError) return;
            decimal res = 0;
            if (op != '\0')
            {
                try { res = left.Value * Current() / 100m; }
                catch (OverflowException) { SetError("Переполнение"); return; }
                expression = Num(left.Value) + " " + Symbol(op) + " " + Num(res);
            }
            else
            {
                expression = "0";
            }
            entry = Format(res);
            newEntry = true;
            afterEquals = false;
        }

        public void Sqrt()
        {
            if (HasError) return;
            decimal x = Current();
            if (x < 0) { SetError("Недопустимый ввод"); return; }
            Unary("√(" + Num(x) + ")", (decimal)Math.Sqrt((double)x));
        }

        public void Square()
        {
            if (HasError) return;
            decimal x = Current();
            try { Unary("sqr(" + Num(x) + ")", x * x); }
            catch (OverflowException) { SetError("Переполнение"); }
        }

        public void Reciprocal()
        {
            if (HasError) return;
            decimal x = Current();
            if (x == 0) { SetError("Деление на ноль невозможно"); return; }
            Unary("1/(" + Num(x) + ")", 1m / x);
        }

        // ---- Clipboard ----

        /// <summary>The shown number without thousands separators, ready for the clipboard. Empty on error.</summary>
        public string CopyText
        {
            get { return HasError ? "" : entry.Replace(".", DecimalSeparator); }
        }

        /// <summary>Parses pasted text such as "1 234,5", "1,234.56" or "-7". Returns false for anything else.</summary>
        public static bool TryParseNumber(string text, out decimal value)
        {
            value = 0;
            if (text == null) return false;
            var sb = new StringBuilder();
            foreach (char c in text.Trim())
                if (c != ' ' && c != ' ' && c != ' ' && c != ' ') sb.Append(c);
            string s = sb.ToString();
            if (s.Length == 0) return false;

            int dots = s.Split('.').Length - 1, commas = s.Split(',').Length - 1;
            if (commas > 1 && dots == 0) s = s.Replace(",", "");      // 1,234,567
            else if (dots > 1 && commas == 0) s = s.Replace(".", ""); // 1.234.567
            int dec = Math.Max(s.LastIndexOf('.'), s.LastIndexOf(','));
            if (dec >= 0)
                s = s.Substring(0, dec).Replace(".", "").Replace(",", "") + "." + s.Substring(dec + 1);

            return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Replaces the number being typed with pasted text. Returns false if the text is not a number.</summary>
        public bool Paste(string text)
        {
            decimal v;
            if (!TryParseNumber(text, out v)) return false;
            StartEntry();
            entry = Format(v);
            newEntry = false;
            return true;
        }

        public void RemoveHistory(HistoryItem item)
        {
            if (history.Remove(item)) HistoryVersion++;
        }

        // ---- Memory (MC, MR, M+, M-, MS) ----

        decimal? memory;

        public bool HasMemory { get { return memory.HasValue; } }

        public void MemoryClear() { memory = null; }

        public void MemoryStore()
        {
            if (HasError) return;
            memory = Current();
            newEntry = true;
        }

        public void MemoryAdd() { MemoryChange(1); }

        public void MemorySubtract() { MemoryChange(-1); }

        void MemoryChange(int sign)
        {
            if (HasError) return;
            try { memory = (memory ?? 0m) + sign * Current(); }
            catch (OverflowException) { SetError("Переполнение"); return; }
            newEntry = true;
        }

        public void MemoryRecall()
        {
            if (!memory.HasValue) return;
            if (HasError) Clear();
            entry = Format(memory.Value);
            if (op == '\0')
            {
                expression = "";
                lastOp = '\0';
                afterEquals = false;
            }
            newEntry = true;
        }

        void Unary(string text, decimal result)
        {
            expression = (op != '\0' ? Num(left.Value) + " " + Symbol(op) + " " : "") + text;
            entry = Format(result);
            newEntry = true;
            afterEquals = false;
        }

        void StartEntry()
        {
            if (HasError) Clear();
            if (newEntry)
            {
                entry = "0";
                newEntry = false;
                if (op == '\0')
                {
                    expression = "";
                    lastOp = '\0';
                    afterEquals = false;
                }
            }
        }

        decimal Current()
        {
            return decimal.Parse(entry, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }

        bool Apply(decimal a, char o, decimal b, out decimal result)
        {
            result = 0;
            try
            {
                switch (o)
                {
                    case '+': result = a + b; break;
                    case '-': result = a - b; break;
                    case '*': result = a * b; break;
                    case '/':
                        if (b == 0)
                        {
                            SetError(a == 0 ? "Результат не определён" : "Деление на ноль невозможно");
                            return false;
                        }
                        result = a / b;
                        break;
                }
                return true;
            }
            catch (OverflowException)
            {
                SetError("Переполнение");
                return false;
            }
        }

        void SetError(string text)
        {
            errorText = text;
            left = null;
            op = '\0';
            lastOp = '\0';
            newEntry = true;
            afterEquals = false;
        }

        static string Symbol(char c)
        {
            switch (c)
            {
                case '*': return "×";
                case '/': return "÷";
                case '-': return "−";
                default: return c.ToString();
            }
        }

        static string Num(decimal v) { return Group(Format(v)); }

        /// <summary>Rounds to 15 significant digits and returns an invariant string without trailing zeros.</summary>
        public static string Format(decimal v)
        {
            if (v == 0) return "0";
            int intDigits = (int)Math.Floor(Math.Log10((double)Math.Abs(v))) + 1;
            int dec = Math.Min(28, Math.Max(0, 15 - intDigits));
            v = Math.Round(v, dec, MidpointRounding.AwayFromZero);
            v = v / 1.0000000000000000000000000000m;
            string s = v.ToString(CultureInfo.InvariantCulture);
            return s == "-0" ? "0" : s;
        }

        /// <summary>Adds thousands separators and the local decimal separator to an invariant number string.</summary>
        public static string Group(string s)
        {
            // Results of the scientific mode can look like "1.5e-7"; they are not grouped.
            if (s.IndexOf('e') >= 0) return s.Replace(".", DecimalSeparator);

            string sign = "";
            if (s.StartsWith("-")) { sign = "-"; s = s.Substring(1); }
            string frac = "";
            int dot = s.IndexOf('.');
            if (dot >= 0) { frac = DecimalSeparator + s.Substring(dot + 1); s = s.Substring(0, dot); }
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && (s.Length - i) % 3 == 0) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sign + sb + frac;
        }
    }
}
