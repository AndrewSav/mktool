namespace mktool.Models
{
	class Record
	{
		public string? IP { get; set; } // DHCP, DNS
		public string? Mac { get; set; } // DHCP, WiFi
		public string? DhcpServer { get; set; }
		public string? DhcpLabel { get; set; }
		public string? DnsHostName { get; set; } // DHCP comment, DNS name, WiFi comment
		public string? DnsRegexp { get; set; } // DHCP comment, DNS name, WiFi comment
		public string? DnsType { get; set; }
		public string? DnsCName { get; set; }
		public bool HasDhcp { get; set; }
		public bool HasDns { get; set; }
		public bool HasWiFi { get; set; }
	}
}
