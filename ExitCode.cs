namespace mktool
{
    enum ExitCode
    {
        Success = 0,
        CommandLineError = 1,
        VaultRequestError = 2,
        VaultMissingKey = 3,
        VaultMissingToken = 4,
        VaultHttpError = 5,
        MikrotikConnectionError = 6,
        FileWriteError = 7,
        LoggingInitError = 8,
        MissingFormat = 9,
        ImportFileError = 10,
        ValidationError = 11,
        MikrotikWriteError = 12,
        ConfigurationLoadError = 13,
        ConfigurationError = 14,
        AllocationPoolExhausted = 15,
        MikrotikRecordNotFound = 16,
        UnhandledException = 127,
    }
}
