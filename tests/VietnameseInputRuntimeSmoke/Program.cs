using System;
using System.Linq;
using System.Text;
using WAHU.TypingInput;

namespace WAHU.VietnameseInputRuntimeSmoke
{
    internal static class Program
    {
        private static int _assertions;

        private static void Main()
        {
            TestNfcNfdAndCaseFold();
            TestVietnameseShapesAndDStroke();
            TestAllToneMarks();
            TestBaseModeNoDiacritics();
            TestTelexAliases();
            TestVniAliases();
            TestComparisonCandidateUniqueness();
            Console.WriteLine("VIETNAMESE_INPUT_RUNTIME_SMOKE_PASS assertions=" + _assertions);
        }

        private static void TestNfcNfdAndCaseFold()
        {
            var nfc = "TIẾNG VIỆT";
            var nfd = nfc.Normalize(NormalizationForm.FormD);
            Equal("tiếng việt", VietnameseInputNormalizer.Canonicalize(nfc), "nfc_casefold");
            Equal("tiếng việt", VietnameseInputNormalizer.Canonicalize(nfd), "nfd_to_nfc");
            Assert(VietnameseInputNormalizer.Canonicalize(nfd).IsNormalized(NormalizationForm.FormC), "canonical_is_nfc");
        }

        private static void TestVietnameseShapesAndDStroke()
        {
            Equal("a a e o o u d", VietnameseInputNormalizer.RemoveVietnameseDiacritics("ă â ê ô ơ ư đ"), "shape_fold");
            Equal("a a e o o u đ", VietnameseInputNormalizer.RemoveVietnameseDiacritics("Ă Â Ê Ô Ơ Ư Đ", false), "shape_preserve_d_stroke");
        }

        private static void TestAllToneMarks()
        {
            Equal("a a a a a", VietnameseInputNormalizer.RemoveVietnameseDiacritics("á à ả ã ạ"), "tone_a");
            Equal("e e e e e", VietnameseInputNormalizer.RemoveVietnameseDiacritics("é è ẻ ẽ ẹ"), "tone_e");
            Equal("o o o o o", VietnameseInputNormalizer.RemoveVietnameseDiacritics("ố ồ ổ ỗ ộ"), "tone_o_circumflex");
            Equal("u u u u u", VietnameseInputNormalizer.RemoveVietnameseDiacritics("ứ ừ ử ữ ự"), "tone_u_horn");
        }

        private static void TestBaseModeNoDiacritics()
        {
            Assert(VietnameseInputNormalizer.IsAccepted("Phi thuyền", "phi thuyen"), "base_unaccented_accept");
            Assert(VietnameseInputNormalizer.IsAccepted("Đèn đỏ", "den do"), "d_stroke_unaccented_accept");
            Assert(VietnameseInputNormalizer.IsAccepted("CỨU HỘ", "cứu hộ"), "native_casefold_accept");
            Assert(!VietnameseInputNormalizer.IsAccepted("cứu hộ", "cuu ho!"), "extra_character_reject");
        }

        private static void TestTelexAliases()
        {
            Equal("tieengs vieetj", VietnameseInputNormalizer.ToTelex("Tiếng Việt"), "telex_tieng_viet");
            Equal("aw aa ee oo ow uw dd", VietnameseInputNormalizer.ToTelex("ă â ê ô ơ ư đ"), "telex_shapes");
            Assert(VietnameseInputNormalizer.IsAccepted("Tiếng Việt", "tieengs vieetj"), "telex_accept");
            Equal("ddoons", VietnameseInputNormalizer.ToTelex("đốn"), "telex_shape_plus_tone");
        }

        private static void TestVniAliases()
        {
            Equal("tie6ng1 vie6t5", VietnameseInputNormalizer.ToVni("Tiếng Việt"), "vni_tieng_viet");
            Equal("a8 a6 e6 o6 o7 u7 d9", VietnameseInputNormalizer.ToVni("ă â ê ô ơ ư đ"), "vni_shapes");
            Assert(VietnameseInputNormalizer.IsAccepted("Tiếng Việt", "tie6ng1 vie6t5"), "vni_accept");
            Equal("d9o6n1", VietnameseInputNormalizer.ToVni("đốn"), "vni_shape_plus_tone");
        }

        private static void TestComparisonCandidateUniqueness()
        {
            var plain = VietnameseInputNormalizer.GetAcceptedCandidates("abc");
            Assert(plain.Count == plain.Distinct().Count(), "candidate_unique");
            Assert(plain.Contains("abc"), "plain_candidate_present");

            var forms = VietnameseInputNormalizer.GetComparisonForms("ĐỎ");
            Assert(forms.Contains("đỏ"), "comparison_native");
            Assert(forms.Contains("do"), "comparison_plain");
        }

        private static void Equal(string expected, string actual, string name)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception("ASSERT_FAIL " + name + ": expected=[" + expected + "] actual=[" + actual + "]");
            _assertions++;
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new Exception("ASSERT_FAIL: " + name);
            _assertions++;
        }
    }
}
