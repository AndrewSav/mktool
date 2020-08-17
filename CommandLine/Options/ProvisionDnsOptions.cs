﻿using System.IO;

namespace mktool.CommandLine
{
    class ProvisionDnsOptions : RootOptions
    {
        public string? RecordType { get; set; }
        public string? DnsName { get; set; }
        public string? Regexp { get; set; }
        public string? IpAddress { get; set; }
        public string? Cname { get; set; }
        public bool Execute { get; set; }
        public bool Disable { get; set; }
    }
}
