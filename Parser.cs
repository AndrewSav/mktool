using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace mktool
{

    static class Parser
    {

        public static async Task<int> InvokeAsync(string[] args, IConsole? console = null)
        {
            // This is to exclude ExceptionHandler middleware, to prevent CommandLine library from swallowing exceptions
            System.CommandLine.Parsing.Parser _ = new CommandLineBuilder(RootCommand)
                   .UseVersionOption()
                   .UseHelp()
                   .UseEnvironmentVariableDirective()
                   .UseParseDirective()
                   .UseDebugDirective()
                   .UseSuggestDirective()
                   .RegisterWithDotnetSuggest()
                   .UseTypoCorrections()
                   .UseParseErrorReporting()
                   .CancelOnProcessTermination()
                   .Build();

            return await RootCommand.InvokeAsync(args, console);
        }

        private static RootCommand RootCommand { get; } = BuildRootCommand();

        private static RootCommand BuildRootCommand()
        {
            RootCommand rootCommand = new RootCommand("Manage IP addresses on a Mikrotik router");

            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--address", "-a" },
                    description: "(global) Network address, IP or DNS, for Mikrotik"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--user", "-u" },
                    description: "(global) Connection user for Mikrotik"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--password", "-p" },
                    description: "(global) Connection password for Mikrotik"));

            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-address", "--va", "-v" },
                    description: "(global) Vault url, e.g. https://vault, alternatively can be specified in VAULT_ADDR environment variable"));

            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-user-location", "--vul" },
                    description: "(global) Path to Mikrotik user in vault, e.g. 'secret/my/path'"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-password-location", "--vpl" },
                    description: "(global) Path to Mikrotik password in vault, e.g. 'secret/my/path'"));

            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-user-key", "--vuk" },
                    getDefaultValue: () => "username",
                    description: "(global) Key of the username in Vault under path given by --vault-user-location"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-password-key", "--vpk" },
                    getDefaultValue: () => "password",
                    description: "(global) Key of the username in Vault under path given by --vault-password-location"));
            
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-token", "--vt", "-t" },
                    description: $"(global) Vault token, alternatively can be specified in VAULT_TOKEN environment variable, or {rootCommand.Name} can reuse results of 'vault login' command"));
            rootCommand.AddGlobalOption(new Option<bool>(
                    new[] { "--vault-debug", "--vd", "-z" },
                    description: "(global) In case of problems with Vault response will dump the content of the response to stderr"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--log-level", "-l" },
                    description: $"(global) Write log to {LoggingHelper.LogFile}. Re-created each run") { Argument = new Argument<string>().FromAmong("verbose", "debug", "information", "warning", "error", "fatal")});

            rootCommand.Add(BuildExportCommand());
            rootCommand.Add(BuildImportCommand());
            rootCommand.Add(BuildProvisionCommand());
            rootCommand.Add(BuildDeprovisionCommand());

            rootCommand.Handler = CommandHandler.Create<RootOptions>(rootOptions =>
            {
                rootCommand.Invoke(new[] { "-h" });
            });

            return rootCommand;
        }

        private static void AddGlobalValidators(Command command)
        {
            command.AddValidator(commandResult =>
            {
                if (!commandResult.Children.Contains("address"))
                {
                    return "Option '--address' is required.";
                }

                return null;
            });

            command.AddValidator(commandResult =>
            {
                if (commandResult.Children.Contains("vault-token") &&
                    !commandResult.Children.Contains("vault-password-location") &&
                    !commandResult.Children.Contains("vault-user-location"))
                {
                    return "Options '--vault-token' must be used with '--vault-password-location' and/or '--vault-user-location' options";
                }

                return null;
            });

            command.AddValidator(commandResult =>
            {
                if (!commandResult.Children.Contains("user") && !commandResult.Children.Contains("vault-user-location"))
                {
                    return "One of the options '--user' and '--vault-user-location' is required.";
                }

                return null;
            });

            command.AddValidator(commandResult =>
            {
                if (commandResult.Children.Contains("password") &&
                    commandResult.Children.Contains("vault-password-location"))
                {
                    return "Options '--password' and '--vault-password-location' cannot be used together.";
                }
                return null;
            });

            command.AddValidator(commandResult =>
            {
                if (!commandResult.Children.Contains("password") &&
                    !commandResult.Children.Contains("vault-password-location"))
                {
                    return "One of the options '--password' and '--vault-password-location' is required.";
                }
                return null;
            });

            command.AddValidator(commandResult =>
            {
                if (commandResult.Children.Contains("vault-address") &&
                    !commandResult.Children.Contains("vault-password-location") &&
                    !commandResult.Children.Contains("vault-user-location"))
                {
                    return "Options '--vault-address' must be used with '--vault-password-location' and/or '--vault-user-location' options";
                }
                return null;
            });

            command.AddValidator(commandResult =>
            {
                if (commandResult.Children.Contains("user") &&
                    commandResult.Children.Contains("vault-user-location"))
                {
                    return "Options '--user' and '--vault-user-location' cannot be used together.";
                }
                return null;
            });

            command.AddValidator(commandResult =>
            {
                if (commandResult.Children.Contains("vault-password-location") ||
                    commandResult.Children.Contains("vault-user-location"))
                {
                    if (!commandResult.Children.Contains("vault-address") && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VAULT_ADDR")))
                    {
                        return "Option '--vault-password-location' and/or '--vault-user-location' must be specified with '--vault-address' option or 'VAULT_ADDR' environment variable";
                    }
                }
                return null;
            });

            command.AddValidator(commandResult =>
            {
                if (commandResult.Children.Contains("vault-token"))
                {
                    if (!commandResult.Children.Contains("vault-address") && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VAULT_ADDR")))
                    {
                        return "Option '--vault-token' must be specified with '--vault-address' option or 'VAULT_ADDR' environment variable";
                    }
                }
                return null;
            });
        }

        private static Command BuildDeprovisionCommand()
        {
            Command command = new Command("deprovision", "Deprovision a DHCP record, a WiFi record (if any) and all DNS records, matching provided criteria");

            Option<string> macAddressOption = new Option<string>(new[] { "--mac-address", "-m" }, description: "MAC address to deprovision"); 
            macAddressOption.AddValidator(r =>
            {
                string? value = r.GetValueOrDefault<string>();
                if (value != null && !Validation.IsMacValid(value))
                {
                    return $"Option {r.Token?.Value} has to be a valid MAC-48 address.";
                }
                return null;
            });

            Option<string> ipAddressOption = new Option<string>(new[] { "--ip-address", "-i" }, description: "IP address to deprovision");
            ipAddressOption.AddValidator(r =>
            {
                string? value = r.GetValueOrDefault<string>();
                if (value != null && !Validation.IsIpValid(value))
                {
                    return $"Option {r.Token?.Value} has to be a valid IPv4 address.";
                }
                return null;
            });

            Option<string> dnsNameOption = new Option<string>(new[] { "--dns-name", "-d" }, description: "DNS name to deprovision");
            Option<string> labelOption = new Option<string>(new[] { "--label", "-b" }, description: "DHCP comment to deprovision");
            Option<bool> disableOption = new Option<bool>(new[] { "--disable", "-q" }, description: "Instead of deleting Mikrotik records mark them as disabled");
            Option<bool> executeOption = new Option<bool>(new[] { "--execute", "-e" }, description: "By default this command is run in dry-run mode. Specify this to actually apply changes to Mikrotik.");


            command.Add(macAddressOption);
            command.Add(ipAddressOption);
            command.Add(dnsNameOption);
            command.Add(labelOption);
            command.Add(disableOption);
            command.Add(executeOption);

            command.AddValidator(commandResult =>
            {
                int total = GetNumberOfOnSwitches(commandResult, new[] { "ip-address", "mac-address", "dns-name", "label"});
                if (total > 1 )
                {
                    return "Options '--mac-address', '--ip-address', '--dns-name', '--label' cannot be used together.";
                }
                return null;
            });

            command.AddValidator(commandResult =>
            {
                int total = GetNumberOfOnSwitches(commandResult, new[] { "ip-address", "mac-address", "dns-name", "label" });
                if (total == 0)
                {
                    return "One of the options '--mac-address', '--ip-address', '--dns-name', '--label' is required.";
                }
                return null;
            });

            AddGlobalValidators(command);

            command.Handler = CommandHandler.Create<DeprovisionOptions>(async deprovisionOptions => await CommandHandlerWrapper.ExecuteCommandHandler(deprovisionOptions, Deprovision.Execute));

            return command;
        }

        private static int GetNumberOfOnSwitches(CommandResult commandResult, string[] switchNames)
        {
            int result = 0;
            foreach(string? name in switchNames)            
            {
                if (commandResult.Children.Contains(name))
                {
                    result++;
                }
            }
            return result;
        }

        private static Command BuildProvisionCommand()
        {
            Command command = new Command("provision", "Provision a new DHCP or DNS record on Mikrotik");
            Command dhcpCommand = BuildProvisionDhcpCommand();
            Command dnsCommand = BuildProvisionDnsCommand();

            command.Add(dhcpCommand);
            command.Add(dnsCommand);

            command.Handler = CommandHandler.Create(() =>
            {
                command.Invoke(new[] { "-h" });
            });

            return command;
        }

        private static Command BuildProvisionDhcpCommand()
        {
            Option<string> dnsNameOption = new Option<string>(new[] { "--dns-name", "-d" }, description: "Create a DNS record for the address, with specified name");

            dnsNameOption.AddValidator(r =>
            {
                string? value = r.GetValueOrDefault<string>();
                if (value != null && !Validation.IsDnsValid(value))
                {
                    return $"Option {r.Token?.Value} has to be rfc1123 DNS name.";
                }
                return null;
            });

            Option<string> macAddressOption = new Option<string>(new[] { "--mac-address", "-m" }, description: "MAC address");
            macAddressOption.AddValidator(r =>
            {
                string? value = r.GetValueOrDefault<string>();
                if (value != null && !Validation.IsMacValid(value))
                {
                    return $"Option {r.Token?.Value} has to be a valid MAC-48 address.";
                }
                return null;
            });

            Command dhcpCommand = new Command("dhcp", "Provision a new DHCP record and optionally a DNS and a Wi-Fi record on Mikrotik")
            {
                macAddressOption,
                new Option<string>(
                    new []{ "--active-host", "--ah", "-r" },
                    description: "When specified instead of --mac-address option, the mac-address will be fetched from a dynamic dhcp record on Mikrotik, with the active host name specified in this option"),
                dnsNameOption,
                new Option<FileInfo>(
                    new []{ "--config", "-c" },
                    description: "Allocations config file path",
                    getDefaultValue: () => new FileInfo("mktool.toml")),
                new Option<string>(
                    new []{ "--allocation", "-n" },
                    description: "Allocation name (one of the names present in the allocations config)") { IsRequired = true },
                new Option<bool>(
                    new []{ "--enable-WiFi", "-w" },
                    description: "Enable WiFi access for the MAC address"),
                new Option<string>(
                    new []{ "--label", "-b" },
                    description: "Comment field for the DHCP lease on Mikrotik, if different from dns-name or dns-name is not specified"),
                new Option<bool>(
                    new []{ "--continue-on-errors", "-k" },
                    description: "Does not stop execution with a error code when the was a error writing a record to Mikrotik"),
                new Option<bool>(
                    new []{ "--execute", "-e" },
                    description: "By default this command is run in dry-run mode. Specify this to actually apply changes to Mikrotik."),
            };
            AddGlobalValidators(dhcpCommand);

            dhcpCommand.AddValidator(commandResult =>
            {
                if (!commandResult.Children.Contains("mac-address") &&
                    !commandResult.Children.Contains("active-host"))
                {
                    return "One of the options '--mac-address' and '--active-host' is required.";
                }
                if (commandResult.Children.Contains("mac-address") &&
                    commandResult.Children.Contains("active-host"))
                {
                    return "Options '--mac-address' and '--active-host' cannot be used together.";
                }
                return null;
            });


            dhcpCommand.Handler = CommandHandler.Create<ProvisionDhcpOptions>(async provisionDhcpOptions => await CommandHandlerWrapper.ExecuteCommandHandler(provisionDhcpOptions, ProvisionDhcp.Execute));
            return dhcpCommand;
        }

        private static Command BuildProvisionDnsCommand()
        {
            Option<string> ipAddressOption = new Option<string>(new[] { "--ip-address", "-i" }, description: "Provide IP address for record type 'a'");
            ipAddressOption.AddValidator(r =>
            {
                string? value = r.GetValueOrDefault<string>();
                if (value != null && !Validation.IsIpValid(value))
                {
                    return $"Option {r.Token?.Value} has to be a valid IPv4 address.";
                }
                return null;
            });

            Option<string> recordTypeOption = new Option<string>(
                new[] { "--record-type", "-r" },
                description: "Dns record type")
            { Argument = new Argument<string>(() => "a").FromAmong("a", "cname") };
            Command dnsCommand = new Command("dns", "Provision a new DNS record on Mikrotik")
            {
                recordTypeOption,
                new Option<string>(
                    new []{ "--dns-name", "-d" },
                    description: "DNS record name.  Mutually exclusive with '--regexp'"),
                new Option<string>(
                    new []{ "--regexp", "-x" },
                    description: "Provide Mikrotik regular expression. Mutually exclusive with '--dns-name'"),
                ipAddressOption,
                new Option<string>(
                    new []{ "--cname", "-y" },
                    description: "Provide CNAME for record type 'cname'"),
                new Option<bool>(
                    new []{ "--execute", "-e" },
                    description: "By default this command is run in dry-run mode. Specify this to actually apply changes to Mikrotik."),
            };


            AddGlobalValidators(dnsCommand);

            recordTypeOption.AddValidator(commandResult =>
            {
                Debug.Assert(commandResult.Parent != null);
                var value = commandResult.GetValueOrDefault<string>();
                if (string.Equals(value, "a", StringComparison.OrdinalIgnoreCase))
                {
                    if (!commandResult.Parent.Children.Contains("ip-address"))
                    {
                        return "When '--record-type' is 'a', '--ip-address' is required.";
                    }
                    if (commandResult.Parent.Children.Contains("cname"))
                    {
                        return "When '--record-type' is 'a', '--cname' is not supported.";
                    }
                }
                if (string.Equals(value, "cname", StringComparison.OrdinalIgnoreCase))
                {
                    if (!commandResult.Parent.Children.Contains("cname"))
                    {
                        return "When '--record-type' is 'cname', '--cname' is required.";
                    }
                    if (commandResult.Parent.Children.Contains("ip-address"))
                    {
                        return "When '--record-type' is 'cname', '--ip-address' is nor supported.";
                    }
                }
                return null;
            });

            dnsCommand.AddValidator(commandResult =>
            {
                if (!commandResult.Children.Contains("regexp") &&
                    !commandResult.Children.Contains("dns-name"))
                {
                    return "One of the options '--dns-name' and '--regexp' is required.";
                }
                if (commandResult.Children.Contains("regexp") &&
                    commandResult.Children.Contains("dns-name"))
                {
                    return "Only one of the options '--dns-name' and '--regexp' can be specified.";
                }
                return null;
            });

            dnsCommand.Handler = CommandHandler.Create<ProvisionDnsOptions>(async provisionDnsOptions => await CommandHandlerWrapper.ExecuteCommandHandler(provisionDnsOptions, ProvisionDns.Execute));
            return dnsCommand;
        }

        private static Command BuildImportCommand()
        {
            Command command = new Command("import", "Import (apply) provisioning configuration from file to Mikrotik")
            {
                new Option<FileInfo>(
                    new []{ "--file", "-f" },
                    description: "Read from specified file") { IsRequired = true },
                new Option<string>(
                    new []{ "--format", "-o" },
                    description: "Export format") { Argument = new Argument<string>().FromAmong("toml","yaml","json","csv")},
                new Option<bool>(
                    new []{ "--execute", "-e" },
                    description: "By default this command is run in dry-run mode. Specify this to actually apply changes to Mikrotik"),
                new Option<bool>(
                    new []{ "--continue-on-errors", "-k" },
                    description: "Does not stop execution with a error code when the was a error writing a record to Mikrotik"),
                new Option<bool>(
                    new []{ "--skip-existing", "-s" },
                    description: "Reduce output verbosity by not printing already existing records that will not be updated"),
            };
            AddGlobalValidators(command);

            command.Handler = CommandHandler.Create<ImportOptions>(async importOptions => await CommandHandlerWrapper.ExecuteCommandHandler(importOptions, Import.Execute));
            return command;

        }

        private static Command BuildExportCommand()
        {
            Command command = new Command("export", "Export provisioning configuration from Mikrotik")
            {
                new Option<FileInfo>(
                    new []{ "--file", "-f" },
                    description: "Write to specified file instead of stdout"),
                new Option<string>(
                    new []{ "--format", "-o" },
                    description: "Export format") { Argument = new Argument<string>(() => "csv").FromAmong("toml","yaml","json","csv")},
            };
            AddGlobalValidators(command);

            command.Handler = CommandHandler.Create<ExportOptions>(async exportOptions => await CommandHandlerWrapper.ExecuteCommandHandler(exportOptions, Export.Execute));
            return command;
        }
    }
}
