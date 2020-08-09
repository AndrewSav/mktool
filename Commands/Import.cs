using CsvHelper;
using mktool.CommandLine;
using mktool.Models;
using mktool.Utility;
using Nett;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using tik4net;
using YamlDotNet.Serialization;

namespace mktool.Commands
{
    static class Import
    {
        public static async Task Execute(ImportOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("Import command started");
            Log.Debug("Parameters: {@params}", options);

            if (!options.Execute)
            {
                Console.WriteLine("DRY RUN");
            }

            Debug.Assert(options.File != null);

            if (options.Format == null)
            {
                options.Format = GetFormatFromExtension(options.File.Extension);
            }

            List<Record> records = ReadRecords(options.File.FullName, options.Format);

            List<Record> dhcpRecords = records.Where(x => x.HasDhcp).ToList();
            List<Record> dnsRecords = records.Where(x => x.HasDns).ToList();
            List<Record> wifiRecords = records.Where(x => x.HasWiFi).ToList();
            if (!ValidateDhcpRecords(dhcpRecords) ||!ValidateDnsRecords(dnsRecords))
            {
                throw new MktoolException("Error", ExitCode.ValidationError);
            }

            ITikConnection connection = await Mikrotik.ConnectAsync(options);

            IEnumerable<ITikSentence>? dhcp = Mikrotik.GetDhcpRecords(connection);
            ProcessDhcpRecords(options, connection, dhcp, dhcpRecords);

            IEnumerable<ITikSentence>? dns = Mikrotik.GetDnsRecords(connection);
            ProcessDnsRecords(options, connection, dns, dnsRecords);

            IEnumerable<ITikSentence>? wifi = Mikrotik.GetDhcpRecords(connection);
            ProcessWiFiRecords(options, connection, wifi, wifiRecords);
        }

        private static void ProcessWiFiRecords(ImportOptions options, ITikConnection connection, IEnumerable<ITikSentence> wifi, List<Record> wifiRecords)
        {
            throw new NotImplementedException();
        }

        private static void ProcessDnsRecords(ImportOptions options, ITikConnection connection, IEnumerable<ITikSentence> dns, List<Record> dnsRecords)
        {
            foreach (Record record in dnsRecords)
            {
                List<ITikSentence> nameMatches = dns.Where(x => x.Words.Any(y => y.Key == "name" && y.Value == record.DnsHostName))
                    .Where(x => x.Words.Any(y => y.Key == "dynamic" && y.Value == "false"))
                    .Where(x => x.Words.Any(y => y.Key == "disabled" && y.Value == "false"))
                    .Where(x => x.Words.Any(y => y.Key == "type" && y.Value == record.DnsType))
                    .ToList();
                List<ITikSentence> regexpMatches = dns.Where(x => x.Words.Any(y => y.Key == "regexp" && y.Value == record.DnsRegexp))
                    .Where(x => x.Words.Any(y => y.Key == "dynamic" && y.Value == "false"))
                    .Where(x => x.Words.Any(y => y.Key == "disabled" && y.Value == "false"))
                    .Where(x => x.Words.Any(y => y.Key == "type" && y.Value == record.DnsType))
                    .ToList();

                List<ITikSentence> matches = nameMatches.Count > 0 ? nameMatches : regexpMatches;

                if (matches.Count == 0)
                {
                    CreateMikrotikDnsRecord(connection, record, options.Execute, options.ContinueOnErrors);
                }
                else 
                { 
                    if (string.Compare(record.DnsType, "A", true) == 0)
                    {
                        matches = matches.Where(x => x.Words.Any(y => y.Key == "address" && y.Value == record.IP)).ToList();
                        if (matches.Count > 1)
                        {
                            throw new ApplicationException($"We found two static DNS A records from Mikrotik with the same IP ${record.IP}. We do not know how to process it.");
                        }
                    }
                    else
                    {
                        matches = matches.Where(x => x.Words.Any(y => y.Key == "cname" && y.Value == record.DnsCName)).ToList();
                        if (matches.Count > 1)
                        {
                            throw new ApplicationException($"We found two static DNS CNAME records from Mikrotik with the same CNAME ${record.DnsCName}. We do not know how to process it.");
                        }
                    }

                    if (matches.Count == 0)
                    {
                        CreateMikrotikDnsRecord(connection, record, options.Execute, options.ContinueOnErrors);
                    }
                    else
                    {
                        UpdateMikrotikDnsRecord(connection, matches[0], record, options.Execute, options.ContinueOnErrors);
                    }
                }

            }
        }

