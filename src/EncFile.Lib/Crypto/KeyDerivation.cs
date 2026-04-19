using Konscious.Security.Cryptography;
using System;
using System.Security.Cryptography;

namespace EncFile.Lib.Crypto
{
    public enum KdfProfile : byte
    {
        Pbkdf2 = 0x00,
        Argon2Id = 0x01
    }

    public static class KeyDerivation
    {
        public static byte[] DeriveKey(byte[] password, byte[] salt, KdfProfile profile)
        {
            switch (profile)
            {
                case KdfProfile.Pbkdf2:
                    {
                        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 600_000, HashAlgorithmName.SHA256);
                        return pbkdf2.GetBytes(32);
                    }

                case KdfProfile.Argon2Id:
                    {
                        using var argon2 = new Argon2id(password)
                        {
                            Salt = salt,
                            DegreeOfParallelism = 4,
                            Iterations = 3,
                            MemorySize = 65536
                        };
                        return argon2.GetBytes(32);
                    }

                default:
                    throw new ArgumentException($"Unsupported KDF profile: {(byte)profile:X2}");
            }
        }

        public static byte[] DeriveNonce(byte[] baseNonce, long chunkIndex)
        {
            if (baseNonce.Length != 12) throw new ArgumentException("BaseNonce must be exactly 12 bytes");
            var nonce = new byte[12];
            Buffer.BlockCopy(baseNonce, 0, nonce, 0, 4);
            // Big-endian 8-byte chunk index
            for (int i = 7; i >= 0; i--)
            {
                nonce[4 + i] = (byte)(chunkIndex & 0xFF);
                chunkIndex >>= 8;
            }
            return nonce;
        }
    }
}
