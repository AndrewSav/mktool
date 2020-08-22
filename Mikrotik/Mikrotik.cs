using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using tik4net;

namespace mktool
{
    static class Mikrotik
    {
        public static async Task<ITikConnection> ConnectAsync(RootOptions options)
        {
            (string username, string password) = await CredentialsHelper.GetUsernameAndPassword(options);

            Debug.Assert(options.Address != null);

            Log.Information("Connecting to Mikrotik");
            ITikConnection? connection;
            try
            {
                connection = await ConnectionFactory.OpenConnectionAsync(TikConnectionType.Api, options.Address, username, password);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error connecting to Mikrotik. {ex.Message}");
                throw new MktoolException(ExitCode.MikrotikConnectionError);
            }
            return connection;
        }

        private static IEnumerable<ITikSentence> CallMikrotik(ITikConnection? connection, string[] request)
        {
            Debug.Assert(connection != null);
            Log.Information("Executing Mikrotik call {@request}", request);
            List<ITikSentence> response;
            try
            {
                response = connection.CallCommandSync(request).ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error executing Mikrotik command: {ex.Message}");
                throw new MktoolException(ExitCode.MikrotikConnectionError);
            }
            Log.Verbose("Response: {@response}", response);
            return response;
        }

        public static IEnumerable<ITikSentence> GetDhcpRecords(ITikConnection? connection)
        {
            return CallMikrotik(connection, new[] { "/ip/dhcp-server/lease/print" });
        }
        public static IEnumerable<ITikSentence> GetDnsRecords(ITikConnection? connection)
        {
            return CallMikrotik(connection, new[] { "/ip/dns/static/print" });

        }
        public static IEnumerable<ITikSentence> GetWifiRecords(ITikConnection? connection)
        {
            return CallMikrotik(connection, new[] { "/interface/wireless/access-list/print" });
        }

        private static void ProcessResponse(bool continueOnErrors, IEnumerable<ITikSentence> result)
        {
            foreach (var responseSentence in result)
            {
                if (responseSentence is ITikTrapSentence trap)
                {
                    Console.Error.WriteLine($"!Error: {trap.Message}");
                    Log.Error(trap.Message);
                    if (!continueOnErrors)
                    {
                        throw new MktoolException(ExitCode.MikrotikWriteError);
                    }
                }
            }
        }

        public static void DeleteMikrotikDhcpRecord(MikrotikOptions options, ITikConnection connection, ITikSentence existing)
        {
            if (options.LogToStdout) Console.WriteLine($"-Deleting dynamic DHCP record {existing.Words["mac-address"]}, {existing.Words["address"]}, {existing.Words["host-name"]}");
            Log.Information("Deleting dynamic DHCP record {mac}, {ip}, {hostName}", existing.Words["mac-address"], existing.Words["address"], existing.Words["host-name"]);
            string[] sentence = new[]
            {
                "/ip/dhcp-server/lease/remove",
                $"=.id={existing.Words[".id"]}",
            };
            if (options.Execute)
            {
                IEnumerable<ITikSentence> result = CallMikrotik(connection, sentence);
                ProcessResponse(options.ContinueOnErrors, result);
            }
        }

        public static void CreateMikrotikDhcpRecord(MikrotikOptions options, ITikConnection connection, Record record)
        {
            if (options.LogToStdout) Console.WriteLine($"+Creating DHCP record. IP: {record.Ip}, MAC: {record.Mac}, Comment: {record.DhcpLabel}, Server: {record.DhcpServer}");
            Log.Information("Creating DHCP record. IP: {IP}, MAC: {MAC}, Comment: {Label}, Server: {Server}", record.Ip, record.Mac, record.DhcpLabel, record.DhcpServer);

            if (string.IsNullOrWhiteSpace(record.DhcpLabel))
            {
                record.DhcpLabel = record.DnsHostName;
            }

            string[] sentence = {
                "/ip/dhcp-server/lease/add",
                $"=address={record.Ip}",
                $"=mac-address={record.Mac}",
                $"=comment={record.DhcpLabel}",
                $"=server={record.DhcpServer}",
                "=use-src-mac=true",
            };

            if (options.Execute)
            {
                IEnumerable<ITikSentence> result = CallMikrotik(connection, sentence);
                ProcessResponse(options.ContinueOnErrors, result);
            }
        }

        public static void UpdateMikrotikDhcpRecord(MikrotikOptions options, ITikConnection connection, ITikSentence existing, Record record)
        {
            Dictionary<string, string> fieldsToUpdate = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(record.DhcpLabel))
            {
                record.DhcpLabel = record.DnsHostName;
            }

            record.Ip ??= "";
            record.Mac ??= "";
            record.DhcpLabel ??= "";
            record.DhcpServer ??= "";

            if (existing.Words["address"] != record.Ip)
            {
                fieldsToUpdate.Add("address", record.Ip);
            }
            if (existing.Words["comment"] != record.DhcpLabel)
            {
                fieldsToUpdate.Add("comment", record.DhcpLabel);
            }
            if (existing.Words["mac-address"] != record.Mac)
            {
                fieldsToUpdate.Add("mac-address", record.Mac);
            }
            if (existing.Words["server"] != record.DhcpServer)
            {
                fieldsToUpdate.Add("server", record.DhcpServer);
            }

