using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace mktool
{
    static class LoggingHelper
    {
        public static string LogFile => "mktool.log";

        public static void ConfigureLogging(string? level)
        {
            if (level == null)
            {
                // Log to nowhere
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Fatal()
                    .CreateLogger();
                return;
            }
            try
            { 
                File.Delete(LogFile);
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Is(Enum.Parse<LogEventLevel>(level, true))
                    .Destructure.AsScalar(typeof(FileInfo))
                    .WriteTo.File(LogFile)
                    .CreateLogger();
                Log.Information("Logging system initialized");
            }
            catch (Exception ex)
            {
                Console.Error.Write($"Error initializing logging system. {ex.Message}");
                throw new MktoolException( ExitCode.LoggingInitError);
            }
        }
    }
}
