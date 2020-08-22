using System.Linq;
using System.Collections.Generic;
using System.Net;
using System;

namespace mktool
{
    public class Ip4Range
    {
        private readonly Ip4Span[] _spans;
        private int _currentSpanIndex;
        private uint? _currentIp;

        private Ip4Range(Ip4Span[] spans)
        {
            _spans = Normalize(spans);
            if (_spans.Length != 0)
            {
                _currentIp = _spans[0].Start;
            }
        }

        public string? GetNext()
        {
            if (!_currentIp.HasValue)
            {
                return null;
            }
            string result = ConvertFromIntegerToIpAddress(_currentIp.Value);
            Advance();
            return result;
        }

        private void Advance()
        {
            if (!_currentIp.HasValue)
            {
                return;
            }
            _currentIp++;
            if (_currentIp <= _spans[_currentSpanIndex].End)
            {
                return;
            }
            _currentSpanIndex++;
            if (_currentSpanIndex >= _spans.Length)
            {
                _currentIp = null;
                return;
            }
            _currentIp = _spans[_currentSpanIndex].Start;
        }

        private static Ip4Span[] Normalize(Ip4Span[] spans)
        {
            var results = new List<Ip4Span>();
            foreach (var item in spans.OrderBy(x => x.Start))
            {
                var lastResultIndex = results.Count - 1;
                if (lastResultIndex >= 0 && results[lastResultIndex].TryMerge(item, out Ip4Span mergedItem))
                {
                    results[lastResultIndex] = mergedItem;
                }
                else
                {
                    results.Add(item);
                }
            }
            return results.ToArray();
        }

        public static Ip4Range Parse(string s)
        {
            string[] list = s.Split(',');
            List<Ip4Span> result = new List<Ip4Span>();
            foreach (var el in list)
            {
                Ip4Span span;
                if (el.IndexOf('-') >= 0)
                {
                    span = ParseRange(el);
                }
                else if (el.IndexOf('/') >= 0)
                {
                    span = ParseSubnet(el);
                }
                else
                {
                    span = ParseIp(el);
                }
                result.Add(span);
            }
            return new Ip4Range(result.ToArray());

        }
        private static Ip4Span ParseIp(string el)
        {
            if (!IPAddress.TryParse(el, out IPAddress address))
            {
                throw new FormatException($"'{el}' cannot be parsed as IP address");
            }
            uint a = ConvertFromIpAddressToInteger(address);
            return Ip4Span.Create(a, a);
        }

        private static Ip4Span ParseSubnet(string el)
        {
            string[] parts = el.Split("/");
            if (parts.Length != 2) throw new FormatException($"'{el}' should contain ip address and network mask separated by '/'");
            if (!IPAddress.TryParse(parts[0], out IPAddress ip))
            {
                throw new FormatException($"'{parts[0]}' cannot be parsed as IP address");
            }
            if (!int.TryParse(parts[1], out int netMask))
            {
                throw new FormatException($"'{parts[1]}' cannot be parsed as an integer net mask");
            }
            if (netMask <= 0 || netMask > 32)
            {
                throw new FormatException($"Net mask should between 1 and 32, inclusive, '{netMask}' is not");
            }
            uint start = ConvertFromIpAddressToInteger(ip);
            uint end = start | (uint)IPAddress.HostToNetworkOrder(-1 << 32 - netMask);
            return Ip4Span.Create(start, end);
        }

        private static Ip4Span ParseRange(string el)
        {
            string[] ips = el.Split("-");
            if (ips.Length != 2) throw new FormatException($"'{el}' should contain start of range and end of range separated by '-'");

            if (!IPAddress.TryParse(ips[0], out IPAddress first))
            {
                throw new FormatException($"'{ips[0]}' cannot be parsed as IP address");
            }
            if (!IPAddress.TryParse(ips[1], out IPAddress second))
            {
                throw new FormatException($"'{ips[1]}' cannot be parsed as IP address");
            }
            uint[] a = new[] { ConvertFromIpAddressToInteger(first), ConvertFromIpAddressToInteger(second) };
            Array.Sort(a);
            return Ip4Span.Create(a[0], a[1]);
        }
        private static uint ConvertFromIpAddressToInteger(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();

            // flip big-endian(network order) to little-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt32(bytes, 0);
        }

        private static string ConvertFromIntegerToIpAddress(uint ipAddress)
        {
            byte[] bytes = BitConverter.GetBytes(ipAddress);

            // flip little-endian to big-endian(network order)
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return new IPAddress(bytes).ToString();
        }

    }
}
