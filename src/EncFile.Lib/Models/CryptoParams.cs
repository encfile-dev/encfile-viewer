using System;

namespace EncFile.Lib.Models
{
    public class CryptoParams
    {
        public byte[] Salt { get; }
        public byte[] BaseNonce { get; }
        public byte KdfProfile { get; }

        public CryptoParams(byte[] salt, byte[] baseNonce, byte kdfProfile)
        {
            Salt = salt ?? throw new ArgumentNullException(nameof(salt));
            BaseNonce = baseNonce ?? throw new ArgumentNullException(nameof(baseNonce));
            KdfProfile = kdfProfile;
        }
    }
}
