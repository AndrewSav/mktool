using System.Diagnostics;

namespace mktool
{
    class Record
	{
		public string? Ip { get; set; } // DHCP, DNS
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

		public string GetDnsId() 
		{ 
			if (string.IsNullOrWhiteSpace(DnsHostName))
            {
				Debug.Assert(DnsRegexp!=null);
				return DnsRegexp;
            }
			else
            {
				return DnsHostName;
			}
		}
		public string GetDnsIdName()
		{
			return string.IsNullOrWhiteSpace(DnsHostName) ? "DnsRegexp" : "DnsHostName";
		}

		public string GetDnsIdField()
		{
			return string.IsNullOrWhiteSpace(DnsHostName) ? "regexp" : "name";
		}

		public Record SetEmptyPropertiesToNull()
		{
			Record result = (Record)MemberwiseClone();
			foreach (var property in GetType().GetProperties())
			{
				var value = property.GetValue(result, null);
				if (value is string s && string.IsNullOrWhiteSpace(s))
				{
					property.SetValue(result, null);
				}
			}
			return result;
		}
	}
}
