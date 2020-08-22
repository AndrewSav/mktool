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

            options.Format ??= GetFormatFromExtension(options.File.Extension);

            List<Record> records = ReadRecords(options.File.FullName, options.Format);

            List<Record> dhcpRecords = records.Where(x => x.HasDhcp).ToList();
            List<Record> dnsRecords = records.Where(x => x.HasDns).ToList();
            List<Record> wifiRecords = records.Where(x => x.HasWiFi).ToList();
            if (!ValidateDhcpRecords(dhcpRecords) || !ValidateDnsRecords(dnsRecords))
            {
                throw new MktoolException(ExitCode.ValidationError);
            }

            ITikConnection connection = await Mikrotik.ConnectAsync(options);

            List<ITikSentence> dhcp = Mikrotik.GetDhcpRecords(connection).ToList();
            ProcessDhcpRecords(options, connection, dhcp, dhcpRecords);

            List<ITikSentence> dns = Mikrotik.GetDnsRecords(connection).ToList();
            ProcessDnsRecords(options, connection, dns, dnsRecords);

            List<ITikSentence> wifi = Mikrotik.GetWifiRecords(connection).ToList();
            ProcessWiFiRecords(options, connection, wifi, wifiRecords);
        }

        private static void ProcessWiFiRecords(ImportOptions options, ITikConnection connection, List<ITikSentence> wifi, List<Record> wifiRecords)
        {

            foreach (Record record in wifiRecords)
            {
                List<ITikSentence> matches = wifi.Where(x => x.Words.Any(y => y.Key == "mac-address" && y.Value == record.Mac))
                    .Where(x => x.Words.Any(y => y.Key == "disabled" && y.Value == "false"))
                    .ToList();

                if (matches.Count > 1)
                {
                    string message = $"There are {matches.Count} wifi records with this MAC. Update not attempted";
                    if (!options.SkipExisting)
                    {
                        Console.WriteLine($"=Wifi record already exist. MAC: {record.Mac}, DnsHostName: {record.DnsHostName}");
                        Console.WriteLine("?Warning: message");
                    }
                    Log.Information("Wifi record already exist. MAC: {mac}, DnsHostName: {dns}", record.Mac, record.DnsHostName);
                    Log.Warning(message);
                    continue;
                }
                if (matches.Count == 0)
                {
                    Mikrotik.CreateMikrotikWifiRecord(GetMikrotikOptions(options), connection, record);
                }
                else
                {
                    Mikrotik.UpdateMikrotikWifiRecord(GetMikrotikOptions(options), connection, matches[0], record);

                }
            }
        }


        private static void ProcessDnsRecords(ImportOptions options, ITikConnection connection, List<ITikSentence> dns, List<Record> dnsRecords)
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
                    Mikrotik.CreateMikrotikDnsRecord(GetMikrotikOptions(options), connection, record);
                }
                else
                {
                    if (string.Equals(record.DnsType, "A", StringComparison.OrdinalIgnoreCase))
                    {
                        matches = matches.Where(x => x.Words.Any(y => y.Key == "address" && y.Value == record.Ip)).ToList();
                        if (matches.Count > 1)
                        {
                            throw new ApplicationException($"We found two static DNS A records from Mikrotik with the same IP ${record.Ip}. We do not know how to process it.");
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
                        Mikrotik.CreateMikrotikDnsRecord(GetMikrotikOptions(options), connection, record);
                    }
                    else
                    {
                        ReportDnsRecordExists(options, record);
                    }
                }
            }
        }


        private static void ReportDnsRecordExists(ImportOptions options, Record record)
        {
            if (string.Equals(record.DnsType, "A", StringComparison.OrdinalIgnoreCase))
            {
                if (!options.SkipExisting)
                {
                    Console.WriteLine($"=DNS A record already exist. {record.GetDnsIdName()}: {record.GetDnsId()}, DnsType: {record.DnsType}, IP: {record.Ip}");
                }
                Log.Information($"DNS A record already exist. {record.GetDnsIdName()}: {{dns}}, DnsType: {{type}}, IP: {{address}}", record.GetDnsId(), record.DnsType, record.Ip);
            }
            else
            {
                if (!options.SkipExisting)
                {
                    Console.WriteLine($"=DNS CNAME record already exist. {record.GetDnsIdName()}: {record.GetDnsId()}, DnsType: {record.DnsType}, DnsСName: {record.DnsCName}");
                }
                Log.Information($"DNS CNAME record already exist. {record.GetDnsIdName()}: {{dns}}, DnsType: {{type}}, DnsCName: {{cname}}", record.GetDnsId(), record.DnsType, record.DnsCName);
            }
        }

        private static void ProcessDhcpRecords(ImportOptions options, ITikConnection connection, List<ITikSentence> dhcp, List<Record> dhcpRecords)
        {

            foreach (Record record in dhcpRecords)
            {
                List<ITikSentence> ipMatches = dhcp.Where(x => x.Words.Any(y => y.Key == "address" && y.Value == record.Ip))
                    .Where(x => x.Words.Any(y => y.Key == "dynamic" && y.Value == "false"))
                    .Where(x => x.Words.Any(y => y.Key == "disabled" && y.Value == "false"))
                    .ToList();
                List<ITikSentence> macMatches = dhcp.Where(x => x.Words.Any(y => y.Key == "mac-address" && y.Value == record.Mac))
                    .Where(x => x.Words.Any(y => y.Key == "dynamic" && y.Value == "false"))
                    .Where(x => x.Words.Any(y => y.Key == "disabled" && y.Value == "false"))
                    .ToList();

                if (ipMatches.Count > 1)
                {
                    throw new ApplicationException($"We found two static DHCP records from Mikrotik with the same IP ${record.Ip}. We do not know how to process it.");
                }
                if (macMatches.Count > 1)
                {
                    throw new ApplicationException($"We found two static DHCP records from Mikrotik with the same MAC ${record.Mac}. We do not know how to process it.");
                }

                if (ipMatches.Count == 1 && macMatches.Count == 1)
                {
                    if (ipMatches[0] == macMatches[0])
                    {
                        Mikrotik.UpdateMikrotikDhcpRecord(GetMikrotikOptions(options), connection, ipMatches[0], record);
                    }
                    else
                    {
                        string message = $"DHCP record IP {record.Ip}, MAC {record.Mac} is ignored. Clashes with records: IP {ipMatches[0].Words["address"]}, MAC {ipMatches[0].Words["mac-address"]} and IP {macMatches[0].Words["address"]}, MAC {macMatches[0].Words["mac-address"]}";
                        Log.Warning(message);
                        Console.WriteLine("?Warning: message");
                    }
                }

                if (ipMatches.Count == 1 && macMatches.Count == 0)
                {
                    Mikrotik.UpdateMikrotikDhcpRecord(GetMikrotikOptions(options), connection, ipMatches[0], record);
                }
                if (ipMatches.Count == 0 && macMatches.Count == 1)
                {
                    Mikrotik.UpdateMikrotikDhcpRecord(GetMikrotikOptions(options), connection, macMatches[0], record);
                }
                if (ipMatches.Count == 0 && macMatches.Count == 0)
                {
                    Mikrotik.CreateMikrotikDhcpRecord(GetMikrotikOptions(options), connection, record);
                    List<ITikSentence> dynamicMatches = dhcp.Where(x => x.Words.Any(y => y.Key == "mac-address" && string.Equals(y.Value,record.Mac,StringComparison.OrdinalIgnoreCase)))
                        .Where(x => x.Words.Any(y => y.Key == "dynamic" && y.Value == "true"))
                        .Where(x => x.Words.Any(y => y.Key == "disabled" && y.Value == "false"))
                        .ToList();
                    foreach (ITikSentence? dyn in dynamicMatches)
                    {
                        Mikrotik.DeleteMikrotikDhcpRecord(GetMikrotikOptions(options), connection, dyn);
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
                    Console.Error.WriteLine("Error: Both DnsHostName and DnsRegexp are empty in record");
                    Console.Error.Write(text);
                    Log.Error("Both DnsHostName and DnsRegexp are empty in record {@record}", record);
                }
                if (!string.IsNullOrWhiteSpace(record.DnsHostName) && !string.IsNullOrWhiteSpace(record.DnsRegexp))
                {
                    valid = false;
                    Console.Error.WriteLine($"Error: Both DnsHostName {record.DnsHostName} and DnsRegexp '{record.DnsRegexp}' are present in a single record");
                    Log.Error("Both DnsHostName {hostName} and DnsRegexp '{regexp}' are present in a single record", record.DnsHostName, record.DnsRegexp);
                }
                if (!string.IsNullOrWhiteSpace(record.DnsType) && !string.Equals(record.DnsType, "A", StringComparison.OrdinalIgnoreCase) && String.Compare(record.DnsType, "CNAME", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    valid = false;
                    Console.Error.WriteLine($"Error: record type is not 'A' or 'CNAME': '{record.DnsType}', DnsId: {record.GetDnsId()}");
                    Log.Error("Record type is not 'A' or 'CNAME': '{dnsType}', DnsId: {dns}", record.DnsType, record.GetDnsId());
                }
                if (string.Equals(record.DnsType, "A", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(record.Ip))
                {
                    valid = false;
                    Console.Error.WriteLine($"Error: 'A' record must have IP address. {record.GetDnsIdName()}: {record.GetDnsId()}");
                    Log.Error($"'A' record must have IP address. {record.GetDnsIdName()}: {{dns}}", record.GetDnsId());
                }
                if (string.Equals(record.DnsType, "CNAME", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(record.DnsCName))
                {
                    valid = false;
                    Console.Error.WriteLine($"Error: 'CNAME' record must have CNAME. {record.GetDnsIdName()}: {record.GetDnsId()}");
                    Log.Error($"Error: 'CNAME' record must have CNAME. {record.GetDnsIdName()}: {{dns}}", record.GetDnsId());
                }
            }
            return valid;
        }

        private static bool ValidateDhcpRecords(List<Record> dhcpRecords)
        {
            // We mainly rely on Mikrotik itself to tell us if something wrong with the data
            // This validation is to avoid a situation where we keep overwriting the same record with different data
            // Mikrotik would be non the wiser
            Log.Information("Validating DHCP records");
            bool valid = true;
            List<string?> nonUniqueIPs = dhcpRecords.GroupBy(x => x.Ip).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
            if (nonUniqueIPs.Count > 0)
            {
                valid = false;
                Console.Error.WriteLine("Your export file contains DHCP records with duplicate IP addresses:");
                foreach(string? line in nonUniqueIPs)
                {
                    Console.Error.WriteLine($"'{line}'");
                }
            }
            List<string?> nonUniqueMacs = dhcpRecords.GroupBy(x => x.Mac).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
            if (nonUniqueMacs.Count > 0)
            {
                valid = false;
                Console.Error.WriteLine("Your export file contains DHCP records with duplicate MAC addresses:");
                foreach (string? line in nonUniqueMacs)
                {
                    Console.Error.WriteLine($"'{line}'");
                }
            }
            return valid;
        }

        private static List<Record> ReadRecords(string fileName, string format)
        {
            Log.Information("Reading export file");
            return format switch
            {
                "csv" => ReadCsvExport(fileName),
                "toml" => ReadTomlExport(fileName),
                "yaml" => ReadYamlExport(fileName),
                "json" => ReadJsonExport(fileName),
                _ => throw new ApplicationException($"Unexpected file format {format}"),
            };
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
                Console.Error.WriteLine($"Error: Cannot deserialize '{fileName}'. {ex.Message}");
                throw new MktoolException(ExitCode.ImportFileError);
            }
            Log.Verbose("Json deserialization result: {@result}", result);
            return result;
                
        }

        private static List<Record> ReadYamlExport(string fileName)
        {
            List<Record> result;
            try
            {
                IDeserializer deserializer = new DeserializerBuilder().Build();
                result = deserializer.Deserialize<List<Record>>(File.ReadAllText(fileName));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Cannot deserialize '{fileName}'. {ex.Message}");
                throw new MktoolException( ExitCode.ImportFileError);
            }
            Log.Verbose("Yaml deserialization result: {@result}", result);
            return result;
        }

        private static List<Record> ReadTomlExport(string fileName)
        {
            Record[]? result;
            try
            {
                result = Toml.ReadFile<RecordTomlWrapper>(fileName).Record;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Cannot deserialize '{fileName}'. {ex.Message}");
                throw new MktoolException( ExitCode.ImportFileError);
            }
            if (result == null)
            {
                Console.Error.WriteLine($"Error: Cannot find records in '{fileName}'.");
                throw new MktoolException(ExitCode.ImportFileError);
            }
            Log.Verbose("Toml deserialization result: {@result}", result);
            return result.ToList();
        }

        private static List<Record> ReadCsvExport(string fileName)
        {
            List<Record> result;
            try
            {
                using StreamReader reader = new StreamReader(fileName);
                using CsvReader csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                result = csv.GetRecords<Record>().ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Cannot deserialize '{fileName}'. {ex.Message}");
                throw new MktoolException(ExitCode.ImportFileError);
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
                Console.Error.WriteLine("You have not specified import format, and the file you specified does not have any of the supported format extensions");
                throw new MktoolException( ExitCode.MissingFormat);
            }
            return extension == "yml" ? "yaml" : extension;
        }
        private static MikrotikOptions GetMikrotikOptions(ImportOptions options)
        {
            return new MikrotikOptions
            {
                ContinueOnErrors = options.ContinueOnErrors,
                Execute = options.Execute,
                LogToStdout = true,
                SkipExisting = options.SkipExisting
            };
        }
    }
}
