using mktool.CommandLine;
using mktool.Utility;
using Serilog;
using System.Threading.Tasks;
using tik4net;

namespace mktool.Commands
{
    static class ProvisionDns
    {
        public static async Task Execute(ProvisionDnsOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("ProvisionDns command started");
            Log.Debug("Parameters: {@params}", options);

            ITikConnection connection = await Mikrotik.ConnectAsync(options);

        }
    }
}
