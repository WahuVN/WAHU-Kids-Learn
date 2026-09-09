using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WAHU.TypingInput
{
    /// <summary>
    /// Pure Vietnamese input normalization used by typing gameplay.
    /// It never depends on the operating-system IME.
    /// </summary>
    public static class VietnameseInputNormalizer
    {
        private static readonly Dictionary<char, string> TelexBase = new Dictionary<char, string>
        {
            {'ă', "aw"}, {'â', "aa"}, {'ê', "ee"}, {'ô', "oo"}, {'ơ', "ow"}, {'ư', "uw"}, {'đ', "dd"}
        };

        private static readonly Dictionary<char, string> VniBase = new Dictionary<char, string>
        {
            {'ă', "a8"}, {'â', "a6"}, {'ê', "e6"}, {'ô', "o6"}, {'ơ', "o7"}, {'ư', "u7"}, {'đ', "d9"}
        };

        private static readonly Dictionary<char, char> ToneToTelex = new Dictionary<char, char>
        {
            {'\u0301', 's'}, // sắc
            {'\u0300', 'f'}, // huyền
            {'\u0309', 'r'}, // hỏi
            {'\u0303', 'x'}, // ngã
            {'\u0323', 'j'}  // nặng
        };

        private static readonly Dictionary<char, char> ToneToVni = new Dictionary<char, char>
        {
            {'\u0301', '1'},
            {'\u0300', '2'},
            {'\u0309', '3'},
            {'\u0303', '4'},
            {'\u0323', '5'}
        };

        /// <summary>Returns lowercase NFC for stable ordinal comparison.</summary>
        public static string Canonicalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        /// <summary>
        /// Removes Vietnamese vowel/tone diacritics. By default đ/Đ is folded to d.
        /// Set foldDStroke=false when a caller wants to preserve đ semantics.
        /// </summary>
        public static string RemoveVietnameseDiacritics(string value, bool foldDStroke = true)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var canonical = Canonicalize(value);
            var decomposed = canonical.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (c == 'đ')
                    builder.Append(foldDStroke ? 'd' : 'đ');
                else
                    builder.Append(c);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Returns comparison forms for raw input. Index 0 is canonical Unicode;
        /// index 1, when different, is the accent-insensitive V1 form.
        /// </summary>
        public static IReadOnlyList<string> GetComparisonForms(string raw)
        {
            var result = new List<string>();
            AddUnique(result, Canonicalize(raw));
            AddUnique(result, RemoveVietnameseDiacritics(raw));
            return result.AsReadOnly();
        }

        /// <summary>
        /// Accepted aliases for a displayed target: Unicode, no-diacritic V1,
        /// deterministic Telex and deterministic VNI. The latter two are adapters,
        /// not OS-IME emulation.
        /// </summary>
        public static IReadOnlyList<string> GetAcceptedCandidates(string displayText)
        {
            var result = new List<string>();
            AddUnique(result, Canonicalize(displayText));
            AddUnique(result, RemoveVietnameseDiacritics(displayText));
            AddUnique(result, ToTelex(displayText));
            AddUnique(result, ToVni(displayText));
            return result.AsReadOnly();
        }

        public static bool IsAccepted(string displayText, string rawInput)
        {
            var input = Canonicalize(rawInput);
            if (input.Length == 0 && !string.IsNullOrEmpty(displayText)) return false;

            foreach (var candidate in GetAcceptedCandidates(displayText))
            {
                if (string.Equals(candidate, input, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static string ToTelex(string value)
        {
            return ToInputMethodAlias(value, TelexBase, ToneToTelex);
        }

        public static string ToVni(string value)
        {
            return ToInputMethodAlias(value, VniBase, ToneToVni);
        }

        private static string ToInputMethodAlias(
            string value,
            IDictionary<char, string> shapeMap,
            IDictionary<char, char> toneMap)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var canonical = Canonicalize(value);
            var output = new StringBuilder(canonical.Length * 2);
            var token = new StringBuilder();
            var tokenTones = new StringBuilder();

            Action flushToken = () =>
            {
                if (token.Length == 0 && tokenTones.Length == 0) return;
                output.Append(token);
                output.Append(tokenTones);
                token.Clear();
                tokenTones.Clear();
            };

            foreach (var c in canonical)
            {
                if (char.IsWhiteSpace(c) || IsTokenBoundary(c))
                {
                    flushToken();
                    output.Append(c);
                    continue;
                }

                AppendVietnameseCharacter(c, token, tokenTones, shapeMap, toneMap);
            }

            flushToken();
            return output.ToString();
        }

        private static void AppendVietnameseCharacter(
            char value,
            StringBuilder token,
            StringBuilder tokenTones,
            IDictionary<char, string> shapeMap,
            IDictionary<char, char> toneMap)
        {
            if (value == 'đ')
            {
                token.Append(shapeMap['đ']);
                return;
            }

            var decomposed = value.ToString().Normalize(NormalizationForm.FormD);
            if (decomposed.Length == 0) return;

            // Keep shape marks (breve/circumflex/horn) but separate tone marks.
            // This turns e.g. ố -> ô + sắc, ứ -> ư + sắc before alias mapping.
            var shape = new StringBuilder();
            shape.Append(decomposed[0]);
            for (var i = 1; i < decomposed.Length; i++)
            {
                char tone;
                if (toneMap.TryGetValue(decomposed[i], out tone))
                    tokenTones.Append(tone);
                else if (CharUnicodeInfo.GetUnicodeCategory(decomposed[i]) == UnicodeCategory.NonSpacingMark)
                    shape.Append(decomposed[i]);
            }

            var shapedBase = shape.ToString().Normalize(NormalizationForm.FormC);
            string encodedBase;
            if (shapedBase.Length == 1 && shapeMap.TryGetValue(shapedBase[0], out encodedBase))
                token.Append(encodedBase);
            else
                token.Append(shapedBase);
        }

        private static bool IsTokenBoundary(char c)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            return category == UnicodeCategory.DashPunctuation ||
                   category == UnicodeCategory.ConnectorPunctuation ||
                   category == UnicodeCategory.OtherPunctuation ||
                   category == UnicodeCategory.OpenPunctuation ||
                   category == UnicodeCategory.ClosePunctuation ||
                   category == UnicodeCategory.InitialQuotePunctuation ||
                   category == UnicodeCategory.FinalQuotePunctuation;
        }

        private static void AddUnique(ICollection<string> values, string value)
        {
            foreach (var existing in values)
            {
                if (string.Equals(existing, value, StringComparison.Ordinal)) return;
            }
            values.Add(value);
        }
    }
}