        private static void CreateMikrotikDnsRecord(ITikConnection connection, Record record, bool execute, bool continueOnErrors)
        {
            string[] sentence;
            if (string.Compare(record.DnsType, "A", true) == 0)
            {
                Console.WriteLine($"+Creating DNS A record. {record.GetDnsIdName()}: {record.GetDnsId()}, DnsType: {record.DnsType}, IP: {record.IP}");
                Log.Information($"Creating DNS A record. {record.GetDnsIdName()}: {{dns}}, DnsType: {{type}}, IP: {{address}}", record.GetDnsId(), record.DnsType, record.IP);
                sentence = new[]
                {
                    "/ip/dns/static/add",
                    $"={record.GetDnsIdField()}={record.GetDnsId()}",
                    $"=type=A",
                    $"=address={record.IP}",
                };
            }
            else
            {
                Console.WriteLine($"+Creating DNS A record. {record.GetDnsIdName()}: {record.GetDnsId()}, DnsType: {record.DnsType}, DnsСName: {record.DnsCName}");
                Log.Information($"Creating DNS A record. {record.GetDnsIdName()}: {{dns}}, DnsType: {{type}}, DnsCName: {{cname}}", record.GetDnsId(), record.DnsType, record.DnsCName);
                sentence = new[]
                {
                    "/ip/dns/static/add",
                    $"={record.GetDnsIdField()}={record.GetDnsId()}",
                    $"=type=CNAME",
                    $"=cname={record.DnsCName}",
                };
            }

            if (execute)
            {
                IEnumerable<ITikSentence> result = Mikrotik.CallMikrotik(connection, sentence);
                ProcessMikrotikResponse(continueOnErrors, result);
            }

        }

        private static void UpdateMikrotikDnsRecord(ITikConnection connection, ITikSentence existing, Record record, bool execute, bool continueOnErrors)
        {
            Dictionary<string, string> fieldsToUpdate = new Dictionary<string, string>();


            record.IP ??= "";
            record.DnsCName ??= "";
            record.DnsType ??= "";

            if (existing.Words[record.GetDnsIdField()] != record.GetDnsId())
            {
                fieldsToUpdate.Add(record.GetDnsIdField(), record.GetDnsId());
            }
            if (existing.Words["type"] != record.DnsType)
            {
                fieldsToUpdate.Add("type", record.DnsType);
            }

            if (string.Compare(record.DnsType, "A", true) == 0)
            {
                if (existing.Words["address"] != record.IP)
                {
                    fieldsToUpdate.Add("address", record.IP);
                }
            }
            else
            {
                if (existing.Words["cname"] != record.DnsCName)
                {
                    fieldsToUpdate.Add("cname", record.DnsCName);
                }
            }

            if (fieldsToUpdate.Count == 0)
            {
                if (string.Compare(record.DnsType, "A", true) == 0)
                {
                    Console.WriteLine($"=DNS A record already exist. {record.GetDnsIdName()}: {record.GetDnsId()}, DnsType: {record.DnsType}, IP: {record.IP}");
                    Log.Information($"DNS A record already exist. {record.GetDnsIdName()}: {{dns}}, DnsType: {{type}}, IP: {{address}}", record.GetDnsId(), record.DnsType, record.IP);
                }
                else
                {
                    Console.WriteLine($"=DNS CNAME record already exist. {record.GetDnsIdName()}: {record.GetDnsId()}, DnsType: {record.DnsType}, DnsСName: {record.DnsCName}");
                    Log.Information($"DNS CNAME record already exist. {record.GetDnsIdName()}: {{dns}}, DnsType: {{type}}, DnsCName: {{cname}}", record.GetDnsId(), record.DnsType, record.DnsCName);
                }
                return;
            }
            throw new ApplicationException("bla");
        }

