using mktool.CommandLine;
using mktool.Models;
using mktool.Utility;
using Nett;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using tik4net;

namespace mktool.Commands
{
    static class ProvisionDhcp
    {
        public static async Task Execute(ProvisionDhcpOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("ProvisionDhcp command started");
            Log.Debug("Parameters: {@params}", options);

            if (!options.Execute)
            {
                Console.WriteLine("DRY RUN");
            }

            Allocation[] allocations;
            Debug.Assert(options.Config != null);
            try
            {
                allocations = Toml.ReadStream<AllocationTomlWrapper>(options.Config.OpenRead()).Allocation ??= new Allocation[0];
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Configuration load error {options.Config.FullName}; " + ex.Message);
                throw new MktoolException(ExitCode.ConfigurationLoadError);
            }

            Debug.Assert(options.Allocation != null);

            Allocation? allocation = allocations.FirstOrDefault(x => string.Equals(x.Name,options.Allocation, StringComparison.OrdinalIgnoreCase));

            if (allocation == null)
            {
                Console.Error.WriteLine($"Cannot find allocation with name {options.Allocation} in the configuration file");
                throw new MktoolException(ExitCode.ConfigurationError);
            }

            if (string.IsNullOrWhiteSpace(allocation.IpRange))
            {
                Console.Error.WriteLine($"Allocation {options.Allocation} does not have IpRange");
                throw new MktoolException(ExitCode.ConfigurationError);
            }

            Ip4Range range;
            try
            {
                range = Ip4Range.Parse(allocation.IpRange);
            }
            catch(FormatException ex)
            {
                Console.Error.WriteLine(ex.Message);
                throw new MktoolException(ExitCode.ConfigurationError);
            }

            if (string.IsNullOrWhiteSpace(allocation.DhcpServer))
            {
                Console.Error.WriteLine($"Allocation {options.Allocation} does not have DhcpServer");
                throw new MktoolException(ExitCode.ConfigurationError);
            }

            ITikConnection connection = await Mikrotik.ConnectAsync(options);

            IEnumerable<ITikSentence> dhcp = Mikrotik.GetDhcpRecords(connection);
            var usedIps = dhcp.Where(x => !x.Words.ContainsKey("disabled") || x.Words["disabled"] != "true")
                .SelectMany(x => x.Words.Where(y => y.Key == "address")).Select(x=> x.Value).ToList();
            string? ip;
            do
            {
                ip = range.GetNext();
                if (ip == null)
                {
                    Console.Error.WriteLine($"There are no free allocations left in the given range");
                    throw new MktoolException(ExitCode.AllocationPoolExhausted);
                }
                
            } while (usedIps.Contains(ip.ToString()));

            string macAddress;
            if (options.MacAddress == null)
            {
                Debug.Assert(options.ActiveHost != null);
                macAddress = dhcp.Where(x => x.Words.ContainsKey("dynamic") &&  x.Words["dynamic"] != "true" && 
                        x.Words.ContainsKey("host-name") && x.Words["host-name"] == options.ActiveHost)
                    .SelectMany(x => x.Words.Where(y => y.Key == "mac-address")).Select(x => x.Value).FirstOrDefault();
                if (macAddress == null)
                {
                    Console.Error.WriteLine($"No dynamic DHCP record with given host name {options.ActiveHost} was found");
                    throw new MktoolException(ExitCode.MikrotikRecordNotFound);
                }                
            }
            else
            {
                macAddress = options.MacAddress;
            }

            Record record = new Record
            {
                DhcpLabel = options.Label ?? options.DnsName,
                DhcpServer = allocation.DhcpServer,
                DnsHostName = options.DnsName,
                DnsType = "A",
                HasDhcp = true,
                HasDns = options.DnsName == null,
                HasWiFi = options.EnableWiFi,
                IP = ip,
                Mac = macAddress
            };

            Mikrotik.CreateMikrotikDhcpRecord(GetMikrotikOptions(options), connection, record);
            List<ITikSentence>? dynamicMatches = dhcp.Where(x => x.Words.Any(y => y.Key == "mac-address" && string.Equals(y.Value, record.Mac, StringComparison.OrdinalIgnoreCase)))
                .Where(x => x.Words.Any(y => y.Key == "dynamic" && y.Value == "true"))
                .Where(x => x.Words.Any(y => y.Key == "disabled" && y.Value == "false"))
                .ToList();
            foreach (ITikSentence? dyn in dynamicMatches)
            {
                Mikrotik.DeleteMikrotikDhcpRecord(GetMikrotikOptions(options), connection, dyn);
            }

            if (options.DnsName != null)
            {
                Mikrotik.CreateMikrotikDnsRecord(GetMikrotikOptions(options), connection, record);
            }

            if (options.EnableWiFi)
            {
                Mikrotik.CreateMikrotikWifiRecord(GetMikrotikOptions(options), connection, record);
            }

            Console.WriteLine($"{{\"ip\"=\"{ip}\"}}");
        }
        private static MikrotikOptions GetMikrotikOptions(ProvisionDhcpOptions options)
        {
            return new MikrotikOptions
            {
                ContinueOnErrors = options.ContinueOnErrors,
                Execute = options.Execute,
                LogToStdout = false,
                SkipExisting = true
            };
        }
    }
}
