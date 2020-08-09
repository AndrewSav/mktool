using System;

namespace mktool.Utility
{
    class MktoolException : VaultException
	{
		public MktoolException(string message, ExitCode exitCode) : base(message)
		{
			ExitCode = exitCode;
		}

		public MktoolException(string message, ExitCode exitCode, Exception innerException) : base(message, innerException)
		{
			ExitCode = exitCode;
		}

		public ExitCode ExitCode { get; }
	}
}