        private static void ProcessDhcpRecords(ImportOptions options, ITikConnection connection, IEnumerable<ITikSentence> dhcp, List<Record> dhcpRecords)
        {

            foreach (Record record in dhcpRecords)
            {
                List<ITikSentence>? ipMatches = dhcp.Where(x => x.Words.Any(y => y.Key == "address" && y.Value == record.IP))
                    .Where(x => x.Words.Any(y => y.Key == "dynamic" && y.Value == "false"))
                    .Where(x => x.Words.Any(y => y.Key == "disabled" && y.Value == "false"))
                    .ToList();
                List<ITikSentence>? macMatches = dhcp.Where(x => x.Words.Any(y => y.Key == "mac-address" && y.Value == record.Mac))
                    .Where(x => x.Words.Any(y => y.Key == "dynamic" && y.Value == "false"))
                    .Where(x => x.Words.Any(y => y.Key == "disabled" && y.Value == "false"))
                    .ToList();

                if (ipMatches.Count > 1)
                {
                    throw new ApplicationException($"We found two static DHCP records from Mikrotik with the same IP ${record.IP}. We do not know how to process it.");
                }
                if (macMatches.Count > 1)
                {
                    throw new ApplicationException($"We found two static DHCP records from Mikrotik with the same MAC ${record.Mac}. We do not know how to process it.");
                }

                if (ipMatches.Count == 1 && macMatches.Count == 1)
                {
                    if (ipMatches[0] == macMatches[0])
                    {
                        UpdateMikrotikDhcpRecord(connection, ipMatches[0], record, options.Execute, options.ContinueOnErrors);
                    }
                    else
                    {
                        string message = $"DHCP record IP {record.IP}, MAC {record.Mac} is ignored. Clashes with records: IP {ipMatches[0].Words["address"]}, MAC {ipMatches[0].Words["mac-address"]} and IP {macMatches[0].Words["address"]}, MAC {macMatches[0].Words["mac-address"]}";
                        Log.Warning(message);
                        Console.WriteLine($"?Warning: message");
                    }
                }

                if (ipMatches.Count == 1 && macMatches.Count == 0)
                {
                    UpdateMikrotikDhcpRecord(connection, ipMatches[0], record, options.Execute, options.ContinueOnErrors);
                }
                if (ipMatches.Count == 0 && macMatches.Count == 1)
                {
                    UpdateMikrotikDhcpRecord(connection, macMatches[0], record, options.Execute, options.ContinueOnErrors);
                }
                if (ipMatches.Count == 0 && macMatches.Count == 0)
                {
                    CreateMikrotikDhcpRecord(connection, record, options.Execute, options.ContinueOnErrors);
                }
            }
        }


        private static void CreateMikrotikDhcpRecord(ITikConnection connection, Record record, bool execute, bool continueOnErrors)
        {
            Console.WriteLine($"+Creating DHCP record. IP {record.IP}, MAC {record.Mac}, Label {record.DhcpLabel}, Server {record.DhcpServer}");
            Log.Information("Creating DHCP record. IP {IP}, MAC {MAC}, Label {Label}, Server {Server}", record.IP, record.Mac, record.DhcpLabel, record.DhcpServer);

            if (string.IsNullOrWhiteSpace(record.DhcpLabel))
            {
                record.DhcpLabel = record.DnsHostName;
            }

            string[] sentence = new[]
            {
                "/ip/dhcp-server/lease/add",
                $"=address={record.IP}",
                $"=mac-address={record.Mac}",
                $"=comment={record.DhcpLabel}",
                $"=server={record.DhcpServer}",
                "=use-src-mac=true",
            };

            if (execute)
            {
                IEnumerable<ITikSentence> result = Mikrotik.CallMikrotik(connection, sentence);
                ProcessMikrotikResponse(continueOnErrors, result);
            }
        }

        private static void UpdateMikrotikDhcpRecord(ITikConnection connection, ITikSentence existing, Record record, bool execute, bool continueOnErrors)
        {
            Dictionary<string, string> fieldsToUpdate = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(record.DhcpLabel))
            {
                record.DhcpLabel = record.DnsHostName;
            }

            record.IP ??= "";
            record.Mac ??= "";
            record.DhcpLabel ??= "";
            record.DhcpServer ??= "";

