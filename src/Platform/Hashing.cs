using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WAHU.Platform
{
    public static class Hashing
    {
        public static string Sha256File(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(stream));
            }
        }

        public static string Sha256Text(string text)
        {
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty)));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            const string hex = "0123456789ABCDEF";
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = hex[bytes[i] >> 4];
                chars[i * 2 + 1] = hex[bytes[i] & 15];
            }
            return new string(chars);
        }
    }
}
