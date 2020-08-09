namespace mktool
{
    enum ExitCode
    {
        VaultRequestError = 1,
        VaultMissingAddress = 2,
        VaultMissingKey = 3,
        VaultMissingToken = 4,
        VaultHttpError = 5,
        MikrotikConnectionError = 6,
        FileWriteError = 7,
        LoggingInitError = 8
    }
}
