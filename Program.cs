using mktool.CommandLine;
using mktool.Utility;
using Serilog;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace mktool
{
    class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                return await Parser.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                HandleUnhandledException(ex);
                return 127;
            } finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void HandleUnhandledException(Exception ex)
        {
            // The logging infrastructure may be not up yet
            string bootstrapLogFile = "bootstrap.log";
            Serilog.Core.Logger? bootstrapLogger = new LoggerConfiguration()
                .WriteTo.File(bootstrapLogFile)
                .CreateLogger();

            Exception loggingException = ex;
            if (ex is TargetInvocationException tex && tex.InnerException != null)
            {
                loggingException = tex.InnerException;
            }
            Console.Error.WriteLine("An unhandled exception caused the program to terminate");
            Console.Error.WriteLine($"Message: {loggingException.Message}");
            Console.Error.WriteLine($"Type: {loggingException.GetType()}");
            Console.Error.WriteLine($"Stack trace will be logged to {bootstrapLogFile}");

            bootstrapLogger.Fatal(ex, "Top level unhandled exception caught");
            if (ex is VaultDataException vex)
            {
                bootstrapLogger.Fatal(vex.Response, "Vault response logged");
            }
        }
    }
}
