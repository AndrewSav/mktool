namespace mktool
{
    class DeprovisionOptions : RootOptions
    {
        public string? IpAddress { get; set; }
        public string? MacAddress { get; set; }
        public string? DnsName { get; set; }
        public string? Label { get; set; }
        public bool Disable { get; set; }
    }
}
