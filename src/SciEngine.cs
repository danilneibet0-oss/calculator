using System;

namespace Calculator
{
    /// <summary>Scientific-mode input: builds an expression as text and evaluates it with Expr.</summary>
    public sealed class SciEngine
    {
        const int MaxDigits = 16;   // per number

        string buffer = "";

        /// <summary>The expression exactly as typed, using "." as decimal point.</summary>
        public string Buffer { get { return buffer; } }

        public string DisplayText { get { return buffer.Length == 0 ? "0" : Show(buffer); } }

        static string Show(string s) { return s.Replace(".", CalcEngine.DecimalSeparator); }

        /// <summary>The digits and decimal point at the very end of the buffer, i.e. the number being typed.</summary>
        string TrailingNumber()
        {
            int i = buffer.Length;
            while (i > 0 && (char.IsDigit(buffer[i - 1]) || buffer[i - 1] == '.')) i--;
            return buffer.Substring(i);
        }

        public void Digit(char d)
        {
            string number = TrailingNumber();
            if (number == "0") buffer = buffer.Substring(0, buffer.Length - 1);   // "0" then "5" is "5"
            else if (number.Replace(".", "").Length >= MaxDigits) return;
            buffer += d;
        }

        public void Clear()
        {
            buffer = "";
        }
    }
}
