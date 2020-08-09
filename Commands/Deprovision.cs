using mktool.CommandLine;
using mktool.Utility;
using Serilog;
using System.Threading.Tasks;
using tik4net;

namespace mktool.Commands
{
    static class Deprovision
    {
        public static async Task Execute(DeprovisionOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("Deprovision command started");
            Log.Debug("Parameters: {@params}", options);

            ITikConnection connection = await Mikrotik.ConnectAsync(options);

        }
    }
}
