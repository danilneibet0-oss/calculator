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

        public static double Evaluate(string text, AngleMode angle)
        {
            if (text == null) throw new ExprException(SyntaxError);
            return new Parser(text, angle).ParseAll();
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

            double ParseExpr() { return ParsePrimary(); }

            double ParsePrimary()
            {
                SkipSpaces();
                if (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.' || s[pos] == ',')) return ParseNumber();
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

            void SkipSpaces()
            {
                while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
            }
        }
    }
}
