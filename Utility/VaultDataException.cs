using System;

namespace mktool.Utility
{
    class VaultDataException : Exception
	{
		public VaultDataException(string message, string response) : base(message)
		{
			Response = response;
		}

		public VaultDataException(string message, string response, Exception innerException) : base(message, innerException)
		{
			Response = response;
		}

		public string Response { get; }
	}
}
