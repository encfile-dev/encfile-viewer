using EncFile.Lib.Crypto;
using EncFile.Lib.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EncFile.Lib.Core
{
    public static class EncFile
    {
        public static void EncryptStream(Stream input, Stream output, byte[] password, KdfProfile kdfProfile = KdfProfile.Argon2Id,
            int chunkSize = 1_048_576, Dictionary<string, object> metadata = null)
        {
            metadata = metadata ?? new Dictionary<string, object>();
            bool hasMeta = metadata.Count > 0;
            byte flags = (byte)(hasMeta ? 0x01 : 0x00);
            byte mode = 1;

            byte[] metaBytes = hasMeta ? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(metadata)) : new byte[0];
            if (hasMeta && metaBytes.Length > EncFileConstants.METADATA_MAX_SIZE)
                throw new ArgumentException("Metadata exceeds 64 KB limit");

            if (hasMeta) ValidateMetadataDepth(metadata, 0);

            byte[] salt = new byte[16];
            RandomNumberGenerator.Fill(salt);

            byte[] baseNonce = new byte[12];
            RandomNumberGenerator.Fill(baseNonce);

            byte[] key = KeyDerivation.DeriveKey(password, salt, kdfProfile);

            uint metaSectionSize = hasMeta ? (uint)(4 + metaBytes.Length) : 0;
            uint payloadOffset = (uint)(EncFileConstants.HEADER_SIZE + EncFileConstants.CRYPTO_SIZE + metaSectionSize);

            output.Write(HeaderPacking.PackHeader(new EncFileHeader(0x01, flags, payloadOffset, mode)), 0, EncFileConstants.HEADER_SIZE);
            output.Write(HeaderPacking.PackCryptoParams(new CryptoParams(salt, baseNonce, (byte)kdfProfile)), 0, EncFileConstants.CRYPTO_SIZE);

            if (hasMeta)
            {
                WriteUInt32BE(output, (uint)metaBytes.Length);
                output.Write(metaBytes, 0, metaBytes.Length);
            }

            long chunkIdx = 0;
            byte[] buffer = new byte[chunkSize];
            int bytesRead;

            while ((bytesRead = input.Read(buffer, 0, chunkSize)) > 0)
            {
                byte[] chunk = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);

                byte[] nonce = KeyDerivation.DeriveNonce(baseNonce, chunkIdx);
                var (ct, tag) = Cipher.EncryptChunk(key, nonce, chunk);

                WriteUInt32BE(output, (uint)ct.Length);
                output.Write(ct, 0, ct.Length);
                output.Write(tag, 0, tag.Length);

                chunkIdx++;
            }
        }

        public static Dictionary<string, object> DecryptStream(Stream input, Stream output, byte[] password)
        {
            byte[] headerBuf = new byte[EncFileConstants.HEADER_SIZE];
            if (input.Read(headerBuf, 0, headerBuf.Length) != headerBuf.Length)
                throw new EndOfStreamException();

            var header = HeaderPacking.UnpackHeader(headerBuf);

            byte[] cryptoBuf = new byte[EncFileConstants.CRYPTO_SIZE];
            if (input.Read(cryptoBuf, 0, cryptoBuf.Length) != cryptoBuf.Length)
                throw new EndOfStreamException();

            var cryptoParams = HeaderPacking.UnpackCryptoParams(cryptoBuf);

            byte[] key = KeyDerivation.DeriveKey(password, cryptoParams.Salt, (KdfProfile)cryptoParams.KdfProfile);

            bool hasMeta = (header.Flags & 0x01) != 0;
            Dictionary<string, object> metadata = null;

            long consumed = EncFileConstants.HEADER_SIZE + EncFileConstants.CRYPTO_SIZE;

            if (hasMeta)
            {
                byte[] metaLenBuf = new byte[4];
                if (input.Read(metaLenBuf, 0, 4) != 4)
                    throw new EndOfStreamException();

                uint metaLen = ReadUInt32BE(metaLenBuf);

                if (metaLen > EncFileConstants.METADATA_MAX_SIZE)
                    throw new ArgumentException("MetadataLen exceeds 64 KB limit");

                byte[] metaData = new byte[metaLen];
                if (input.Read(metaData, 0, metaData.Length) != metaData.Length)
                    throw new EndOfStreamException("Truncated metadata section");

                metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(metaData));

                consumed += 4 + metaLen;
            }

            if (header.PayloadOffset > consumed)
            {
                long skip = header.PayloadOffset - consumed;
                byte[] skipBuf = new byte[8192];

                while (skip > 0)
                {
                    int toRead = (int)Math.Min(skip, skipBuf.Length);
                    int read = input.Read(skipBuf, 0, toRead);
                    if (read == 0)
                        throw new EndOfStreamException("Unexpected EOF while advancing to payload");

                    skip -= read;
                }
            }
            else if (header.PayloadOffset < consumed)
            {
                throw new InvalidDataException("Invalid PayloadOffset in header");
            }

            if (header.Mode != 1)
                throw new NotSupportedException("Streaming decryption only supports Mode 1");

            long chunkIdx = 0;
            byte[] lenBuf = new byte[4];

            while (input.Read(lenBuf, 0, 4) == 4)
            {
                uint ctLen = ReadUInt32BE(lenBuf);

                byte[] ct = new byte[ctLen];
                if (input.Read(ct, 0, ct.Length) != ct.Length)
                    throw new EndOfStreamException("Truncated ciphertext");

                byte[] tag = new byte[16];
                if (input.Read(tag, 0, 16) != 16)
                    throw new EndOfStreamException("Truncated authentication tag");

                byte[] nonce = KeyDerivation.DeriveNonce(cryptoParams.BaseNonce, chunkIdx);

                try
                {
                    byte[] pt = Cipher.DecryptChunk(key, nonce, ct, tag);
                    output.Write(pt, 0, pt.Length);
                }
                catch (Exception ex)
                {
                    throw new CryptographicException($"Authentication failed at chunk {chunkIdx}", ex);
                }

                chunkIdx++;
            }

            return metadata;
        }

        public static void EncryptFile(string inputPath, string outputPath, byte[] password, KdfProfile kdfProfile = KdfProfile.Argon2Id,
            int chunkSize = 1_048_576, Dictionary<string, object> metadata = null)
        {
            using (var inp = File.OpenRead(inputPath))
            using (var outp = File.Create(outputPath))
            {
                EncryptStream(inp, outp, password, kdfProfile, chunkSize, metadata);
            }
        }

        public static Dictionary<string, object> DecryptFile(string inputPath, string outputPath, byte[] password)
        {
            using (var inp = File.OpenRead(inputPath))
            using (var outp = File.Create(outputPath))
            {
                return DecryptStream(inp, outp, password);
            }
        }

        public static byte[] EncryptBytes(byte[] data, byte[] password, KdfProfile kdfProfile = KdfProfile.Argon2Id,
            int chunkSize = 1_048_576, Dictionary<string, object> metadata = null)
        {
            using (var inp = new MemoryStream(data))
            using (var outp = new MemoryStream())
            {
                EncryptStream(inp, outp, password, kdfProfile, chunkSize, metadata);
                return outp.ToArray();
            }
        }

        public static (byte[] plaintext, Dictionary<string, object> metadata) DecryptBytes(byte[] data, byte[] password)
        {
            using (var inp = new MemoryStream(data))
            using (var outp = new MemoryStream())
            {
                var meta = DecryptStream(inp, outp, password);
                return (outp.ToArray(), meta);
            }
        }

        // ---- Helpers ----

        private static void WriteUInt32BE(Stream stream, uint value)
        {
            byte[] buf = BitConverter.GetBytes(value);
            Array.Reverse(buf);
            stream.Write(buf, 0, 4);
        }

        private static uint ReadUInt32BE(byte[] buf)
        {
            byte[] tmp = (byte[])buf.Clone();
            Array.Reverse(tmp);
            return BitConverter.ToUInt32(tmp, 0);
        }

        private static void ValidateMetadataDepth(Dictionary<string, object> obj, int depth)
        {
            if (depth > EncFileConstants.METADATA_MAX_DEPTH)
                throw new ArgumentException("Metadata nesting depth exceeds 10");

            foreach (var v in obj.Values)
            {
                if (v is Dictionary<string, object> dict)
                    ValidateMetadataDepth(dict, depth + 1);
                else if (v is List<object> list)
                    ValidateListDepth(list, depth + 1);
            }
        }

        private static void ValidateListDepth(List<object> list, int depth)
        {
            if (depth > EncFileConstants.METADATA_MAX_DEPTH)
                throw new ArgumentException("Metadata nesting depth exceeds 10");

            foreach (var item in list)
            {
                if (item is Dictionary<string, object> dict)
                    ValidateMetadataDepth(dict, depth + 1);
                else if (item is List<object> sublist)
                    ValidateListDepth(sublist, depth + 1);
            }
        }
    }
}