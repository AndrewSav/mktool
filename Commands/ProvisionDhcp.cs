using mktool.CommandLine;
using mktool.Utility;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace mktool.Commands
{
    static class ProvisionDhcp
    {
        public static async Task<int> Execute(ProvisionDhcpOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("Provision command started");
            Log.Debug("Parameters: {@params}", options);
            return 0;
        }
    }
}
