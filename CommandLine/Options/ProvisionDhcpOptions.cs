using System.IO;

namespace mktool.CommandLine
{
    class ProvisionDhcpOptions : RootOptions
    {
        public string? MacAddress { get; set; }
        public string? ActiveHost { get; set; }
        public FileInfo? Config { get; set; }
        public string? Allocation { get; set; }
        public bool EnableWiFi { get; set; }
        public string? DnsName { get; set; }
        public string? Label { get; set; }
        public bool Execute { get; set; }
        public bool ContinueOnErrors { get; set; }
    }
}
