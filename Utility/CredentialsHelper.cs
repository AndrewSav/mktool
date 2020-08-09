using mktool.CommandLine;
using Serilog;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace mktool.Utility
{
    class CredentialsHelper
    {
        public static async Task<(string username, string password)> GetUsernameAndPassword(RootOptions options)
        {
            Log.Information("Retreiving username and password from Vault");
            try
            {
                string username;
                string password;
                if (options.User != null)
                {
                    username = options.User;
                }
                else
                {
                    Debug.Assert(options.VaultUserLocation != null);
                    Debug.Assert(options.VaultUserKey != null);
                    username = await Vault.GetVaultValue(options.VaultUserLocation, options.VaultUserKey, options.VaultAddress);
                }

                if (options.Password != null)
                {
                    password = options.Password;
                }
                else
                {
                    Debug.Assert(options.VaultPasswordLocation != null);
                    Debug.Assert(options.VaultPasswordKey != null);
                    password = await Vault.GetVaultValue(options.VaultPasswordLocation, options.VaultPasswordKey, options.VaultAddress);
                }
                return (username, password);
            }
            catch (VaultRequestException ex)
            {
                Log.Error(ex,"Error");
                Console.Error.WriteLine(ex.Message);
                if (options.VaultDiag)
                {
                    Log.Debug("Response: {response}", ex.Response);
                    Console.Error.WriteLine(ex.Response);
                }
                throw new MktoolException( ExitCode.VaultRequestError);
            }
            catch (VaultNoAddressException ex)
            {
                Log.Error(ex, "Error");
                Console.Error.WriteLine(ex.Message);
                throw new MktoolException( ExitCode.VaultMissingAddress);
            }
            catch (VaultMissingKeyException ex)
            {
                Log.Error(ex, "Error");
                Console.Error.WriteLine(ex.Message);
                if (options.VaultDiag)
                {
                    Log.Debug("Response: {response}", ex.Response);
                    Console.Error.WriteLine(ex.Response);
                }
                throw new MktoolException( ExitCode.VaultMissingKey);
            }
            catch (VaultTokenException ex)
            {
                Log.Error(ex, "Error");
                Console.Error.WriteLine(ex.Message);
                throw new MktoolException( ExitCode.VaultMissingToken);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "Error");
                Console.Error.WriteLine(ex.Message);
                throw new MktoolException( ExitCode.VaultHttpError);

            }
        }
    }
}
