namespace mktool
{
    class IdentityRecord : Record 
    {
        public string? WifiId { get; set; }
        public string? DhcpId { get; set; }
        public string? DnsId { get; set;  }
    }
}
