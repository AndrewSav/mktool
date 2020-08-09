using mktool.CommandLine;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using tik4net;

namespace mktool.Utility
{
    static class Mikrotik
    {
        public static async Task<ITikConnection> ConnectAsync(RootOptions options)
        {
            (string username, string password) = await CredentialsHelper.GetUsernameAndPassword(options);

            Debug.Assert(options.Address != null);

            Log.Information("Connecting to Mikrotik");
            ITikConnection? connection;
            try
            {
                connection = ConnectionFactory.OpenConnection(TikConnectionType.Api, options.Address, username, password);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                throw new MktoolException("Error", ExitCode.MikrotikConnectionError);
            }
            return connection;
        }

        public static IEnumerable<ITikSentence> CallMikrotik(ITikConnection? connection, string[] request)
        {
            Debug.Assert(connection != null);
            Log.Information("Executing microtik call {@request}", request);
            IEnumerable<ITikSentence> response;
            try
            {
                response = connection.CallCommandSync(request);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                throw new MktoolException("Error", ExitCode.MikrotikConnectionError);
            }
            Log.Verbose("Response: {@response}", response);
            return response;
        }
    }
}
