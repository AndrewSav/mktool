﻿using mktool.CommandLine;
using mktool.Utility;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace mktool.Commands
{
    static class Import
    {
        public static async Task<int> Execute(ImportOptions options)
        {
            LoggingHelper.ConfigureLogging(options.LogLevel);
            Log.Information("Import command started");
            Log.Debug("Parameters: {@params}", options);
            return 0;
        }
    }
}
