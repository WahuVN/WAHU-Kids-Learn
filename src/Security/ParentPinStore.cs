using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace WAHU.Security
{
    public sealed class ParentPinStore
    {
        public const string Algorithm = "PBKDF2-HMAC-SHA256";
        public const int DefaultIterations = 80000;
        private static readonly Regex PinFormat = new Regex("^[0-9]{4,10}$", RegexOptions.Compiled);
        private readonly string _recordPath;
        private readonly Func<DateTime> _utcNow;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public ParentPinStore(string recordPath) : this(recordPath, () => DateTime.UtcNow) { }

        public ParentPinStore(string recordPath, Func<DateTime> utcNow)
        {
            if (string.IsNullOrWhiteSpace(recordPath)) throw new ArgumentException("recordPath");
            _recordPath = Path.GetFullPath(recordPath);
            _utcNow = utcNow ?? throw new ArgumentNullException("utcNow");
        }

        public string RecordPath { get { return _recordPath; } }
        public bool IsConfigured { get { return File.Exists(_recordPath); } }

        public void SetPin(string pin) { SetPin(pin, DefaultIterations); }

        public void SetPin(string pin, int iterations)
        {
            ValidatePin(pin);
            if (iterations < 1000 || iterations > 5000000) throw new ArgumentOutOfRangeException("iterations");
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            var hash = Pbkdf2Sha256.Derive(pin, salt, iterations, 32);
            try
            {
                var record = new ParentPinRecord
                {
                    schema_version = 1,
                    algorithm = Algorithm,
                    iterations = iterations,
                    salt_b64 = Convert.ToBase64String(salt),
                    hash_b64 = Convert.ToBase64String(hash),
                    failed_attempts = 0,
                    locked_until_utc = null,
                    updated_at_utc = Now().ToString("o")
                };
                Save(record);
            }
            finally
            {
                Array.Clear(salt, 0, salt.Length);
                Array.Clear(hash, 0, hash.Length);
            }
        }

        public ParentPinVerifyResult Verify(string pin)
        {
            if (!IsConfigured) return Result(false, false, 0, "pin_not_configured");
            var record = Load();
            var now = Now();
            DateTime lockedUntil;
            if (TryParseUtc(record.locked_until_utc, out lockedUntil) && lockedUntil > now)
                return Result(false, true, Math.Max(1, (int)Math.Ceiling((lockedUntil - now).TotalSeconds)), "temporarily_locked");

            if (!PinFormat.IsMatch(pin ?? string.Empty))
                return RegisterFailure(record, now, "pin_format_invalid");

            var salt = Convert.FromBase64String(record.salt_b64);
            var expected = Convert.FromBase64String(record.hash_b64);
            var actual = Pbkdf2Sha256.Derive(pin, salt, record.iterations, expected.Length);
            var ok = FixedTimeEquals(expected, actual);
            Array.Clear(salt, 0, salt.Length);
            Array.Clear(expected, 0, expected.Length);
            Array.Clear(actual, 0, actual.Length);

            if (!ok) return RegisterFailure(record, now, "pin_mismatch");
            if (record.failed_attempts != 0 || !string.IsNullOrWhiteSpace(record.locked_until_utc))
            {
                record.failed_attempts = 0;
                record.locked_until_utc = null;
                record.updated_at_utc = now.ToString("o");
                Save(record);
            }
            return Result(true, false, 0, "verified");
        }

        public bool ChangePin(string currentPin, string newPin)
        {
            var verify = Verify(currentPin);
            if (!verify.Success) return false;
            SetPin(newPin);
            return true;
        }

        private ParentPinVerifyResult RegisterFailure(ParentPinRecord record, DateTime now, string reason)
        {
            record.failed_attempts++;
            var locked = false;
            var seconds = 0;
            if (record.failed_attempts >= 5)
            {
                seconds = Math.Min(300, 30 * (record.failed_attempts - 4));
                record.locked_until_utc = now.AddSeconds(seconds).ToString("o");
                locked = true;
            }
            record.updated_at_utc = now.ToString("o");
            Save(record);
            return Result(false, locked, seconds, locked ? "temporarily_locked" : reason);
        }

        private ParentPinRecord Load()
        {
            ParentPinRecord record;
            try { record = _json.Deserialize<ParentPinRecord>(File.ReadAllText(_recordPath)); }
            catch (Exception ex) { throw new InvalidDataException("Parent PIN record invalid.", ex); }
            if (record == null || record.schema_version != 1) throw new InvalidDataException("Unsupported Parent PIN record schema.");
            if (!string.Equals(record.algorithm, Algorithm, StringComparison.Ordinal)) throw new InvalidDataException("Unsupported Parent PIN algorithm.");
            if (record.iterations < 1000 || record.iterations > 5000000) throw new InvalidDataException("Parent PIN iteration count invalid.");
            try
            {
                if (Convert.FromBase64String(record.salt_b64 ?? string.Empty).Length < 16) throw new InvalidDataException("Parent PIN salt invalid.");
                if (Convert.FromBase64String(record.hash_b64 ?? string.Empty).Length != 32) throw new InvalidDataException("Parent PIN hash invalid.");
            }
            catch (FormatException ex) { throw new InvalidDataException("Parent PIN base64 invalid.", ex); }
            if (record.failed_attempts < 0 || record.failed_attempts > 1000000) throw new InvalidDataException("Parent PIN failed_attempts invalid.");
            return record;
        }

        private void Save(ParentPinRecord record)
        {
            var parent = Path.GetDirectoryName(_recordPath);
            if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
            var temp = _recordPath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temp, _json.Serialize(record));
                if (!File.Exists(_recordPath))
                {
                    File.Move(temp, _recordPath);
                }
                else
                {
                    try { File.Replace(temp, _recordPath, null, true); }
                    catch (PlatformNotSupportedException) { ReplaceWithRollbackFallback(temp); }
                    catch (IOException) { ReplaceWithRollbackFallback(temp); }
                }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private void ReplaceWithRollbackFallback(string temp)
        {
            var backup = _recordPath + ".bak";
            if (File.Exists(backup)) File.Delete(backup);
            File.Copy(_recordPath, backup, false);
            try
            {
                File.Delete(_recordPath);
                File.Move(temp, _recordPath);
                File.Delete(backup);
            }
            catch
            {
                if (!File.Exists(_recordPath) && File.Exists(backup)) File.Move(backup, _recordPath);
                throw;
            }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            var diff = a.Length ^ b.Length;
            var max = Math.Max(a.Length, b.Length);
            for (var i = 0; i < max; i++)
            {
                var av = i < a.Length ? a[i] : (byte)0;
                var bv = i < b.Length ? b[i] : (byte)0;
                diff |= av ^ bv;
            }
            return diff == 0;
        }

        private static void ValidatePin(string pin)
        {
            if (!PinFormat.IsMatch(pin ?? string.Empty)) throw new ArgumentException("PIN phải gồm 4–10 chữ số.", "pin");
        }

        private DateTime Now()
        {
            var now = _utcNow();
            if (now.Kind == DateTimeKind.Local) return now.ToUniversalTime();
            if (now.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(now, DateTimeKind.Utc);
            return now;
        }

        private static bool TryParseUtc(string text, out DateTime value)
        {
            value = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(text)) return false;
            DateTime parsed;
            if (!DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed)) return false;
            value = parsed.Kind == DateTimeKind.Local ? parsed.ToUniversalTime() : DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        private static ParentPinVerifyResult Result(bool success, bool locked, int seconds, string reason)
        {
            return new ParentPinVerifyResult { Success = success, Locked = locked, RemainingSeconds = seconds, Reason = reason };
        }
    }
}
