using Serilog;
using System.Threading.Tasks;
using tik4net;

namespace mktool
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
