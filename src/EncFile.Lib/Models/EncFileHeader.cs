namespace EncFile.Lib.Models
{
    public class EncFileHeader
    {
        public byte Version { get; }
        public byte Flags { get; }
        public uint PayloadOffset { get; }
        public byte Mode { get; }

        public EncFileHeader(byte version, byte flags, uint payloadOffset, byte mode)
        {
            Version = version;
            Flags = flags;
            PayloadOffset = payloadOffset;
            Mode = mode;
        }
    }
}
