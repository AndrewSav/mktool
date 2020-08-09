namespace mktool.CommandLine
{
    class RootOptions
    {
        public string? Address { get; set; }
        public string? User { get; set; }
        public string? Password { get; set; }
        public string? VaultUserLocation { get; set; }
        public string? VaultPasswordLocation { get; set; }
        public string? VaultUserKey { get; set; }
        public string? VaultPasswordKey { get; set; }
        public string? VaultAddress { get; set; }
        public string? VaultToken { get; set; }
        public bool VaultDiag { get; set; }
        public string? LogLevel { get; set; }
    }
}
