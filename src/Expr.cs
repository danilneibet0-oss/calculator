using System;
using System.Globalization;

namespace Calculator
{
    public enum AngleMode { Degrees, Radians, Gradians }

    /// <summary>Thrown when an expression cannot be evaluated; Message is shown to the user.</summary>
    public sealed class ExprException : Exception
    {
        public ExprException(string message) : base(message) { }
    }

    /// <summary>Evaluates text expressions for the scientific mode.</summary>
    public static class Expr
    {
        public const string SyntaxError = "Ошибка в выражении";
        public const string DivideByZero = "Деление на ноль невозможно";
        public const string Undefined = "Результат не определён";
        public const string Invalid = "Недопустимый ввод";
        public const string Overflow = "Переполнение";

        public static double Evaluate(string text, AngleMode angle)
        {
            if (text == null) throw new ExprException(SyntaxError);
            return new Parser(text, angle).ParseAll();
        }

        static double Divide(double a, double b)
        {
            if (b == 0) throw new ExprException(a == 0 ? Undefined : DivideByZero);
            return a / b;
        }

        /// <summary>Turns NaN and infinity into user-facing errors.</summary>
        static double Check(double v)
        {
            if (double.IsNaN(v)) throw new ExprException(Invalid);
            if (double.IsInfinity(v)) throw new ExprException(Overflow);
            return v;
        }

        /// <summary>Remainder with the sign of the dividend, like the C# % operator.</summary>
        static double Modulo(double a, double b)
        {
            if (b == 0) throw new ExprException(DivideByZero);
            return a % b;
        }

        static double Factorial(double v)
        {
            if (v < 0 || v != Math.Floor(v)) throw new ExprException(Invalid);
            if (v > 170) throw new ExprException(Overflow);   // 171! no longer fits in a double
            double r = 1;
            for (int i = 2; i <= (int)v; i++) r *= i;
            return r;
        }

        static double Power(double b, double e)
        {
            if (b == 0 && e == 0) throw new ExprException(Undefined);
            if (b == 0 && e < 0) throw new ExprException(DivideByZero);
            return Check(Math.Pow(b, e));
        }

        sealed class Parser
        {
            readonly string s;
            readonly AngleMode angle;
            int pos;

            public Parser(string text, AngleMode angle)
            {
                s = text;
                this.angle = angle;
            }

            public double ParseAll()
            {
                double v = ParseExpr();
                SkipSpaces();
                if (pos < s.Length) throw new ExprException(SyntaxError);
                return v;
            }

            double ParseExpr()
            {
                double v = ParseTerm();
                while (true)
                {
                    if (Match('+')) v += ParseTerm();
                    else if (Match('-') || Match('−')) v -= ParseTerm();
                    else return v;
                }
            }

            double ParseTerm()
            {
                double v = ParseUnary();
                while (true)
                {
                    if (Match('*') || Match('×')) v *= ParseUnary();
                    else if (Match('/') || Match('÷')) v = Divide(v, ParseUnary());
                    else if (MatchWord("mod")) v = Modulo(v, ParseUnary());
                    else return v;
                }
            }

            double ParseUnary()
            {
                if (Match('-') || Match('−')) return -ParseUnary();
                if (Match('+')) return ParseUnary();
                return ParsePower();
            }

            // Right-associative, and binds tighter than unary minus: -2^2 = -4, 2^3^2 = 512.
            double ParsePower()
            {
                double b = ParsePostfix();
                if (Match('^')) return Power(b, ParseUnary());
                return b;
            }

            double ParsePostfix()
            {
                double v = ParsePrimary();
                while (true)
                {
                    if (Match('!')) v = Factorial(v);
                    else if (Match('%')) v /= 100;
                    else return v;
                }
            }

            double ParsePrimary()
            {
                SkipSpaces();
                if (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.' || s[pos] == ',')) return ParseNumber();
                if (Match('('))
                {
                    double inner = ParseExpr();
                    if (!Match(')')) throw new ExprException(SyntaxError);
                    return inner;
                }
                if (Match('π')) return Math.PI;
                if (pos < s.Length && IsAsciiLetter(s[pos]))
                {
                    string word = ReadWord();
                    if (word == "pi") return Math.PI;
                    if (word == "e") return Math.E;
                }
                throw new ExprException(SyntaxError);
            }

            double ParseNumber()
            {
                int start = pos;
                bool seenDot = false, seenDigit = false;
                while (pos < s.Length)
                {
                    char c = s[pos];
                    if (char.IsDigit(c)) seenDigit = true;
                    else if ((c == '.' || c == ',') && !seenDot) seenDot = true;
                    else break;
                    pos++;
                }
                if (!seenDigit) throw new ExprException(SyntaxError);
                return double.Parse(s.Substring(start, pos - start).Replace(',', '.'), CultureInfo.InvariantCulture);
            }

            /// <summary>Consumes c (after optional spaces) if it is next.</summary>
            bool Match(char c)
            {
                SkipSpaces();
                if (pos < s.Length && s[pos] == c) { pos++; return true; }
                return false;
            }

            /// <summary>Consumes a whole word (not a prefix of a longer one) if it is next.</summary>
            bool MatchWord(string word)
            {
                SkipSpaces();
                int end = pos + word.Length;
                if (end > s.Length) return false;
                if (string.Compare(s, pos, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
                if (end < s.Length && IsAsciiLetter(s[end])) return false;
                pos = end;
                return true;
            }

            static bool IsAsciiLetter(char c)
            {
                return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
            }

            string ReadWord()
            {
                int start = pos;
                while (pos < s.Length && IsAsciiLetter(s[pos])) pos++;
                return s.Substring(start, pos - start).ToLowerInvariant();
            }

            void SkipSpaces()
            {
                while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
            }
        }
    }
}
