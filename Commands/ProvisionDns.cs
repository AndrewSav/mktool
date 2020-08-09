using mktool.CommandLine;
using mktool.Utility;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace mktool.Commands
{
    static class ProvisionDns
    {
        public static async Task<int> Execute(ProvisionDnsOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("Provision command started");
            Log.Debug("Parameters: {@params}", options);
            return 0;
        }
    }
}
