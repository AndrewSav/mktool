using Serilog;
using System;
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
                int result =  await Parser.InvokeAsync(args);
                if (result != 0)
                {
                    return (int) ExitCode.CommandLineError;
                }
                else
                {
                    return (int) ExitCode.Success;
                }
            }
            catch (Exception ex)
            {
                HandleUnhandledException(ex);
                return (int)ExitCode.UnhandledException;
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
