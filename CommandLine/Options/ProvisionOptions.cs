using System.IO;

namespace mktool.CommandLine
{
    class ProvisionOptions : RootOptions
    {
        public string? MacAddress { get; set; }
        public FileInfo? Config { get; set; }
        public string? Allocation { get; set; }
        public bool EnableWifi { get; set; }
        public string? DnsName { get; set; }
        public string? Label { get; set; }
    }
}
