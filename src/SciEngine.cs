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

        public void Decimal()
        {
            string number = TrailingNumber();
            if (number.IndexOf('.') >= 0) return;
            buffer += number.Length == 0 ? "0." : ".";
        }

        static readonly string[] BinaryOperators = { "+", "−", "×", "÷", "^", " mod " };

        /// <summary>The binary operator the buffer ends with, or null.</summary>
        string TrailingOperator()
        {
            foreach (string op in BinaryOperators)
                if (buffer.EndsWith(op, StringComparison.Ordinal)) return op;
            return null;
        }

        /// <summary>True if the buffer ends with a minus that is a sign, not a subtraction ("-" or "(-").</summary>
        bool EndsWithSign()
        {
            return buffer == "−" || buffer.EndsWith("(−", StringComparison.Ordinal);
        }

        /// <summary>Appends one of + − × ÷ ^ or " mod ". A second operator in a row replaces the first.</summary>
        public void Operator(string op)
        {
            if (buffer.Length == 0)
            {
                buffer = op == "−" ? "−" : "0" + op;
                return;
            }
            if (EndsWithSign()) return;
            if (buffer.EndsWith("(", StringComparison.Ordinal))
            {
                if (op == "−") buffer += op;
                return;
            }
            string last = TrailingOperator();
            if (last != null) buffer = buffer.Substring(0, buffer.Length - last.Length);
            buffer += op;
        }

        /// <summary>True if the buffer ends with something a postfix sign like "!" can apply to.</summary>
        bool EndsWithOperand()
        {
            if (buffer.Length == 0) return false;
            char c = buffer[buffer.Length - 1];
            return char.IsDigit(c) || c == ')' || c == 'π' || c == 'e' || c == '!';
        }

        /// <summary>Appends the constant "π" or "e".</summary>
        public void Constant(string name)
        {
            if (name != "π" && name != "e") throw new ArgumentException("unknown constant " + name);
            buffer += name;
        }

        public void Factorial()
        {
            if (EndsWithOperand()) buffer += "!";
        }

        /// <summary>Number of "(" still waiting for their ")".</summary>
        int OpenCount()
        {
            int n = 0;
            foreach (char c in buffer)
            {
                if (c == '(') n++;
                else if (c == ')') n--;
            }
            return n;
        }

        public void OpenParen()
        {
            buffer += "(";
        }

        /// <summary>Closes a group, but only if there is one open and it has something inside.</summary>
        public void CloseParen()
        {
            if (OpenCount() <= 0) return;
            if (buffer.EndsWith("(", StringComparison.Ordinal) || TrailingOperator() != null || EndsWithSign()) return;
            buffer += ")";
        }

        public void Clear()
        {
            buffer = "";
        }
    }
}
