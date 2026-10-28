using System;

namespace Calculator
{
    /// <summary>Scientific-mode input: builds an expression as text and evaluates it with Expr.</summary>
    public sealed class SciEngine
    {
        const int MaxDigits = 16;   // per number

        string buffer = "";
        string expression = "";     // the line above the result, e.g. "1+2 ="
        string resultText;          // formatted result while afterEquals
        string errorText;
        bool afterEquals;

        public AngleMode Angle = AngleMode.Degrees;

        /// <summary>Raised after every successful evaluation with the expression line and the invariant result.</summary>
        public event Action<string, string> Calculated;

        /// <summary>Switches DEG, RAD, GRAD in turn.</summary>
        public void CycleAngle()
        {
            Angle = Angle == AngleMode.Degrees ? AngleMode.Radians
                  : Angle == AngleMode.Radians ? AngleMode.Gradians
                  : AngleMode.Degrees;
        }

        public string AngleLabel
        {
            get
            {
                switch (Angle)
                {
                    case AngleMode.Degrees: return "DEG";
                    case AngleMode.Radians: return "RAD";
                    default: return "GRAD";
                }
            }
        }

        /// <summary>The expression exactly as typed, using "." as decimal point.</summary>
        public string Buffer { get { return buffer; } }

        /// <summary>The finished expression shown above the result.</summary>
        public string Expression { get { return Show(expression); } }

        public bool HasError { get { return errorText != null; } }

        public string DisplayText
        {
            get
            {
                if (errorText != null) return errorText;
                if (afterEquals) return CalcEngine.Group(resultText);
                return buffer.Length == 0 ? "0" : Show(buffer);
            }
        }

        static string Show(string s) { return s.Replace(".", CalcEngine.DecimalSeparator); }

        /// <summary>The digits and decimal point at the very end of the buffer, i.e. the number being typed.</summary>
        string TrailingNumber()
        {
            int i = buffer.Length;
            while (i > 0 && (char.IsDigit(buffer[i - 1]) || buffer[i - 1] == '.')) i--;
            return buffer.Substring(i);
        }

        /// <summary>Call before typing something that starts a new expression (digit, constant, function, "(").</summary>
        void Begin()
        {
            if (afterEquals || HasError) Clear();
        }

        /// <summary>Call before typing something that continues from the shown result (operator, "!", "x²").
        /// Returns false while an error is shown.</summary>
        bool Continue()
        {
            if (HasError) return false;
            if (afterEquals)
            {
                afterEquals = false;
                expression = "";
            }
            return true;
        }

        public void Digit(char d)
        {
            Begin();
            string number = TrailingNumber();
            if (number == "0") buffer = buffer.Substring(0, buffer.Length - 1);   // "0" then "5" is "5"
            else if (number.Replace(".", "").Length >= MaxDigits) return;
            buffer += d;
        }

        public void Decimal()
        {
            Begin();
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
            if (!Continue()) return;
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
            Begin();
            buffer += name;
        }

        public void Factorial()
        {
            if (Continue() && EndsWithOperand()) buffer += "!";
        }

        static readonly string[] FunctionNames =
            { "sin", "cos", "tan", "asin", "acos", "atan", "ln", "log", "exp", "abs", "√", "∛" };

        /// <summary>Starts a function call such as "sin(".</summary>
        public void Function(string name)
        {
            if (Array.IndexOf(FunctionNames, name) < 0) throw new ArgumentException("unknown function " + name);
            Begin();
            buffer += name + "(";
        }

        /// <summary>x²: raises what was just entered to the power 2.</summary>
        public void Square()
        {
            if (Continue() && EndsWithOperand()) buffer += "^2";
        }

        /// <summary>1/x: wraps the number being typed, or opens "1÷(" on an empty spot.</summary>
        public void Reciprocal()
        {
            if (!Continue()) return;
            string number = TrailingNumber();
            if (number.Length > 0)
                buffer = buffer.Substring(0, buffer.Length - number.Length) + "1÷(" + number + ")";
            else if (!EndsWithOperand())
                buffer += "1÷(";
        }

        /// <summary>10ˣ: opens "10^(", multiplying with a number that was just entered.</summary>
        public void TenPower()
        {
            if (!Continue()) return;
            buffer += EndsWithOperand() ? "×10^(" : "10^(";
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
            Begin();
            buffer += "(";
        }

        /// <summary>Closes a group, but only if there is one open and it has something inside.</summary>
        public void CloseParen()
        {
            if (!Continue() || OpenCount() <= 0) return;
            if (buffer.EndsWith("(", StringComparison.Ordinal) || TrailingOperator() != null || EndsWithSign()) return;
            buffer += ")";
        }

        /// <summary>Evaluates the buffer. A trailing operator is dropped and open parentheses are closed.</summary>
        public void Evaluate()
        {
            if (HasError || afterEquals || buffer.Length == 0) return;
            string text = buffer;
            string trailing = TrailingOperator();
            if (trailing != null) text = text.Substring(0, text.Length - trailing.Length);
            text += new string(')', Math.Max(0, OpenCount()));

            expression = text + " =";
            try
            {
                resultText = Expr.Format(Expr.Evaluate(text, Angle));
            }
            catch (ExprException ex)
            {
                errorText = ex.Message;
                return;
            }
            buffer = ToBuffer(resultText);
            afterEquals = true;
            if (Calculated != null) Calculated(Expression, resultText);
        }

        /// <summary>Turns a formatted result into text that can be typed on: "1.5e-7" becomes "1.5×10^(−7)".</summary>
        static string ToBuffer(string formatted)
        {
            int e = formatted.IndexOf('e');
            if (e >= 0) formatted = formatted.Substring(0, e) + "×10^(" + formatted.Substring(e + 1).TrimStart('+') + ")";
            return formatted.Replace('-', '−');
        }

        /// <summary>True if buffer[i] is a minus used as a sign: at the start, after "(" or after another operator.</summary>
        bool IsSignAt(int i)
        {
            if (i < 0 || buffer[i] != '−') return false;
            if (i == 0) return true;
            return "(+−×÷^ ".IndexOf(buffer[i - 1]) >= 0;
        }

        /// <summary>±: flips the sign of the number being typed ("2×3" becomes "2×−3").</summary>
        public void Negate()
        {
            if (!Continue()) return;
            string number = TrailingNumber();
            if (number.Length == 0) return;
            int start = buffer.Length - number.Length;
            buffer = IsSignAt(start - 1) ? buffer.Remove(start - 1, 1) : buffer.Insert(start, "−");
        }

        // Longest names first: "asin(" must be tried before "sin(".
        static readonly string[] Tokens =
            { " mod ", "asin(", "acos(", "atan(", "sin(", "cos(", "tan(", "log(", "exp(", "abs(", "ln(", "√(", "∛(" };

        /// <summary>Removes the last key press: a whole function name or " mod ", otherwise one character.</summary>
        public void Backspace()
        {
            if (!Continue() || buffer.Length == 0) return;
            foreach (string token in Tokens)
            {
                if (!buffer.EndsWith(token, StringComparison.Ordinal)) continue;
                buffer = buffer.Substring(0, buffer.Length - token.Length);
                return;
            }
            buffer = buffer.Substring(0, buffer.Length - 1);
        }

        public void Clear()
        {
            buffer = "";
            expression = "";
            errorText = null;
            afterEquals = false;
        }
    }
}
