using System;

namespace mktool
{
    class VaultNoAddressException : VaultException
    {
        public VaultNoAddressException(string message) : base(message)
        {
        }

        public VaultNoAddressException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public VaultNoAddressException()
        {
        }
    }
}
