using mktool.CommandLine;
using mktool.Utility;
using Serilog;
using System.Threading.Tasks;
using tik4net;

namespace mktool.Commands
{
    static class ProvisionDhcp
    {
        public static async Task Execute(ProvisionDhcpOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("ProvisionDhco command started");
            Log.Debug("Parameters: {@params}", options);

            ITikConnection connection = await Mikrotik.ConnectAsync(options);

        }
    }
}