            if (fieldsToUpdate.Count == 0)
            {
                if (!options.SkipExisting)
                {
                    if (options.LogToStdout) Console.WriteLine($"=DHCP record already exist. IP {record.Ip}, MAC {record.Mac}, Label {record.DhcpLabel}, Server {record.DhcpServer}");
                }
                Log.Information("DHCP record already exist. IP {IP}, MAC {MAC}, Label {Label}, Server {Server}", record.Ip, record.Mac, record.DhcpLabel, record.DhcpServer);
                return;
            }

            if (options.LogToStdout) Console.WriteLine($"^Updating DHCP record. IP {record.Ip}, MAC {record.Mac}, Label {record.DhcpLabel}, Server {record.DhcpServer}");
            Log.Information("Updating DHCP record. IP {IP}, MAC {MAC}, Label {Label}, Server {Server}", record.Ip, record.Mac, record.DhcpLabel, record.DhcpServer);
            foreach (KeyValuePair<string, string> kvp in fieldsToUpdate)
            {
                if (options.LogToStdout) Console.WriteLine($">{kvp.Key}: {existing.Words[kvp.Key]} => {kvp.Value}");
                Log.Information("{field}: {oldValue} => {newValue}", kvp.Key, existing.Words[kvp.Key], kvp.Value);
            }

            string[] sentence = new[] { "/ip/dhcp-server/lease/set" }
                .Concat(fieldsToUpdate.Select(x => $"={x.Key}={x.Value}"))
                .Concat(new[] { $"=.id={existing.Words[".id"]}" })
                .ToArray();
            if (options.Execute)
            {
                IEnumerable<ITikSentence> result = CallMikrotik(connection, sentence);
                ProcessResponse(options.ContinueOnErrors, result);
            }
        }
        public static void CreateMikrotikDnsRecord(MikrotikOptions options, ITikConnection connection, Record record)
        {
            string[] sentence;
            if (string.Equals(record.DnsType, "A", StringComparison.OrdinalIgnoreCase))
            {
                if (options.LogToStdout) Console.WriteLine($"+Creating DNS A record. {record.GetDnsIdName()}: {record.GetDnsId()}, DnsType: {record.DnsType}, IP: {record.Ip}");
                Log.Information($"Creating DNS A record. {record.GetDnsIdName()}: {{dns}}, DnsType: {{type}}, IP: {{address}}", record.GetDnsId(), record.DnsType, record.Ip);
                sentence = new[]
                {
                    "/ip/dns/static/add",
                    $"={record.GetDnsIdField()}={record.GetDnsId()}",
                    "=type=A",
                    $"=address={record.Ip}",
                };
            }
            else
            {
                if (options.LogToStdout) Console.WriteLine($"+Creating DNS CNAME record. {record.GetDnsIdName()}: {record.GetDnsId()}, DnsType: {record.DnsType}, DnsСName: {record.DnsCName}");
                Log.Information($"Creating DNS A record. {record.GetDnsIdName()}: {{dns}}, DnsType: {{type}}, DnsCName: {{cname}}", record.GetDnsId(), record.DnsType, record.DnsCName);
                sentence = new[]
                {
                    "/ip/dns/static/add",
                    $"={record.GetDnsIdField()}={record.GetDnsId()}",
                    "=type=CNAME",
                    $"=cname={record.DnsCName}",
                };
            }

            if (options.Execute)
            {
                IEnumerable<ITikSentence> result = CallMikrotik(connection, sentence);
                ProcessResponse(options.ContinueOnErrors, result);
            }
        }
        public static void CreateMikrotikWifiRecord(MikrotikOptions options, ITikConnection connection, Record record)
        {
            if (options.LogToStdout) Console.WriteLine($"+Creating Wifi record. MAC: {record.Mac}, Comment: {record.DnsHostName}");
            Log.Information("Creating Wifi record. MAC: {mac}, Comment: {dns}", record.Mac, record.DnsHostName);
            string[] sentence = new[]
            {
                "/interface/wireless/access-list/add",
                $"=mac-address={record.Mac}",
                $"=comment={record.DnsHostName}",
                "=authentication=true",
                "=forwarding=true",
            };
            if (options.Execute)
            {
                IEnumerable<ITikSentence> result = CallMikrotik(connection, sentence);
                ProcessResponse(options.ContinueOnErrors, result);
            }
        }

        public static void UpdateMikrotikWifiRecord(MikrotikOptions options, ITikConnection connection, ITikSentence existing, Record record)
        {
            if (string.IsNullOrWhiteSpace(record.DnsHostName) || existing.Words["comment"] == record.DnsHostName)
            {
                if (!options.SkipExisting)
                {
                    if (options.LogToStdout) Console.WriteLine($"=Wifi record already exist. MAC: {record.Mac}, DnsHostName: {record.DnsHostName}");
                }
                Log.Information("Wifi record already exist. MAC: {mac}, DnsHostName: {dns}", record.Mac, record.DnsHostName);
                return;
            }

            if (options.LogToStdout) Console.WriteLine($"^Updating Wifi record. MAC: {record.Mac}");
            Log.Information("Updating Wifi record. MAC: {MAC}", record.Mac);
            if (options.LogToStdout) Console.WriteLine($">comment: {existing.Words["comment"]} => {record.DnsHostName}");
            Log.Information("comment: {oldValue} => {newValue}", existing.Words["comment"], record.DnsHostName);

            string[] sentence = new[]
            {
                "/interface/wireless/access-list/set",
                $"=comment={record.DnsHostName}",
                $"=.id={existing.Words[".id"]}",
            };
            if (options.Execute)
            {
                IEnumerable<ITikSentence> result = CallMikrotik(connection, sentence);
                ProcessResponse(options.ContinueOnErrors, result);
            }
        }
    }
}
