﻿using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace mktool.Utility
{
    static class LoggingHelper
    {
        public static string LogFile { get => "mktool.log"; }

        public static bool ConfigureLogging(string? level)
        {
            if (level == null)
            {
                // Log to nowhere
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Fatal()
                    .CreateLogger();
                return true;
            }
            try
            { 
                File.Delete(LogFile);
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Is(Enum.Parse<LogEventLevel>(level, true))
                    .WriteTo.File(LogFile)
                    .CreateLogger();
                Log.Information("Logging system initialized");
                return true;
            }
            catch (Exception ex)
            {
                TextWriter errorWriter = Console.Error;
                errorWriter.Write(ex.Message);
                return false;
            }
        }
    }
}