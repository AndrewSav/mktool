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
        class TomlWrapper
        {
            public Record[]? Record { get; set; }
        }

        public static async Task<int> Execute(ExportOptions options)
        {
            if(!LoggingHelper.ConfigureLogging(options.LogLevel)) { return (int)ExitCode.LoggingInitError; }
            TextWriter errorWriter = Console.Error;
            Log.Information("Export command started");
            Log.Debug("Parameters: {@params}", options);

            (string username, string password, int code) = await CredentialsHelper.GetUsernameAndPassword(options);
            if (code != 0)
            {
                return code;
            }

            Debug.Assert(options.Address != null);

            Log.Information("Connecting to mikrotik");
            ITikConnection? connection;
            try
            {
                connection = ConnectionFactory.OpenConnection(TikConnectionType.Api, options.Address, username, password);
            } 
            catch (Exception ex)
            {
                errorWriter.WriteLine(ex);
                return (int)ExitCode.MikrotikConnectionError;
            }

            Debug.Assert(connection != null);

            Log.Information("Retreiving DHCP");
            IEnumerable<ITikSentence> dhcp;
            try
            {
                dhcp = connection.CallCommandSync(new[] { "/ip/dhcp-server/lease/print" });
            }
            catch (Exception ex)
            {
                errorWriter.WriteLine(ex);
                return (int)ExitCode.MikrotikConnectionError;
            }

            Log.Verbose("Dhcp response: {@response}", dhcp);
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

            Log.Information("Retreiving DNS");
            IEnumerable<ITikSentence> dns;
            try
            {
                dns = connection.CallCommandSync(new[] { "/ip/dns/static/print" });
            }
            catch (Exception ex)
            {
                errorWriter.WriteLine(ex);
                return (int)ExitCode.MikrotikConnectionError;
            }

            Log.Verbose("Dns response: {@response}", dns);

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
                    List<Record> matches = result.Where(r => string.Compare(r.IP, item.Words["address"], true) == 0).ToList();
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

            Log.Information("Retreiving WiFi");
            IEnumerable<ITikSentence> WiFi;
            try
            {
                WiFi = connection.CallCommandSync(new[] { "/interface/wireless/access-list/print" });
            }
            catch (Exception ex)
            {
                errorWriter.WriteLine(ex);
                return (int)ExitCode.MikrotikConnectionError;
            }

            Log.Verbose("WiFi response: {@response}", WiFi);

            foreach (ITikSentence item in WiFi)
            {
                if (!item.Words.ContainsKey(".id"))
                {
                    Log.Verbose("Tik WiFi record discraded: {@ITikSentence}", item);
                    continue;
                }

                Log.Verbose("Tik WiFi record processing: {@ITikSentence}", item);
                List<Record> matches = result.Where(r => string.Compare(r.Mac, item.Words["mac-address"], true) == 0).ToList();
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

            Log.Debug("Sorting");

            IEnumerable<Record>? withIp = result.Where(r => r.IP != null);
            IEnumerable<Record>? withoutIp = result.Where(r => r.IP == null);
            List<Record>? sorted = withIp.OrderBy(r => { Debug.Assert(r.IP != null); return Version.Parse(r.IP); }).Concat(withoutIp).ToList();

            Log.Information("Opening output");
            TextWriter output;
            if (options.File == null)
            {
                output = Console.Out;
            }
            else
            {
                try
                {
                    output = new StreamWriter(options.File.FullName);
                }
                catch(Exception ex)
                {
                    errorWriter.WriteLine(ex.Message);
                    return (int)ExitCode.FileWriteError;
                }
            }

            Log.Information("Writng output");
            switch (options.Format)
            {
                case "csv":
                    WriteCsvExport(output, sorted);
                    break;
                case "toml":
                    Stream stream;
                    if (options.File == null)
                    {
                        TomlWrapper t = new TomlWrapper { Record = sorted.ToArray() };
                        Console.Write(Toml.WriteString(t));
                    }
                    else
                    {
                        try
                        {
                            output.Close();
                            stream = new FileStream(options.File.FullName, FileMode.Create, FileAccess.Write);
                        } catch (Exception ex)
                        {
                            errorWriter.WriteLine(ex.Message);
                            return (int)ExitCode.FileWriteError;
                        }
                        WriteTomlExport(stream, sorted.ToArray());
                    }
                    break;
                case "yaml":
                    WriteYamlExport(output, sorted);
                    break;
                case "json":
                    WriteJsonExport(output, sorted);
                    break;
                default:
                    throw new ApplicationException($"Unexpected file format {options.Format}");
            }

            return 0;
        }

        private static void WriteJsonExport(TextWriter output, List<Record> sorted)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (JsonWriter writer = new JsonTextWriter(output))
            {
                serializer.Serialize(writer, sorted);
            }
            output.Flush();
        }

        private static void WriteYamlExport(TextWriter output, List<Record> sorted)
        {
            var serializer = new SerializerBuilder().Build();
            serializer.Serialize(output, sorted);
            output.Flush();
        }

        private static void WriteTomlExport(Stream stream, Record[] sorted)
        {
            TomlWrapper t = new TomlWrapper { Record = sorted };
            Toml.WriteStream(t, stream);
            stream.Flush();
        }

        private static void WriteCsvExport(TextWriter output, List<Record> sorted)
        {
            using (CsvWriter? csv = new CsvWriter(output, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(sorted);
            }
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
