namespace mktool
{
    enum ExitCode
    {
        VaultRequestError = 2,
        VaultMissingAddress = 3,
        VaultMissingKey = 4,
        VaultMissingToken = 5,
        VaultHttpError = 6,
        MikrotikConnectionError = 7,
        FileWriteError = 8,
        LoggingInitError = 9,
        MissingFormat = 10,
        ImportFileError = 11,
        ValidationError = 12,
        MikrotikWriteError = 13
    }
}
