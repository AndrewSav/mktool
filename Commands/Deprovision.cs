using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using tik4net;

namespace mktool
{
    static class Deprovision
    {
        public static async Task Execute(DeprovisionOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("Deprovision command started");
            Log.Debug("Parameters: {@params}", options);

            if (!options.Execute)
            {
                Console.WriteLine("DRY RUN");
            }

            ITikConnection connection = await Mikrotik.ConnectAsync(options);

            List<IdentityRecord> records = Export.ExportRecords(connection);

            bool filtered = false;

            if (options.DnsName != null)
            {
                Debug.Assert(!filtered);
                filtered = true;
                records = records.Where(x => (string.Equals(x.DnsHostName, options.DnsName, StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(x.DnsRegexp, options.DnsName, StringComparison.OrdinalIgnoreCase)) && x.HasDns).ToList();
            }

            if (options.MacAddress != null)
            {
                Debug.Assert(!filtered);
                filtered = true;
                records = records.Where(x => string.Equals(x.Mac, options.MacAddress, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (options.Label != null)
            {
                Debug.Assert(!filtered);
                filtered = true;
                records = records.Where(x => string.Equals(x.DhcpLabel, options.Label, StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(x.DnsHostName, options.Label, StringComparison.OrdinalIgnoreCase)  && !x.HasDns)).ToList();
            }

            if (options.IpAddress != null)
            {
                Debug.Assert(!filtered);
                filtered = true;
                records = records.Where(x => string.Equals(x.Ip, options.IpAddress, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (records.Count == 0)
            {
                await Console.Error.WriteLineAsync($"No records to deprovision were found");
                throw new MktoolException(ExitCode.MikrotikRecordNotFound);
            }
            
            foreach (var record in records)
            {
                Mikrotik.DeleteOrDisableMikrotikRecord(GetMikrotikOptions(options), connection, record, options.Disable);
            }
        }
        private static MikrotikOptions GetMikrotikOptions(DeprovisionOptions options)
        {
            return new MikrotikOptions
            {
                ContinueOnErrors = true,
                Execute = options.Execute,
                LogToStdout = true,
            };
        }
    }
}
