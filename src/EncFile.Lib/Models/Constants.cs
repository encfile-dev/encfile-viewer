using System.Text;

namespace EncFile.Lib.Models
{
    public static class EncFileConstants
    {
        public static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("ENCFILE1");
        public const int HEADER_SIZE = 20;
        public const int CRYPTO_SIZE = 29;
        public const string VERSION = "1.0.1";
        public const int METADATA_MAX_SIZE = 65536;
        public const int METADATA_MAX_DEPTH = 10;
        public const int CHUNK_TAG_SIZE = 16;
    }
}