            if (existing.Words["address"] != record.IP)
            {
                fieldsToUpdate.Add("address", record.IP);
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
                Console.WriteLine($"=DHCP record already exist. IP {record.IP}, MAC {record.Mac}, Label {record.DhcpLabel}, Server {record.DhcpServer}");
                Log.Information("DHCP record already exist. IP {IP}, MAC {MAC}, Label {Label}, Server {Server}", record.IP, record.Mac, record.DhcpLabel, record.DhcpServer);
                return;
            }

            Console.WriteLine($"^Updating DHCP record. IP {record.IP}, MAC {record.Mac}, Label {record.DhcpLabel}, Server {record.DhcpServer}");
            Log.Information("Updating DHCP record. IP {IP}, MAC {MAC}, Label {Label}, Server {Server}", record.IP, record.Mac, record.DhcpLabel, record.DhcpServer);
            foreach (KeyValuePair<string, string> kvp in fieldsToUpdate)
            {
                Console.WriteLine($">{kvp.Key}: {existing.Words[kvp.Key]} => {kvp.Value}");
                Log.Information("{field}: {oldValue} => {newValue}", kvp.Key, existing.Words[kvp.Key], kvp.Value);
            }

            string[] sentence = new[] { "/ip/dhcp-server/lease/set" }
                .Concat(fieldsToUpdate.Select(x => $"={x.Key}={x.Value}"))
                .Concat(new[] { $"=.id={existing.Words[".id"]}" })
                .ToArray();
            if (execute)
            {
                IEnumerable<ITikSentence> result = Mikrotik.CallMikrotik(connection, sentence);
                ProcessMikrotikResponse(continueOnErrors, result);
            }
        }

        private static void ProcessMikrotikResponse(bool continueOnErrors, IEnumerable<ITikSentence> result)
        {
            foreach (var responseSentence in result)
            {
                if (responseSentence is ITikTrapSentence trap)
                {
                    Console.Error.WriteLine($"!Error: {trap.Message}");
                    Log.Error(trap.Message);
                    if (!continueOnErrors)
                    {
                        throw new MktoolException("Error", ExitCode.MikrotikWriteError);
                    }
                }
            }
        }

        private static bool ValidateDnsRecords(List<Record> dnsRecords)
        {
            Log.Information("Validating DNS records");
            bool valid = true;
            foreach(Record record in dnsRecords)
            {
                if (string.IsNullOrWhiteSpace(record.DnsHostName) && string.IsNullOrWhiteSpace(record.DnsRegexp))
                {
                    valid = false;
                    string text = Toml.WriteString(record.SetEmptyPropertiesToNull());
                    Console.Error.WriteLine($"Error: Both DnsHostName and DnsRegexp are empty in record");
                    Console.Error.Write(text);
                    Log.Error("Both DnsHostName and DnsRegexp are empty in record {@record}", record);
                }
                if (!string.IsNullOrWhiteSpace(record.DnsHostName) && !string.IsNullOrWhiteSpace(record.DnsRegexp))
                {
                    valid = false;
                    Console.Error.WriteLine($"Error: Both DnsHostName {record.DnsHostName} and DnsRegexp '{record.DnsRegexp}' are present in a single record");
                    Log.Error("Both DnsHostName {hostName} and DnsRegexp '{regexp}' are present in a single record", record.DnsHostName, record.DnsRegexp);
                }
                if (!string.IsNullOrWhiteSpace(record.DnsType) && string.Compare(record.DnsType, "A", true) != 0 && string.Compare(record.DnsType, "CNAME", true) != 0)
                {
                    valid = false;
                    Console.Error.WriteLine($"Error: record type is not 'A' or 'CNAME': '{record.DnsType}', DnsId: {record.GetDnsId()}");
                    Log.Error("Record type is not 'A' or 'CNAME': '{dnsType}', DnsId: {dns}", record.DnsType, record.GetDnsId());
                }
                if (string.Compare(record.DnsType, "A", true) == 0 && string.IsNullOrEmpty(record.IP))
                {
                    valid = false;
                    Console.Error.WriteLine($"Error: 'A' record must have IP address. {record.GetDnsIdName()}: {record.GetDnsId()}");
                    Log.Error($"'A' record must have IP address. {record.GetDnsIdName()}: {{dns}}", record.GetDnsId());
                }
                if (string.Compare(record.DnsType, "CNAME", true) == 0 && string.IsNullOrEmpty(record.DnsCName))
                {
                    valid = false;
                    Console.Error.WriteLine($"Error: 'CNAME' record must have CNAME. {record.GetDnsIdName()}: {record.GetDnsId()}");
                    Log.Error($"'ACNAME record must have CNAME. {record.GetDnsIdName()}: {{dns}}", record.GetDnsId());
                }
            }
            return valid;
        }

