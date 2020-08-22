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
    static class Export
    {
        public static async Task Execute(ExportOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("Export command started");
            Log.Debug("Parameters: {@params}", options);

            ITikConnection connection = await Mikrotik.ConnectAsync(options);

            IEnumerable<ITikSentence> dhcp = Mikrotik.GetDhcpRecords(connection);
            List<Record> result = ProcessDhcpRecords(dhcp);
            IEnumerable<ITikSentence> dns = Mikrotik.GetDnsRecords(connection);
            MergeDnsRecords(result, dns);
            IEnumerable<ITikSentence> wifi = Mikrotik.GetWifiRecords(connection);
            MergeWiFiRecords(result, wifi);

            List<Record> sorted = SortRecords(result);
            TextWriter output = CreateOutputWriter(options.File?.FullName);
            Debug.Assert(options.Format != null);
            WriteOutput(options.File?.FullName, options.Format, sorted, output);

        }

        private static void WriteOutput(string? fileName, string format, List<Record> sorted, TextWriter output)
        {
            Log.Information("Writng output");
            switch (format)
            {
                case "csv":
                    WriteCsvExport(output, sorted);
                    break;
                case "toml":
                    WriteTomlExportWrapper(fileName, sorted, output);
                    break;
                case "yaml":
                    WriteYamlExport(output, sorted);
                    break;
                case "json":
                    WriteJsonExport(output, sorted);
                    break;
                default:
                    throw new ApplicationException($"Unexpected file format {format}");
            }
        }
        private static TextWriter CreateOutputWriter(string? fileName)
        {
            Log.Information("Opening output");
            TextWriter output;
            if (fileName == null)
            {
                output = Console.Out;
            }
            else
            {
                try
                {
                    output = new StreamWriter(fileName);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: Cannot write to {fileName}. {ex.Message}");
                    throw new MktoolException( ExitCode.FileWriteError);
                }
            }

            return output;
        }

        private static List<Record> SortRecords(List<Record> result)
        {
            Log.Debug("Sorting");
            IEnumerable<Record>? withIp = result.Where(r => r.IP != null);
            IEnumerable<Record>? withoutIp = result.Where(r => r.IP == null);
            List<Record>? sorted = withIp.OrderBy(r => { Debug.Assert(r.IP != null); return Version.Parse(r.IP); }).Concat(withoutIp).ToList();
            return sorted;
        }

        private static void MergeWiFiRecords(List<Record> result, IEnumerable<ITikSentence> wifi)
        {
            foreach (ITikSentence item in wifi)
            {
                if (!item.Words.ContainsKey(".id"))
                {
                    Log.Verbose("Tik WiFi record discraded: {@ITikSentence}", item);
                    continue;
                }

                Log.Verbose("Tik WiFi record processing: {@ITikSentence}", item);
                List<Record> matches = result.Where(r => string.Equals(r.Mac, item.Words["mac-address"], StringComparison.OrdinalIgnoreCase)).ToList();
                Log.Verbose("Matches found: {@records}", matches);
                if (matches.Count > 1)
                {
                    throw new ApplicationException($"We found two static DHCP records from Mikrotik with the same MAC address {item.Words["mac-address"]}. We do not know how to process it.");
                }
                if (matches.Count == 0)
                {
                    Record record = new Record
                    {
                        DnsHostName = item.Words["comment"],
                        Mac = item.Words["mac-address"],
                        HasWiFi = true
                    };
                    result.Add(record);
                    Log.Verbose("Mktool record added: {@record}", record);
                }
                else
                {
                    Record record = matches[0];
                    record.HasWiFi = true;
                    Log.Verbose("Mktool record updated: {@record}", record);
                }
            }
        }

        private static void MergeDnsRecords(List<Record> result, IEnumerable<ITikSentence> dns)
        {
            foreach (ITikSentence item in dns)
            {
                if (!item.Words.ContainsKey("dynamic") || (item.Words["dynamic"] != "false") || ((item.Words["type"] != "A" && (item.Words["type"] != "CNAME"))))
                {
                    Log.Verbose("Tik dns record discraded: {@ITikSentence}", item);
                    continue;
                }

                if (!item.Words.ContainsKey("name") && !item.Words.ContainsKey("regexp"))
                {
                    throw new ApplicationException("We received a DNS record from Mikrotik that has neither name nor regexp field. We do not know how to process it.");
                }

                if (item.Words["type"] == "A")
                {
                    Log.Verbose("Tik dns record processing (type A): {@ITikSentence}", item);
                    List<Record> matches = result.Where(r => string.Equals(r.IP, item.Words["address"], StringComparison.OrdinalIgnoreCase)).ToList();
                    Log.Verbose("Matches found: {@records}", matches);
                    if (matches.Count > 1)
                    {
                        throw new ApplicationException($"We found two static DHCP records from Mikrotik with the same IP ${item.Words["address"]}. We do not know how to process it.");
                    }
                    if (matches.Count == 0 || (item.Words.ContainsKey("regexp")))
                    {
                        Record record = new Record
                        {
                            IP = item.Words["address"],
                            DnsType = "A",
                            HasDns = true
                        };
                        result.Add(record);
                        ApplyName(item, record);
                        Log.Verbose("Mktool record added: {@record}", record);
                    }
                    else
                    {
                        Record record = matches[0];
                        record.HasDns = true;
                        record.DnsType = "A";
                        ApplyName(item, record);
                        Log.Verbose("Mktool record updated: {@record}", record);
                    }
                }
                if (item.Words["type"] == "CNAME")
                {
                    Log.Verbose("Tik dns record processing (type CNAME): {@ITikSentence}", item);
                    Record record = new Record
                    {
                        DnsCName = item.Words["cname"],
                        DnsType = "CNAME",
                        HasDns = true
                    };
                    result.Add(record);
                    ApplyName(item, record);
                    Log.Verbose("Mktool record added: {@record}", record);
                }
            }
        }

        private static List<Record> ProcessDhcpRecords(IEnumerable<ITikSentence> dhcp)
        {
            List<Record> result = new List<Record>();

            foreach (ITikSentence item in dhcp)
            {
                if (!item.Words.ContainsKey("dynamic") || (item.Words["dynamic"] != "false"))
                {
                    Log.Verbose("Tik dhcp record discraded: {@ITikSentence}", item);
                    continue;
                }

                Log.Verbose("Tik dhcp record processing: {@ITikSentence}", item);
                Record record = new Record
                {
                    IP = item.Words["address"],
                    DhcpLabel = item.Words["comment"],
                    Mac = item.Words["mac-address"],
                    DhcpServer = item.Words["server"],
                    HasDhcp = true
                };
                result.Add(record);
                Log.Verbose("Mktool record added: {@record}", record);
            }

            return result;
        }

        private static void WriteJsonExport(TextWriter output, List<Record> sorted)
        {
            JsonSerializer serializer = new JsonSerializer
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
            using JsonWriter writer = new JsonTextWriter(output);
            serializer.Serialize(writer, sorted);
            output.Flush();
        }

        private static void WriteYamlExport(TextWriter output, List<Record> sorted)
        {
            YamlDotNet.Serialization.ISerializer? serializer = new SerializerBuilder().Build();
            serializer.Serialize(output, sorted);
            output.Flush();
        }

        private static void WriteTomlExportWrapper(string? fileName, List<Record> sorted, TextWriter output)
        {
            Stream stream;
            if (fileName == null)
            {
                RecordTomlWrapper t = new RecordTomlWrapper { Record = sorted.ToArray() };
                Console.Write(Toml.WriteString(t));
            }
            else
            {
                try
                {
                    output.Close();
                    stream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: Cannot write to {fileName}. {ex.Message}");
                    throw new MktoolException( ExitCode.FileWriteError);
                }
                WriteTomlExport(stream, sorted.ToArray());
            }
        }

        private static void WriteTomlExport(Stream stream, Record[] sorted)
        {
            RecordTomlWrapper t = new RecordTomlWrapper { Record = sorted };
            Toml.WriteStream(t, stream);
            stream.Flush();
        }

        private static void WriteCsvExport(TextWriter output, List<Record> sorted)
        {
            using CsvWriter? csv = new CsvWriter(output, CultureInfo.InvariantCulture);
            csv.WriteRecords(sorted);
        }

        private static void ApplyName(ITikSentence item, Record record)
        {
            if (item.Words.ContainsKey("name"))
            {
                record.DnsHostName = item.Words["name"];
            }
            else
            {
                record.DnsRegexp = item.Words["regexp"];
            }
        }
    }
}
