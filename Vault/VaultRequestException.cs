using System;

namespace mktool
{
    public class VaultRequestException : VaultException
    {
        public VaultRequestException(string message, string response) : base(message)
        {
            Response = response;
        }

        public VaultRequestException(string message, string response, Exception innerException) : base(message, innerException)
        {
            Response = response;
        }

        public string Response { get; }
    }
}
