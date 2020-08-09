﻿using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace mktool.CommandLine
{
    static class Validation
    {
        private static Regex _macRegex = new Regex("^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$", RegexOptions.Singleline);
        public static bool IsDnsValid(string name)
        {
            return name.Length > 0 &&
                name.Length <= 253 &&
                name.All(c => "abcdefghijklmnopqrstuvwxyz1234567890.-".Contains(c)) &&
                "abcdefghijklmnopqrstuvwxyz1234567890".Contains(name[0]) &&
                "abcdefghijklmnopqrstuvwxyz1234567890".Contains(name[name.Length - 1]);
        }

        public static bool IsIpValid(string address)
        {
            return IPAddress.TryParse(address, out IPAddress _);
        }

        public static bool IsMacValid(string address)
        {
            return _macRegex.IsMatch(address);
        }

    }
}