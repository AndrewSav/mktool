using System;

namespace mktool.Utility
{
    class VaultTokenException : VaultException
	{
		public VaultTokenException(string message) : base(message)
		{
		}

		public VaultTokenException(string message, Exception innerException) : base(message, innerException)
		{
		}

        public VaultTokenException()
        {
        }
    }
}
