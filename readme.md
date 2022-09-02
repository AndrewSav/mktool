# `mktool` - managing DHCP/DNS/Wi-Fi records on Mikrotik

## Introduction

`mktool` is a command tool for interacting with Mikrotik router. It was build to address specific challenges and is not suitable for general purpose Mikrotik management. Can use Hashicorp Vault for reading Mikrotik connection credentials. The tool mainly works with the following Mikrotik entities:

- DHCP (`/ip dhcp-server lease`) 
- DNS (`/ip dns static`)
- Wi-Fi (`/interface wireless access-list`)

There are two main function implemented by the utility:

- Import / Export the entities listed about
- Provision / Deprovision an entity on Mikrotik, for use case when no external IPAM is available

This utility is written in C# and binaries are available for Windows and Linux.

## Status

This tool has not (and most likely will never be) extensively tested. It is tested only on the use cases I'm personally using it for.

## Concepts

### Import and export

For the purpose of Disaster Recovery (Eg. in cases when you need to factory reset your Mikrotik router) you can export your entire Mikrotik configuration with the standard Mikrotik `/export` command. However, I found that while the bulk of my Mikrotik configuration stays the same, DHCP, DNS and Wi-Fi configuration is in constant flux. I wanted an automated way to unify this configuration from three tables (DHCP, DNS, Wi-Fi) to one and to be able to import and export this unified information to and from Mikrotik easily. 

The unified configuration consists of the following fields:

| Field         | Description                                                  | Entity Types              |
| ------------- | ------------------------------------------------------------ | ------------------------- |
| `Ip`          | Ip4 address as appears on DHCP and DNS entries in Mikrotik   | DHCP, DNS ("A" type only) |
| `Mac`         | Mac address as appears on DHCP and Wi-Fi entries in Mikrotik | DHCP, Wi-Fi               |
| `DhcpServer`  | DHCP Server (`/ip dhcp-server print`) a DHCP entry has       | DHCP                      |
| `DhcpLabel`   | Comment on the DHCP entry                                    | DHCP                      |
| `DnsHostName` | Name on a DNS entry, mutually exclusive with `DnsRegexp`     | DNS                       |
| `DnsRegexp`   | Regular expression on a DNS entry, mutually exclusive with `DnsHostName` | DNS                       |
| `DnsType`     | Type on a DNS entry - either "A" or "CNAME"                  | DNS ("CNAME" type only)   |
| `DnsCName`    | `CName` field on a DNS entry                                 | DNS                       |
| `HasDhcp`     | Indicates if there is correspondent Mikrotik DHCP entry      | DHCP                      |
| `HasDns`      | Indicates if there is correspondent Mikrotik DNS entry       | DNS                       |
| `HasWiFi`     | Indicates if there is correspondent Mikrotik Wi-Fi entry     | Wi-Fi                     |

During the export process A DHCP entry and a DNS entry is linked by IP address field. If there are several DNS entries with the same IP as a DHCP entry, the first returned by Mikrotik is linked. A DHCP entry and a Wi-Fi entry is linked by MAC address field. To make them easier for humans to work with a DHCP entry comment and Wi-Fi entry comment  is usually the same as the DNS name. In particular:

During *export*: DHCP entry comment is written to `DhcpLabel`. Wi-Fi entry comment is written to `DnsHostName`, if there is no linked DNS entry. If there is, Wi-Fi comment is not exported. 

During *import*: Wi-Fi entry comment is written from `DnsHostName` if present, otherwise from `DhcpLabel`. DHCP entry comment is written from `DhcpLabel` if present, otherwise from `DnsHostName`.

DNS "A" and "CNAME" and Wi-Fi entries that are not linked to a DHCP entry are also exported and imported.

Disabled records are not exported.

Following formats for export / import are supported: `toml`, `yaml`, `json`, `csv`.

### Provisioning and deprovisioning

