using mktool.CommandLine;
using mktool.Utility;
using Serilog;
using System.Threading.Tasks;
using tik4net;

namespace mktool.Commands
{
    static class Import
    {
        public static async Task Execute(ImportOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("Import command started");
            Log.Debug("Parameters: {@params}", options);

            ITikConnection connection = await Mikrotik.ConnectAsync(options);

        }
    }
}
