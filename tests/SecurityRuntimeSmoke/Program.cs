using System;
using System.IO;
using System.Text;
using WAHU.Security;

namespace WAHU.SecurityRuntimeSmoke
{
    internal static class Program
    {
        private static int _a;
        private static void Main()
        {
            TestPbkdf2Vector();
            var root = Path.Combine(Path.GetTempPath(), "wahu-security-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                TestStore(root);
                TestSaltRandomness(root);
                TestPersistentLockout(root);
                Console.WriteLine("SECURITY_RUNTIME_SMOKE_PASS assertions=" + _a);
            }
            finally { Directory.Delete(root, true); }
        }

        private static void TestPbkdf2Vector()
        {
            var actual = Hex(Pbkdf2Sha256.Derive("password", Encoding.ASCII.GetBytes("salt"), 1, 32));
            const string expected = "120FB6CFFCF8B32C43E7225256C4F837A86548C92CCC35480805987CB70BE17B";
            A(actual == expected, "pbkdf2_sha256_known_vector_c1");
            var actual2 = Hex(Pbkdf2Sha256.Derive("password", Encoding.ASCII.GetBytes("salt"), 2, 32));
            const string expected2 = "AE4D0C95AF6B46D32D0ADFF928F06DD02A303F8EF3C251DFD6E2D85A95474C43";
            A(actual2 == expected2, "pbkdf2_sha256_known_vector_c2");
        }

        private static void TestStore(string root)
        {
            var path = Path.Combine(root, "pin.json");
            var store = new ParentPinStore(path);
            A(!store.IsConfigured, "initially_unconfigured");
            store.SetPin("2468", 1000);
            A(store.IsConfigured, "pin_configured");
            var text = File.ReadAllText(path);
            A(text.IndexOf("2468", StringComparison.Ordinal) < 0, "pin_plaintext_not_stored");
            A(text.Contains(ParentPinStore.Algorithm), "algorithm_version_stored");
            A(store.Verify("2468").Success, "correct_pin_verified");
            A(!store.Verify("1357").Success, "wrong_pin_rejected");
            A(!store.ChangePin("1357", "9999"), "change_requires_current_pin");
            A(store.ChangePin("2468", "9999"), "change_pin_success");
            A(store.Verify("9999").Success, "new_pin_verified");
        }

        private static void TestSaltRandomness(string root)
        {
            var a = Path.Combine(root, "salt-a.json");
            var b = Path.Combine(root, "salt-b.json");
            new ParentPinStore(a).SetPin("2468", 1000);
            new ParentPinStore(b).SetPin("2468", 1000);
            A(File.ReadAllText(a) != File.ReadAllText(b), "same_pin_uses_random_salt");
        }

        private static void TestPersistentLockout(string root)
        {
            var now = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
            Func<DateTime> clock = () => now;
            var path = Path.Combine(root, "lock.json");
            var store = new ParentPinStore(path, clock);
            store.SetPin("1234", 1000);
            for (var i = 0; i < 4; i++) A(!store.Verify("0000").Locked, "pre_lock_failure_" + i);
            var fifth = store.Verify("0000");
            A(fifth.Locked && fifth.RemainingSeconds == 30, "fifth_failure_locks_30s");
            var restarted = new ParentPinStore(path, clock);
            var stillLocked = restarted.Verify("1234");
            A(stillLocked.Locked && !stillLocked.Success, "lockout_persists_restart");
            now = now.AddSeconds(31);
            A(restarted.Verify("1234").Success, "correct_pin_after_lock_expiry");
        }

        private static string Hex(byte[] b) { return BitConverter.ToString(b).Replace("-", string.Empty); }
        private static void A(bool ok, string name) { if (!ok) throw new Exception("ASSERT_FAIL: " + name); _a++; }
    }
}
