using EncFile.Lib.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EncFile.Lib.Core
{
    public static class HeaderPacking
    {
        public static byte[] PackHeader(EncFileHeader header)
        {
            if ((header.Flags & 0xF8) != 0) throw new ArgumentException("Unused flag bits (3-7) must be zero");

            var buffer = new byte[EncFileConstants.HEADER_SIZE];
            EncFileConstants.MAGIC.CopyTo(buffer, 0);
            buffer[8] = header.Version;
            buffer[9] = header.Flags;
            WriteUInt32BE(buffer, 10, header.PayloadOffset);
            buffer[14] = header.Mode;
            // 15-19 reserved (already zero)
            return buffer;
        }

        public static EncFileHeader UnpackHeader(byte[] data)
        {
            if (data.Length != EncFileConstants.HEADER_SIZE) throw new ArgumentException("Invalid fixed header size");
            if (!data.AsSpan(0, 8).SequenceEqual(EncFileConstants.MAGIC)) throw new InvalidDataException("Invalid magic string");

            byte version = data[8];
            if (version != 0x01) throw new NotSupportedException($"Unsupported version: 0x{version:X2}");

            byte flags = data[9];
            if ((flags & 0xF8) != 0) throw new InvalidDataException("Unknown flags set; rejecting per spec");

            uint payloadOffset = ReadUInt32BE(data, 10);
            byte mode = data[14];

            for (int i = 15; i < 20; i++)
                if (data[i] != 0) throw new InvalidDataException("Reserved header bytes must be zero");

            return new EncFileHeader(version, flags, payloadOffset, mode);
        }

        public static byte[] PackCryptoParams(CryptoParams @params)
        {
            var buffer = new byte[EncFileConstants.CRYPTO_SIZE];
            @params.Salt.CopyTo(buffer, 0);
            @params.BaseNonce.CopyTo(buffer, 16);
            buffer[28] = @params.KdfProfile;
            return buffer;
        }

        public static CryptoParams UnpackCryptoParams(byte[] data)
        {
            if (data.Length != EncFileConstants.CRYPTO_SIZE) throw new ArgumentException("Invalid crypto params size");
            var salt = new byte[16];
            var baseNonce = new byte[12];
            Buffer.BlockCopy(data, 0, salt, 0, 16);
            Buffer.BlockCopy(data, 16, baseNonce, 0, 12);
            return new CryptoParams(salt, baseNonce, data[28]);
        }

        private static void WriteUInt32BE(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static uint ReadUInt32BE(byte[] buffer, int offset)
        {
            return (uint)((buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3]);
        }
    }
}
