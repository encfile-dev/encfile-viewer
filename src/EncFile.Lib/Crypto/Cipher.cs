using System.Security.Cryptography;

namespace EncFile.Lib.Crypto
{
    public static class Cipher
    {
        public static (byte[] ciphertext, byte[] tag) EncryptChunk(byte[] key, byte[] nonce, byte[] plaintext)
        {
            using var aes = new AesGcm(key);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            aes.Encrypt(nonce, plaintext, null, ciphertext, tag);
            return (ciphertext, tag);
        }

        public static byte[] DecryptChunk(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag)
        {
            using var aes = new AesGcm(key);
            var plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
    }
}
