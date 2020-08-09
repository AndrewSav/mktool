using System;
using System.Collections.Generic;
using System.Text;

namespace mktool
{
    enum ExitCode
    {
        VaultRequestError = 1,
        VaultMissingAddress,
        VaultMissingKey,
        VaultMissingToken,
        VaultHttpError,
        MikrotikConnectionError,
        FileWriteError,
        LoggingInitError
    }
}
