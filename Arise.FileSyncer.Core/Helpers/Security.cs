using System;
using System.Security.Cryptography;

namespace Arise.FileSyncer.Core.Helpers
{
    public static class Security
    {
        public static Guid KeyGenerator(Guid key, Guid id)
        {
            return new Guid(KeyGenerator(key.ToByteArray(), id.ToByteArray(), 16));
        }

        private static byte[] KeyGenerator(byte[] code, byte[] salt, int length)
        {
            using Rfc2898DeriveBytes generator = new(code, salt, 1000);
            return generator.GetBytes(length);
        }
    }
}
