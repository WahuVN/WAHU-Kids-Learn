using System;
using System.Security.Cryptography;
using System.Text;

namespace WAHU.Security
{
    public static class Pbkdf2Sha256
    {
        public static byte[] Derive(string secret, byte[] salt, int iterations, int outputBytes)
        {
            if (secret == null) throw new ArgumentNullException("secret");
            if (salt == null || salt.Length < 1) throw new ArgumentException("salt must not be empty", "salt");
            if (iterations < 1) throw new ArgumentOutOfRangeException("iterations");
            if (outputBytes < 1 || outputBytes > 1024) throw new ArgumentOutOfRangeException("outputBytes");

            var password = Encoding.UTF8.GetBytes(secret);
            try
            {
                using (var hmac = new HMACSHA256(password))
                {
                    var hashLength = hmac.HashSize / 8;
                    var blockCount = (outputBytes + hashLength - 1) / hashLength;
                    var output = new byte[outputBytes];
                    var offset = 0;
                    for (var block = 1; block <= blockCount; block++)
                    {
                        var u = First(hmac, salt, block);
                        var t = (byte[])u.Clone();
                        for (var i = 1; i < iterations; i++)
                        {
                            u = hmac.ComputeHash(u);
                            for (var j = 0; j < t.Length; j++) t[j] ^= u[j];
                        }
                        var take = Math.Min(hashLength, outputBytes - offset);
                        Buffer.BlockCopy(t, 0, output, offset, take);
                        offset += take;
                    }
                    return output;
                }
            }
            finally
            {
                Array.Clear(password, 0, password.Length);
            }
        }

        private static byte[] First(HMACSHA256 hmac, byte[] salt, int block)
        {
            var input = new byte[salt.Length + 4];
            Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
            input[input.Length - 4] = (byte)((block >> 24) & 0xff);
            input[input.Length - 3] = (byte)((block >> 16) & 0xff);
            input[input.Length - 2] = (byte)((block >> 8) & 0xff);
            input[input.Length - 1] = (byte)(block & 0xff);
            return hmac.ComputeHash(input);
        }
    }
}
