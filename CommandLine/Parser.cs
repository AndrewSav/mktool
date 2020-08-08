using mktool.Commands;
using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;

namespace mktool.CommandLine
{
    //TODO: token validation?
    //TODO: provision DNS only?
    //TODO: provision wifi only? (scrape log)
    //TODO: option to NOT provision DNS
    //TODO: DHCP - convert dynamic records
    //TODO: Import dry run
    //TODO: Add "main" loggging with serilog
    static class Parser
    {

        public static async Task<int> InvokeAsync(string[] args, IConsole? console = null)
        {
            System.CommandLine.Parsing.Parser? _ = new CommandLineBuilder(RootCommand)
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
            RootCommand? rootCommand = new RootCommand("Manage IP addresses on a Mikrotik router");

            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--address", "-a" },
                    description: "Network address, ip or dns, for Miktorik"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--user", "-u" },
                    description: "Connection user for Miktorik"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--password", "-p" },
                    description: "Connection password for Miktorik"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-user-location", "--vul", "-z" },
                    description: "Path to Mikrotik user in vault. Include the secret mountpoint and separate they key with colon, e.g. 'secert/my/path:user'"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-password-location", "--vpl", "-y" },
                    description: "Path to Mikrotik password in vault. Include the secret mountpoint and separate they key with colon, e.g. 'secert/my/path:pwd'"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-user-key", "--vuk", "-c" },
                    getDefaultValue: () => "username",
                    description: "Path to Mikrotik user in vault. Include the secret mountpoint and separate they key with colon, e.g. 'secert/my/path:user'"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-password-key", "--vpk", "-b" },
                    getDefaultValue: () => "password",
                    description: "Path to Mikrotik password in vault. Include the secret mountpoint and separate they key with colon, e.g. 'secert/my/path:pwd'"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-address", "--va", "-x" },
                    description: "Vault url, e.g. https://vault, alternatively can be specified in VAULD_ADDR environment variable"));
            rootCommand.AddGlobalOption(new Option<string>(
                    new[] { "--vault-token", "--vt", "-t" },
                    description: $"Vault token, alternatively can be specified in VAULT_TOKEN environment variable, or {rootCommand.Name} can reuse results of 'vault login' command"));
            rootCommand.AddGlobalOption(new Option<bool>(
                    new[] { "--vault-diag", "--vd", "-d" },
                    description: $"In case of problems with Vault response will dump the content of the respond to stderr"));

            rootCommand.Add(BuildExportCommand());
            rootCommand.Add(BuildImportCommand());
            rootCommand.Add(BuildProvisionCommand());
            rootCommand.Add(BuildDeprovisionCommand());

            rootCommand.Handler = CommandHandler.Create<RootOptions>((rootOptions) =>
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
            Command command = new Command("deprovision", "Deprovision an IP address on Mikrotik");

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

            command.Add(macAddressOption);
            command.Add(ipAddressOption);

            command.AddValidator(commandResult =>
            {
                if (commandResult.Children.Contains("mac-address") &&
                    commandResult.Children.Contains("ip-address"))
                {
                    return "Options '--mac-address' and '--ip-address' cannot be used together.";
                }
                return null;
            });

            command.AddValidator(commandResult =>
            {
                if (!commandResult.Children.Contains("mac-address") &&
                    !commandResult.Children.Contains("ip-address"))
                {
                    return "One of the options '--mac-address' and '--ip-address' is required.";
                }
                return null;
            });

            AddGlobalValidators(command);

            command.Handler = CommandHandler.Create<DeprovisionOptions>(async (deprovisionOptions) =>
            {
                return await Deprovision.Execute(deprovisionOptions);
            });

            return command;
        }

        private static Command BuildProvisionCommand()
        {
            Command command = new Command("provision", "Provision a new IP address on Mikroik, and return information about the address provisioned")
            {
                new Option<FileInfo>(
                    new []{ "--config", "-с" },
                    description: "Allocations config file path",
                    getDefaultValue: () => new FileInfo("mktool.toml")),
                new Option<string>(
                    new []{ "--allocation", "-a" },
                    description: "Allocation name") { IsRequired = true },
                new Option<bool>(
                    new []{ "--enable-wifi", "-w" },
                    description: "Enable wifi access for the MAC address"),
                new Option<string>(
                    new []{ "--label", "-l" },
                    description: "Comment field for the DHCP lease on Mikrotik, if different from dns-name or dns name is not specified"),
            };
            
            Option<string> dnsNameOption = new Option<string>(new[] { "--dns-name", "-d" }, description: "Create a dns record for the address");

            dnsNameOption.AddValidator(r =>
            {
                string? value = r.GetValueOrDefault<string>();
                if (value != null && !Validation.IsDnsValid(value))
                {
                    return $"Option {r.Token?.Value} has to be rfc1123 DNS name.";
                }
                return null;
            });

            Option<string> macAddressOption = new Option<string>(new[] { "--mac-address", "-m" }, description: "MAC address") { IsRequired = true };
            macAddressOption.AddValidator(r =>
            {
                string? value = r.GetValueOrDefault<string>();
                if (value != null && !Validation.IsMacValid(value))
                {
                    return $"Option {r.Token?.Value} has to be a valid MAC-48 address.";
                }
                return null;
            });

            command.Add(macAddressOption);
            command.Add(dnsNameOption);


            AddGlobalValidators(command);

            command.Handler = CommandHandler.Create<ProvisionOptions>(async (provisionOptions) =>
            {
                return await Provision.Execute(provisionOptions);
            });

            return command;

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

                };
            AddGlobalValidators(command);
            command.Handler = CommandHandler.Create<ImportOptions>(async (importOptions) =>
            {
                return await Import.Execute(importOptions);
            });
            return command;

        }

        private static Command BuildExportCommand()
        {
            Command command = new Command("export", "Export provisioning configuration from Mikrotik")
                {
                    new Option<FileInfo>(
                        new []{ "--file", "-f" },
                        description: "Write to specified file instead of stdout"),
                    new Option<bool>(
                        new []{ "--no-warnings", "-s" },
                        description: "Suppress warnings about potential re-import issues"),
                    new Option<string>(
                        new []{ "--format", "-o" },
                        description: "Export format") { Argument = new Argument<string>(() => "csv").FromAmong("toml","yaml","json","csv")},
                };
            AddGlobalValidators(command);
            command.Handler = CommandHandler.Create<ExportOptions>(async (exportOptions) =>
            {
                return await Export.Execute(exportOptions);
            });
            return command;
        }
    }
}
