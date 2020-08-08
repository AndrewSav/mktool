using System;
using System.Collections.Generic;
using System.Text;

namespace mktool.Models
{
	class Record
	{
		public string? IP { get; set; } // dhcp, dns
		public string? Mac { get; set; } // dhcp, wifi
		public string? DhcpServer { get; set; }
		public string? DhcpLabel { get; set; }
		public string? DnsHostName { get; set; } // Dhcp comment, dns name, wifi comment
		public string? DnsRegexp { get; set; } // Dhcp comment, dns name, wifi comment
		public string? DnsType { get; set; }
		public string? DnsCName { get; set; }
		public bool HasDhcp { get; set; }
		public bool HasDns { get; set; }
		public bool HasWifi { get; set; }
	}
}
