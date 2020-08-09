using System;

namespace mktool.Utility
{
    class VaultException : Exception
    {
        public VaultException()
        {
        }
        public VaultException(string message) : base(message)
        {
        }

        public VaultException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
