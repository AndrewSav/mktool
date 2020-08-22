using System;

namespace mktool
{
    class MktoolException : VaultException
    {
        public MktoolException()
        {
        }

        public MktoolException(string message) : base(message)
        {
        }

        public MktoolException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public MktoolException(ExitCode exitCode)
        {
            ExitCode = exitCode;
        }

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
