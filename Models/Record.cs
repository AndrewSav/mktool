using System.Diagnostics;
using System.Text;

namespace mktool
{
    class Record
	{
		public string? Ip { get; set; }
		public string? Mac { get; set; }
		public string? DhcpServer { get; set; }
		public string? DhcpLabel { get; set; }
		public string? DnsHostName { get; set; }
		public string? DnsRegexp { get; set; }
		public string? DnsType { get; set; }
		public string? DnsCName { get; set; }
		public bool HasDhcp { get; set; }
		public bool HasDns { get; set; }
		public bool HasWiFi { get; set; }

		public Record()
        {

        }

		public Record(Record r)
        {
			Ip = r.Ip;
			Mac = r.Mac;
			DhcpServer = r.DhcpServer;
			DhcpLabel = r.DhcpLabel;
			DnsHostName = r.DnsHostName;
			DnsRegexp = r.DnsRegexp;
			DnsType = r.DnsType;
			DnsCName = r.DnsCName;
			HasDhcp = r.HasDhcp;
			HasDns = r.HasDns;
			HasWiFi = r.HasWiFi;
		}


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

		public string Format()
        {
			StringBuilder sb = new StringBuilder();
			sb.Append(FormatString("Ip", Ip));
			sb.Append(FormatString("Mac", Mac));
			sb.Append(FormatString("DhcpServer", DhcpServer));
			sb.Append(FormatString("DhcpLabel", DhcpLabel));
			sb.Append(FormatString("DnsHostName", DnsHostName));
			sb.Append(FormatString("DnsRegexp", DnsRegexp));
			sb.Append(FormatString("DnsType", DnsType));
			sb.Append(FormatString("DnsCName", DnsCName));
			sb.Append($"HasDhcp: {HasDhcp}, ");
			sb.Append($"HasDns: {HasDns}, ");
			sb.Append($"HasWiFi: {HasWiFi}");
			return sb.ToString();
		}

		public string FormatWiFi()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(FormatString("Mac", Mac));
			sb.Append(FormatString("DhcpLabel", DhcpLabel));
			sb.Append(FormatString("DnsHostName", DnsHostName));
			return sb.Remove(sb.Length-2,1).ToString();
		}

		public string FormatDhcp()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(FormatString("Ip", Ip));
			sb.Append(FormatString("Mac", Mac));
			sb.Append(FormatString("DhcpServer", DhcpServer));
			sb.Append(FormatString("DhcpLabel", DhcpLabel));
			return sb.Remove(sb.Length - 2, 1).ToString();
		}

		public string FormatDns()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(FormatString("Ip", Ip));
			sb.Append(FormatString("DnsHostName", DnsHostName));
			sb.Append(FormatString("DnsRegexp", DnsRegexp));
			sb.Append(FormatString("DnsType", DnsType));
			sb.Append(FormatString("DnsCName", DnsCName));
			return sb.Remove(sb.Length - 2, 1).ToString();
		}
		private string FormatString(string name, string? value)
        {
            return value != null ? $"{name}: {value}, " : string.Empty;
        }
	}
}