*Note: Mikrotik [does not have](https://forum.mikrotik.com/viewtopic.php?t=108255) a concept of a transaction. This means that use of this tool is potentially dangerous if Mikrotik state changes by a third party when this tool is operating. Additionally, in case of a failure, partial changes are not rolled back. With the home scenario in mind this tool is written for, this is fine most of the time, but this must be considered for more complex environments.*

When a new VM needs to be provisioned on the network, and there is no external IPAM solution, this tool can help for using Mikrotik as poor's man IPAM.  First you will need to define allocation pools to draw IP addresses from. Here is an example:

```toml
[[Allocation]]
Name = "VMInt"
DhcpServer = "VMInt_DHCP"
IpRange = "10.33.90.3-10.33.90.100"

[[Allocation]]
Name = "VMExt"
DhcpServer = "VMExt_DHCP"
IpRange = "10.33.91.3-10.33.91.100"

[[Allocation]]
Name = "Home"
DhcpServer = "Home_DHCP"
IpRange = "10.33.88.3-10.33.88.100,10.33.88.128/26"
```

When a new IP address is required, you will need to supply the MAC address of the new Virtual Machine to the tool, and the name of the allocation to put the VM into. The tool will interrogate Mikrotik to find an address from the allocation IP range that is not used yet and will create a DHCP record, and optionally a DNS "A" record and a Wi-Fi record, with the new address.

If there is already a dynamic record for required MAC address is present, which is created during the first power up of the VM being provisioned, instead of specifying MAC, one can specify the hostname of the VM. The tool will find a dynamic record with that host name and will use the MAC address from that record.

In any case dynamic records with the required MAC address are deleted as part of provisioning, so that the VM can pick up the new IP address on the next DHCP lease request.

It is also possible to provision DNS "A" and "CNAME" records, without creating a DHCP entry.

Deprovisioning operation does the reverse. It takes an IP address or MAC address, or Host Name or DHCP label and removes all associated DHCP, DNS and Wi-Fi records. There is also an option to disable them instead of deleting.

## Usage

```text
mktool:
  Manage IP addresses on a Mikrotik router

Usage:
  mktool [options] [command]

Options:
  -a, --address <address>                                (global) Network address, IP or DNS, for Mikrotik
  -u, --user <user>                                      (global) Connection user for Mikrotik
  -p, --password <password>                              (global) Connection password for Mikrotik
  -v, --va, --vault-address <vault-address>              (global) Vault url, e.g. https://vault,
                                                         alternatively can be specified in VAULT_ADDR
                                                         environment variable
  --vault-user-location, --vul <vault-user-location>     (global) Path to Mikrotik user in vault, e.g.
                                                         'secret/my/path'
  --vault-password-location, --vpl                       (global) Path to Mikrotik password in vault, e.g.
  <vault-password-location>                              'secret/my/path'
  --vault-user-key, --vuk <vault-user-key>               (global) Key of the username in Vault under path
                                                         given by --vault-user-location [default: username]
  --vault-password-key, --vpk <vault-password-key>       (global) Key of the username in Vault under path
                                                         given by --vault-password-location [default:
                                                         password]
  -t, --vault-token, --vt <vault-token>                  (global) Vault token, alternatively can be
                                                         specified in VAULT_TOKEN environment variable, or
                                                         mktool can reuse results of 'vault login' command
  -z, --vault-debug, --vd                                (global) In case of problems with Vault response
                                                         will dump the content of the response to stderr
  -l, --log-level <debug|error|fatal|information|verb    (global) Write log to mktool.log. Re-created each
  ose|warning>                                           run
  --version                                              Show version information
  -?, -h, --help                                         Show help and usage information

Commands:
  export         Export provisioning configuration from Mikrotik
  import         Import (apply) provisioning configuration from file to Mikrotik
  provision      Provision a new DHCP or DNS record on Mikrotik
  deprovision    Deprovision a DHCP record, a WiFi record (if any) and all DNS records, matching provided
                 criteria
```

### Global options

#### Address

`-a, --address <address>`. This options specifies network address: IP or DNS of the Mikrotik router. API service has to be enabled on Mikrotik. Port  8728 is used for the connection. This is a required option.

#### User

`-u, --user <user>`. The user part of the credentials used to connect to the Mikrotik API. It is recommended to create a separate user for use with the tool for security and auditing reasons. Alternatively, the user name can come from Vault, by using the `--vault-user-location` option. One of these two options is required, and they cannot be specified together.

#### Password

`-p, --password <password>`. The password part of the credentials used to connect to the Mikrotik API. Alternatively, the password can come from Vault, by using the `--vault-password-location` option. One of these two options is required, and they cannot be specified together.

#### Vault Address

`-v, --va, --vault-address <vault-address>`. This is the URL of a Vault instance to pull Mikrotik username and/or password from. It can look like: `https://vault.mydomen.tld`. It is required if the username and/or password have to come from Vault and `VAULT_ADDR` environment variable is not defined. It will override  `VAULT_ADDR` environment variable if specified.

#### Vault Token

`-t, --vault-token, --vt <vault-token>`. Normally `mktool` will read the token from `VAULT_TOKEN` environment variable or from `~/.vault-token` file that is written by `vault login`. But you can also provide the token on the command line with this option. That will override token specified elsewhere. A token has to be specified in one of these three source for the Vault operation to be performed.

#### Vault User Location

`--vault-user-location, --vul <vault-user-location>`. This specifies the path for the secret in Vault where Mikrotik user name is stored. For `kv1` use `secret/path/toobject` where `secret` is the the mount path of `kv1` secret engine. For `kv2` use `kv/data/path/toobject` where `kv` is the mount path of `kv2` secret engine. One of this and `--user` option is required, and they cannot be specified together. If used Vault Address and Vault Token should also be provided (via the command line or not).

#### Vault User Key

`--vault-user-key, --vuk <vault-user-key>`. Key in the secret specified by `--vault-user-location`, where the user name is located. By default it's `username`. For example the secret might look like this:

```json
{
  "password": "mysecretpassword",
  "username": "mikrotik"
}
```

In this example `--vault-user-key` should be default, or `username`. This option only makes sense when `--vault-user-location` is also specified.

#### Vault Password Location

`--vault-password-location, --vpl <vault-password-location>`. Same as `--vault-user-location`, but for password. See above.

#### Vault Password Key

`--vault-password-key, --vpk <vault-password-key>`. Same as `--vault-user-key`,but for password. See above.

#### Vault Debug

`-z, --vault-debug, --vd`. This option will cause dumping the contents of Vault http response to `stdout`, if the response caused a error. It is helpful for debugging issues with Vault connection, but it does not sanitize the response, so all secretes will be displayed in clear text.

#### Log Level

`-l, --log-level <debug|error|fatal|information|verbose|warning>`. This options enables writing log to mktool.log in current directory. This is mostly helpful for debugging (when creating bug reports). Note, that the log is not written by default, and when written, overwrites the log from previous run.

### Response File

You can specify some or all command line options in the response file. For example if you run the tool for command line often against the same Mikrotik router with the same credentials, you can use a response file similar to this:

```text
-a
10.33.88.1
-u
mikrotik
--vault-password-location
secret/path/toobject
```

Make sure that the lines do not have trailing spaces. You can use this file similar to the following:

```powershell
mktool.exe deprovision '@mktool.rsp' -b myhost
```

### Export

```text
export:
  Export provisioning configuration from Mikrotik

Usage:
  mktool export [options]

Options:
  -f, --file <file>                                      Write to specified file instead of stdout
  -o, --format <csv|json|toml|yaml>                      Export format [default: csv]
```

#### File

`-f, --file <file>`. You can specify the file name to write the export of DHCP, DNS and Wi-Fi entities to. By default, the export is written to standard output.

#### Format

`-o, --format <csv|json|toml|yaml>`. Specify in which format the export is written. Default is `csv`.

### Import

```text
import:
  Import (apply) provisioning configuration from file to Mikrotik

Usage:
  mktool import [options]

Options:
  -f, --file <file> (REQUIRED)                           Read from specified file
  -o, --format <csv|json|toml|yaml>                      Export format
  -e, --execute                                          By default this command is run in dry-run mode.
                                                         Specify this to actually apply changes to Mikrotik
  -k, --continue-on-errors                               Does not stop execution with a error code when there
                                                         was a error writing a record to Mikrotik
  -s, --skip-existing                                    Reduce output verbosity by not printing already
                                                         existing records that will not be updated
```

#### File

`-f, --file <file>`. Specify the export file to import from. This option is required.

#### Format

`-o, --format <csv|json|toml|yaml>`. `mktool` tries to guess file format from the file extension. You can also specify the format with this option. If this option is not provided, and the format cannot be guessed from the extension, an error will be produced.

#### Execute

`-e, --execute`. Unless you specify this option, the import command will run in dry run mode, indicated by the first line of the output containing `DRY RUN`. This is useful to preview what changed `mktool` is going to do on Mikrotik. Specify this option to actually apply these changes.

#### Continue on errors

`-k, --continue-on-errors`. If this option is specified `mktool` won't stop execution when writing a record to Mikrotik resulted in a error. This can be useful to apply "as much as possible", however a caution should be taken, since an early error could indicated potentially serious problems (such as a third party writing to Mikrotik at the same time). If `mktool` continues on a error, that error's exit code will not be reported by `mktool` on exit.

#### Skip existing

`-s, --skip-existing`. Specify this option to reduce output verbosity by not printing already existing records that will not be updated. Existing records with non-matching values that are going to be updated will still be printed.

### Provision DHCP

```text
dhcp:
  Provision a new DHCP record and optionally a DNS and a Wi-Fi record on Mikrotik

Usage:
  mktool provision dhcp [options]

Options:
  -m, --mac-address <mac-address>                        MAC address
  -r, --active-host, --ah <active-host>                  When specified instead of --mac-address option, the
                                                         mac-address will be fetched from a dynamic dhcp
                                                         record on Mikrotik, with the active host name
                                                         specified in this option
  -d, --dns-name <dns-name>                              Create a DNS record for the address, with specified
                                                         name
  -c, --config <config>                                  Allocations config file path [default: mktool.toml]
  -n, --allocation <allocation> (REQUIRED)               Allocation name (one of the names present in the
                                                         allocations config)
  -w, --enable-WiFi                                      Enable WiFi access for the MAC address
  -b, --label <label>                                    Comment field for the DHCP lease on Mikrotik, if
                                                         different from dns-name or dns-name is not
                                                         specified
  -k, --continue-on-errors                               Does not stop execution with a error code when the
                                                         was a error writing a record to Mikrotik
  -e, --execute                                          By default this command is run in dry-run mode.
                                                         Specify this to actually apply changes to Mikrotik.
```

This command allocates an IP address from a pool described in the allocation configuration file and writes correspondent records to Mikrotik. It will delete existing dynamic DHCP lease for the matching MAC address, see the "Provisioning and deprovisioning" section above.

#### MAC address

`-m, --mac-address <mac-address>`. This is the MAC address for the new DHCP record to provision. Either this or `--active-host` option has to be specified. They cannot be specified together.

#### Active host

`-r, --active-host, --ah <active-host>`. This option is useful, when you have an active VM with a dynamic DHCP lease that you want to convert to a static one. It is slightly more convenient to specify VM by it's host name, rather than by MAC address. When this option is specified, `mktool` will try to find a dynamic DHCP lease on Mikrotik with the specified host name and will use the MAC address from this lease for provisioning. Either this or `--mac-address` option has to be specified. They cannot be specified together.

#### DNS name

`-d, --dns-name <dns-name>`. If this option is provided, in addition to the DHCP record, a DNS "A" record will also be provisioned. The DNS record will have the same IP address as the DHCP record. The host name for the DNS record is specified by this option.

#### Config

`-c, --config <config>`. This options specifies the file name of the allocation config, that is the description of IP address pools to draw new provisioned IPs from. This is a `toml` file and by default `mktool.toml` in the current directory is used. An example of this file can be found above in the "Provisioning and deprovisioning" section. The file must exist for the command to work.

#### Allocation

`-n, --allocation <allocation>`. This is the name of the allocation from the config file above. For example, for the file given in the "Provisioning and deprovisioning" section above, the valid values for this option will be `VMInt`, `VMExt` and `Home`. The new IP address will be allocated from the corresponding range.

#### Enable Wi-Fi

`-w, --enable-WiFi`.  If this option is specified, in addition to the DHCP record, a Wi-Fi record will also be provisioned. It will use the same MAC address, and the comment will be the DNS name, or, if not present, the Label.

#### Label

`-b, --label <label>`. Allows to specify the DHCP entry comment, if DNS name is not specified. It can also be used to override the specified DNS name, Eg. `--dns-name externalservice.domain.tld --label worker2`.

#### Continue on errors

`-k, --continue-on-errors`. If this option is specified `mktool` won't stop execution when writing a record to Mikrotik resulted in a error. This can be useful to apply "as much as possible", however a caution should be taken, since an early error could indicated potentially serious problems (such as a third party writing to Mikrotik at the same time). If `mktool` continues on a error, that error's exit code will not be reported by `mktool` on exit.

#### Execute

`-e, --execute`. Unless you specify this option, the import command will run in dry run mode, indicated by the first line of the output containing `DRY RUN`. This is useful to preview what changed `mktool` is going to do on Mikrotik. Specify this option to actually apply these changes.

### Provision DNS

```text
dns:
  Provision a new DNS record on Mikrotik

Usage:
  mktool provision dns [options]

Options:
  -r, --record-type <a|cname>                            Dns record type [default: a]
  -d, --dns-name <dns-name>                              DNS record name. Mutually exclusive with '--regexp'
  -x, --regexp <regexp>                                  Provide Mikrotik regular expression. Mutually
                                                         exclusive with '--dns-name'
  -i, --ip-address <ip-address>                          Provide IP address for record type 'a'
  -y, --cname <cname>                                    Provide CNAME for record type 'cname'
  -e, --execute                                          By default this command is run in dry-run mode.
                                                         Specify this to actually apply changes to Mikrotik.
```

This commands provisions standalone DNS records. Those can be either unrelated to DHCP records, or additional DNS records.

#### Record type

`-r, --record-type <a|cname>`. Specifies DNS record type `A` or `CNAME`. `A` is the default.

#### DNS name

`-d, --dns-name <dns-name>`. DNS record name. Mutually exclusive with `--regexp`.

#### Regexp

`-x, --regexp <regexp>`. Provides Mikrotik regular expression. Mutually exclusive with `--dns-name`.

#### IP address

`-i, --ip-address <ip-address>`. Provides IP address for record type `a`, cannot be used with record type `cname`.

#### CNAME

`-y, --cname <cname>`. Provides CNAME for record type `cname`, cannot be used with record type `a`.

#### Execute

`-e, --execute`. Unless you specify this option, the import command will run in dry run mode, indicated by the first line of the output containing `DRY RUN`. This is useful to preview what changed `mktool` is going to do on Mikrotik. Specify this option to actually apply these changes.

### Deprovision

```text
deprovision:
  Deprovision a DHCP record, a WiFi record (if any) and all DNS records, matching provided criteria

Usage:
  mktool deprovision [options]

Options:
  -m, --mac-address <mac-address>                        MAC address to deprovision
  -i, --ip-address <ip-address>                          IP address to deprovision
  -d, --dns-name <dns-name>                              DNS name to deprovision
  -b, --label <label>                                    DHCP comment to deprovision
  -q, --disable                                          Instead of deleting Mikrotik records mark them as
                                                         disabled
  -e, --execute                                          By default this command is run in dry-run mode.
                                                         Specify this to actually apply changes to Mikrotik.
```

Removes or disables DHCP, DNS and Wi-Fi records that match the specified criteria. Only one of the four `--mac-address`, `--ip-address`, `--dns-name`and `--label` can be used. This command runs `export` command first which combines DHCP, DNS and Wi-Fi records in a set of `mktool` records. Then it filters out all records, that match the specified criteria. Then it deletes or disables all corresponded Mikrotik DHCP, DNS and Wi-Fi entitles.

#### MAC address

`-m, --mac-address <mac-address>`. Filters records on MAC address. Only one of the four `--mac-address`, `--ip-address`, `--dns-name`and `--label` can be used.

#### IP address

`-i, --ip-address <ip-address>`. Filters records on IP address. Only one of the four `--mac-address`, `--ip-address`, `--dns-name`and `--label` can be used.

#### DNS name

`-d, --dns-name <dns-name>`. Filters records on DNS name. Note, that DHCP records without matching DNS records will not have DNS name. Will not match Wi-Fi records with matching `DnsHostName` field, if there is no linked DNS record. Only one of the four `--mac-address`, `--ip-address`, `--dns-name`and `--label` can be used.

#### Label

`-b, --label <label>`. Filters records on DHCP entity comment. Will also match Wi-Fi records with matching `DnsHostName` field, if there is no linked DNS record. Only one of the four `--mac-address`, `--ip-address`, `--dns-name`and `--label` can be used.

#### Disable

`-q, --disable `. Instead of deleting Mikrotik records mark them as disabled.

#### Execute

`-e, --execute`. Unless you specify this option, the import command will run in dry run mode, indicated by the first line of the output containing `DRY RUN`. This is useful to preview what changed `mktool` is going to do on Mikrotik. Specify this option to actually apply these changes.

### Error Codes

See [here](ExitCode.cs).

## Examples

### Export records

```powershell
mktool export -a 10.33.88.1 -u mikrotik -p mysecretpassword -f export.csv
```

### Import records, dry run

```powershell
mktool import -a 10.33.88.1 -u mikrotik -p mysecretpassword -f export.csv
```

### Import records, dry run, show changes only

```powershell
mktool import -a 10.33.88.1 -u mikrotik -p mysecretpassword -f export.csv -s
```

### Import records

```powershell
mktool import -a 10.33.88.1 -u mikrotik -p mysecretpassword -f export.csv -e
```

### Provision DHCP & DNS records, dry run

Make sure you have `mktool.toml` describing `vmext` allocation pool before running this command.

```powershell
mktool provision dhcp -a 10.33.88.1 -u mikrotik -p mysecretpassword -n vmext -m aa:bb:cc:dd:ee:ff -d myhost
```

### Provision DHCP & DNS records

Make sure you have `mktool.toml` describing `vmext` allocation pool before running this command.

```powershell
mktool provision dhcp -a 10.33.88.1 -u mikrotik -p mysecretpassword -n vmext -m aa:bb:cc:dd:ee:ff -d myhost -e
```

### Provision DNS "CNAME" record, dry run

```powershell
mktool provision dns -a 10.33.88.1 -u mikrotik -p mysecretpassword -r cname -d alias -y www.google.com
```

### Provision DHCP & DNS records

```powershell
mktool provision dns -a 10.33.88.1 -u mikrotik -p mysecretpassword -r cname -d alias -y www.google.com -e
```

### Deprovision DHCP & DNS records, dry run

Assuming provisioning above.

```powershell
mktool deprovision -a 10.33.88.1 -u mikrotik -p mysecretpassword -d myhost
```

### Deprovision DHCP & DNS records

Assuming provisioning above.

```powershell
mktool deprovision -a 10.33.88.1 -u mikrotik -p mysecretpassword -d myhost -e
```

### Deprovision DNS "CNAME record, dry run

Assuming provisioning above.

```powershell
mktool deprovision -a 10.33.88.1 -u mikrotik -p mysecretpassword -d alias
```

### Deprovision DNS "CNAME" records

Assuming provisioning above.

```powershell
mktool deprovision -a 10.33.88.1 -u mikrotik -p mysecretpassword -d alias -e
```

## Used Libraries

- CsvHelper https://joshclose.github.io/CsvHelper/
- Nett https://github.com/paiden/Nett
- Newtonsoft.Json https://www.newtonsoft.com/json
- Serilog https://serilog.net/
- System.CommandLine https://github.com/dotnet/command-line-api
- tik4net https://github.com/danikf/tik4net
- YamlDotNet https://github.com/aaubry/YamlDotNet/wiki

## Change log

- 1.0.2.0 - Fixed an exception if a DHCP record does not have a comment. Upgraded .net framework and dependencies
- 1.0.1.0 - after latest RouterOs upgrade mikrotik no longer returns word "type" if the DNS record is of type "A". Looks like now it's assumed the default. Fixed the code to reflect this

## License

Copyright 2020-2022 Andrew Savinykh

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

