using System;

namespace mktool.Utility
{
    class VaultMissingKeyException : VaultException
	{
		public VaultMissingKeyException(string message, string response) : base(message)
		{
			Response = response;
		}

		public VaultMissingKeyException(string message, string response, Exception innerException) : base(message, innerException)
		{
			Response = response;
		}

		public string Response { get; }
	}
}
