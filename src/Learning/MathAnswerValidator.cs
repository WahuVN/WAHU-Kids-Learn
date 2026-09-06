using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace WAHU.Learning
{
    /// <summary>
    /// Strict, culture-stable Math answer equivalence.
    /// Numeric answers accept mathematically equivalent integer/decimal/fraction forms,
    /// while free text remains deliberately conservative.
    /// </summary>
    public static class MathAnswerValidator
    {
        private const int MaxAnswerLength = 256;
        private const int MaxExpressionDepth = 16;
        private const int MaxExpressionTokens = 96;

        public static bool IsCorrect(MathQuestion question, string answer)
        {
            if (question == null) throw new ArgumentNullException("question");
            if (string.IsNullOrWhiteSpace(answer) || answer.Length > MaxAnswerLength) return false;

            var expectedAnswers = ExpectedAnswers(question);
            foreach (var expected in expectedAnswers)
            {
                if (Matches(question, answer, expected)) return true;
            }
            return false;
        }

        public static bool TryParseInteger(string text, out int value)
        {
            value = 0;
            Rational number;
            if (!TryParseNumber(text, false, out number) || !number.IsInteger) return false;
            if (number.Numerator < int.MinValue || number.Numerator > int.MaxValue) return false;
            value = (int)number.Numerator;
            return true;
        }

        public static bool IsWellFormedNumericAnswer(string text)
        {
            Rational value;
            return TryParseNumber(text, false, out value);
        }

        private static IEnumerable<string> ExpectedAnswers(MathQuestion question)
        {
            var yielded = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(question.CorrectAnswerText) && yielded.Add(question.CorrectAnswerText))
                yield return question.CorrectAnswerText;

            if (question.AcceptedAnswers != null)
            {
                foreach (var accepted in question.AcceptedAnswers)
                {
                    if (!string.IsNullOrWhiteSpace(accepted) && yielded.Add(accepted))
                        yield return accepted;
                }
            }

            var numericFallback = question.CorrectAnswer.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (yielded.Count == 0 && yielded.Add(numericFallback)) yield return numericFallback;
        }

        private static bool Matches(MathQuestion question, string actual, string expected)
        {
            var kind = (question.AnswerKind ?? "integer").Trim().ToLowerInvariant();
            switch (kind)
            {
                case "integer":
                case "interaction_integer":
                case "number":
                case "decimal":
                case "fraction":
                    return NumericEquivalent(actual, expected, question.NumericTolerance, false);
                case "expression":
                    return NumericEquivalent(actual, expected, question.NumericTolerance, true);
                case "unit":
                    return UnitEquivalent(question, actual, expected);
                case "text":
                    return TextEquivalent(actual, expected);
                default:
                    // Unknown answer kinds are intentionally narrow: never silently widen validation.
                    return TextEquivalent(actual, expected);
            }
        }

        private static bool NumericEquivalent(string actual, string expected, double tolerance, bool allowExpression)
        {
            Rational left;
            Rational right;
            if (!TryParseNumber(actual, allowExpression, out left) || !TryParseNumber(expected, allowExpression, out right))
                return false;
            if (left.Equals(right)) return true;
            if (tolerance <= 0 || double.IsNaN(tolerance) || double.IsInfinity(tolerance)) return false;

            var difference = Math.Abs(left.ToDouble() - right.ToDouble());
            return !double.IsNaN(difference) && difference <= tolerance;
        }

        private static bool UnitEquivalent(MathQuestion question, string actual, string expected)
        {
            Rational actualNumber;
            Rational expectedNumber;
            string actualUnit;
            string expectedUnit;
            if (!TrySplitNumberAndUnit(actual, out actualNumber, out actualUnit)) return false;
            if (!TrySplitNumberAndUnit(expected, out expectedNumber, out expectedUnit))
            {
                if (!TryParseNumber(expected, false, out expectedNumber)) return false;
                expectedUnit = question.ExpectedUnit;
            }

            if (!actualNumber.Equals(expectedNumber))
            {
                if (question.NumericTolerance <= 0) return false;
                var diff = Math.Abs(actualNumber.ToDouble() - expectedNumber.ToDouble());
                if (double.IsNaN(diff) || diff > question.NumericTolerance) return false;
            }

            var allowedUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddUnit(allowedUnits, expectedUnit);
            AddUnit(allowedUnits, question.ExpectedUnit);
            if (question.AcceptedUnits != null)
                foreach (var unit in question.AcceptedUnits) AddUnit(allowedUnits, unit);
            return allowedUnits.Count > 0 && allowedUnits.Contains(NormalizeUnit(actualUnit));
        }

        private static void AddUnit(ISet<string> units, string unit)
        {
            var normalized = NormalizeUnit(unit);
            if (normalized.Length > 0) units.Add(normalized);
        }

        private static string NormalizeUnit(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return string.Empty;
            var value = unit.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
            while (value.EndsWith(".", StringComparison.Ordinal)) value = value.Substring(0, value.Length - 1).TrimEnd();
            return CollapseWhitespace(value);
        }

        private static bool TrySplitNumberAndUnit(string text, out Rational number, out string unit)
        {
            number = default(Rational);
            unit = null;
            if (string.IsNullOrWhiteSpace(text) || text.Length > MaxAnswerLength) return false;
            var value = NormalizeSigns(text.Trim());
            for (var i = value.Length; i > 0; i--)
            {
                var numericPart = value.Substring(0, i).TrimEnd();
                var unitPart = value.Substring(i).Trim();
                if (unitPart.Length == 0) continue;
                Rational parsed;
                if (TryParseNumber(numericPart, false, out parsed))
                {
                    number = parsed;
                    unit = unitPart;
                    return true;
                }
            }
            return false;
        }

        private static bool TextEquivalent(string actual, string expected)
        {
            if (actual == null || expected == null) return false;
            return string.Equals(
                CollapseWhitespace(actual.Trim().Normalize(NormalizationForm.FormC)),
                CollapseWhitespace(expected.Trim().Normalize(NormalizationForm.FormC)),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string CollapseWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var builder = new StringBuilder(value.Length);
            var pendingSpace = false;
            foreach (var ch in value)
            {
                if (char.IsWhiteSpace(ch))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }
                if (pendingSpace) builder.Append(' ');
                builder.Append(ch);
                pendingSpace = false;
            }
            return builder.ToString();
        }

        private static bool TryParseNumber(string text, bool allowExpression, out Rational value)
        {
            value = default(Rational);
            if (string.IsNullOrWhiteSpace(text) || text.Length > MaxAnswerLength) return false;
            var normalized = NormalizeSigns(text.Trim());
            if (allowExpression)
            {
                var parser = new ExpressionParser(normalized);
                return parser.TryParse(out value);
            }
            return Rational.TryParseLiteral(normalized, out value);
        }

        private static string NormalizeSigns(string text)
        {
            return (text ?? string.Empty)
                .Replace('\u2212', '-')
                .Replace('\u2013', '-')
                .Replace('\u00D7', '*')
                .Replace('\u00F7', '/');
        }

        private struct Rational : IEquatable<Rational>
        {
            public BigInteger Numerator;
            public BigInteger Denominator;

            public Rational(BigInteger numerator, BigInteger denominator)
            {
                if (denominator.IsZero) throw new DivideByZeroException();
                if (denominator.Sign < 0) { numerator = BigInteger.Negate(numerator); denominator = BigInteger.Negate(denominator); }
                var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
                if (gcd.IsZero) gcd = BigInteger.One;
                Numerator = numerator / gcd;
                Denominator = denominator / gcd;
            }

            public bool IsInteger { get { return Denominator == BigInteger.One; } }

            public double ToDouble()
            {
                return (double)Numerator / (double)Denominator;
            }

            public bool Equals(Rational other)
            {
                return Numerator.Equals(other.Numerator) && Denominator.Equals(other.Denominator);
            }

            public override bool Equals(object obj) { return obj is Rational && Equals((Rational)obj); }
            public override int GetHashCode() { return Numerator.GetHashCode() ^ Denominator.GetHashCode(); }

            public static Rational Add(Rational a, Rational b)
            {
                return new Rational(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
            }

            public static Rational Subtract(Rational a, Rational b)
            {
                return new Rational(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);
            }

            public static Rational Multiply(Rational a, Rational b)
            {
                return new Rational(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
            }

            public static bool TryDivide(Rational a, Rational b, out Rational result)
            {
                result = default(Rational);
                if (b.Numerator.IsZero) return false;
                result = new Rational(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
                return true;
            }

            public static Rational Negate(Rational value) { return new Rational(BigInteger.Negate(value.Numerator), value.Denominator); }

            public static bool TryParseLiteral(string text, out Rational value)
            {
                value = default(Rational);
                if (string.IsNullOrWhiteSpace(text)) return false;
                var trimmed = text.Trim();
                var slash = trimmed.IndexOf('/');
                if (slash >= 0)
                {
                    if (slash != trimmed.LastIndexOf('/')) return false;
                    BigInteger numerator;
                    BigInteger denominator;
                    if (!TryParseIntegerToken(trimmed.Substring(0, slash).Trim(), out numerator) ||
                        !TryParseIntegerToken(trimmed.Substring(slash + 1).Trim(), out denominator) || denominator.IsZero)
                        return false;
                    value = new Rational(numerator, denominator);
                    return true;
                }

                var separator = -1;
                for (var i = 0; i < trimmed.Length; i++)
                {
                    if (trimmed[i] != '.' && trimmed[i] != ',') continue;
                    if (separator >= 0) return false;
                    separator = i;
                }

                if (separator < 0)
                {
                    BigInteger integer;
                    if (!TryParseIntegerToken(trimmed, out integer)) return false;
                    value = new Rational(integer, BigInteger.One);
                    return true;
                }

                var sign = 1;
                var unsigned = trimmed;
                if (unsigned.StartsWith("+", StringComparison.Ordinal)) unsigned = unsigned.Substring(1);
                else if (unsigned.StartsWith("-", StringComparison.Ordinal)) { sign = -1; unsigned = unsigned.Substring(1); }
                var localSeparator = unsigned.IndexOfAny(new[] { '.', ',' });
                if (localSeparator < 0 || localSeparator != unsigned.LastIndexOfAny(new[] { '.', ',' })) return false;
                var whole = unsigned.Substring(0, localSeparator);
                var fraction = unsigned.Substring(localSeparator + 1);
                if (whole.Length == 0 || fraction.Length == 0 || !DigitsOnly(whole) || !DigitsOnly(fraction)) return false;
                BigInteger raw;
                if (!BigInteger.TryParse(whole + fraction, out raw)) return false;
                if (sign < 0) raw = BigInteger.Negate(raw);
                value = new Rational(raw, BigInteger.Pow(10, fraction.Length));
                return true;
            }

            private static bool TryParseIntegerToken(string token, out BigInteger value)
            {
                value = BigInteger.Zero;
                if (string.IsNullOrEmpty(token)) return false;
                var start = token[0] == '+' || token[0] == '-' ? 1 : 0;
                if (start == token.Length) return false;
                for (var i = start; i < token.Length; i++) if (token[i] < '0' || token[i] > '9') return false;
                return BigInteger.TryParse(token, out value);
            }

            private static bool DigitsOnly(string text)
            {
                if (string.IsNullOrEmpty(text)) return false;
                foreach (var ch in text) if (ch < '0' || ch > '9') return false;
                return true;
            }
        }

        private sealed class ExpressionParser
        {
            private readonly string _text;
            private int _index;
            private int _tokens;
            private int _depth;

            public ExpressionParser(string text) { _text = text ?? string.Empty; }

            public bool TryParse(out Rational value)
            {
                value = default(Rational);
                try
                {
                    SkipWhitespace();
                    if (!ParseExpression(out value)) return false;
                    SkipWhitespace();
                    return _index == _text.Length && _tokens <= MaxExpressionTokens;
                }
                catch (ArithmeticException) { return false; }
            }

            private bool ParseExpression(out Rational value)
            {
                if (!ParseTerm(out value)) return false;
                while (true)
                {
                    SkipWhitespace();
                    if (Take('+'))
                    {
                        Rational right;
                        if (!ParseTerm(out right)) return false;
                        value = Rational.Add(value, right);
                    }
                    else if (Take('-'))
                    {
                        Rational right;
                        if (!ParseTerm(out right)) return false;
                        value = Rational.Subtract(value, right);
                    }
                    else return true;
                }
            }

            private bool ParseTerm(out Rational value)
            {
                if (!ParseUnary(out value)) return false;
                while (true)
                {
                    SkipWhitespace();
                    if (Take('*'))
                    {
                        Rational right;
                        if (!ParseUnary(out right)) return false;
                        value = Rational.Multiply(value, right);
                    }
                    else if (Take('/'))
                    {
                        Rational right;
                        Rational divided;
                        if (!ParseUnary(out right) || !Rational.TryDivide(value, right, out divided)) return false;
                        value = divided;
                    }
                    else return true;
                }
            }

            private bool ParseUnary(out Rational value)
            {
                SkipWhitespace();
                if (Take('-'))
                {
                    if (!ParsePrimary(out value)) return false;
                    value = Rational.Negate(value);
                    return true;
                }
                if (Take('+')) return ParsePrimary(out value);
                return ParsePrimary(out value);
            }

            private bool ParsePrimary(out Rational value)
            {
                value = default(Rational);
                SkipWhitespace();
                if (Take('('))
                {
                    _depth++;
                    if (_depth > MaxExpressionDepth) return false;
                    if (!ParseExpression(out value)) return false;
                    SkipWhitespace();
                    if (!Take(')')) return false;
                    _depth--;
                    return true;
                }

                var start = _index;
                var seenSeparator = false;
                while (_index < _text.Length)
                {
                    var ch = _text[_index];
                    if (ch >= '0' && ch <= '9') { _index++; continue; }
                    if ((ch == '.' || ch == ',') && !seenSeparator) { seenSeparator = true; _index++; continue; }
                    break;
                }
                if (_index == start) return false;
                _tokens++;
                if (_tokens > MaxExpressionTokens) return false;
                return Rational.TryParseLiteral(_text.Substring(start, _index - start), out value);
            }

            private bool Take(char expected)
            {
                if (_index >= _text.Length || _text[_index] != expected) return false;
                _index++;
                _tokens++;
                return _tokens <= MaxExpressionTokens;
            }

            private void SkipWhitespace()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index])) _index++;
            }
        }
    }
}
