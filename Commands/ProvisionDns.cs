using mktool.CommandLine;
using mktool.Models;
using mktool.Utility;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using tik4net;

namespace mktool.Commands
{
    static class ProvisionDns
    {
        public static async Task Execute(ProvisionDnsOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("ProvisionDns command started");
            Log.Debug("Parameters: {@params}", options);

            if (!options.Execute)
            {
                Console.WriteLine("DRY RUN");
            }

            Debug.Assert(options.RecordType != null);

            Record record = new Record
            {
                DnsHostName = options.DnsName,
                DnsType = options.RecordType.ToUpper(),
                HasDhcp = false,
                HasDns = true,
                HasWiFi = false,
                Ip = options.IpAddress,
                DnsCName = options.Cname,
                DnsRegexp = options.Regexp
            };

            ITikConnection connection = await Mikrotik.ConnectAsync(options);

            Mikrotik.CreateMikrotikDnsRecord(GetMikrotikOptions(options), connection, record);

        }
        private static MikrotikOptions GetMikrotikOptions(ProvisionDnsOptions options)
        {
            return new MikrotikOptions
            {
                ContinueOnErrors = false,
                Execute = options.Execute,
                LogToStdout = true
            };
        }
    }
}