        private static bool ValidateDhcpRecords(List<Record> dhcpRecords)
        {
            // We mainly rely on Mikrotik itself to tell us if something wrong with the data
            // This vadation to avoid a situation where where keep overwriting the same record with different data
            // Mikrotik would be non the wiser
            Log.Information("Validating DHCP records");
            bool valid = true;
            List<string?> nonUniqueIPs = dhcpRecords.GroupBy(x => x.IP).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
            if (nonUniqueIPs.Count > 0)
            {
                valid = false;
                Console.Error.WriteLine("Your export file contains DHCP records with duplicate IP addresses:");
                foreach(string? line in nonUniqueIPs)
                {
                    Console.Error.WriteLine($"'{line}'");
                }
            }
            List<string?> nonUniqueMACs = dhcpRecords.GroupBy(x => x.Mac).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
            if (nonUniqueMACs.Count > 0)
            {
                valid = false;
                Console.Error.WriteLine("Your export file contains DHCP records with duplicate MAC addresses:");
                foreach (string? line in nonUniqueMACs)
                {
                    Console.Error.WriteLine($"'{line}'");
                }
            }
            return valid;
        }

        private static List<Record> ReadRecords(string fileName, string format)
        {
            Log.Information("Reading export file");
            switch (format)
            {
                case "csv":
                    return ReadCsvExport(fileName);
                case "toml":
                    return ReadTomlExport(fileName);
                case "yaml":
                    return ReadYamlExport(fileName);
                case "json":
                    return ReadJsonExport(fileName);
                default:
                    throw new ApplicationException($"Unexpected file format {format}");
            }
        }

        private static List<Record> ReadJsonExport(string fileName)
        {
            List<Record> result;
            try
            { 
                result = JsonConvert.DeserializeObject<List<Record>>(File.ReadAllText(fileName));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                throw new MktoolException("Error", ExitCode.ImportFileError);
            }
            Log.Verbose("Json deserialization result: {@result}", result);
            return result;
                
        }

        private static List<Record> ReadYamlExport(string fileName)
        {
            List<Record> result;
            try
            {
                IDeserializer? deserializer = new DeserializerBuilder().Build();
                result = deserializer.Deserialize<List<Record>>(File.ReadAllText(fileName));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                throw new MktoolException("Error", ExitCode.ImportFileError);
            }
            Log.Verbose("Yaml deserialization result: {@result}", result);
            return result;
        }

        private static List<Record> ReadTomlExport(string fileName)
        {
            List<Record> result;
            try
            {
                result = Toml.ReadFile<TomlWrapper>(fileName).Record.ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                throw new MktoolException("Error", ExitCode.ImportFileError);
            }
            Log.Verbose("Toml deserialization result: {@result}", result);
            return result;
        }

        private static List<Record> ReadCsvExport(string fileName)
        {
            List<Record> result;
            try
            {
                using (StreamReader? reader = new StreamReader(fileName))
                using (CsvReader? csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    result = csv.GetRecords<Record>().ToList();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                throw new MktoolException("Error", ExitCode.ImportFileError);
            }
            Log.Verbose("Csv deserialization result: {@result}", result);
            return result;

        }

        private static string GetFormatFromExtension(string extension)
        {
            // remove leading dot
            extension = extension.Substring(1);
            string[] supportedExtensions = { "csv", "toml", "yaml", "yml", "json" };
            if (!supportedExtensions.Contains(extension))
            {
                Console.Error.WriteLine("You have not specified import format, and the file you specified does not have any of the supported format extenstions");
                throw new MktoolException("Error", ExitCode.MissingFormat);
            }
            return extension == "yml" ? "yaml" : extension;
        }
    }
}
