using Serilog;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace mktool
{
    static class CredentialsHelper
    {
        public static async Task<(string username, string password)> GetUsernameAndPassword(RootOptions options)
        {
            Log.Information("Retrieving username and password from Vault");
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
                    username = await Vault.GetVaultValue(options.VaultUserLocation, options.VaultUserKey, options.VaultAddress, options.VaultToken);
                }

                if (options.Password != null)
                {
                    password = options.Password;
                }
                else
                {
                    Debug.Assert(options.VaultPasswordLocation != null);
                    Debug.Assert(options.VaultPasswordKey != null);
                    password = await Vault.GetVaultValue(options.VaultPasswordLocation, options.VaultPasswordKey, options.VaultAddress, options.VaultToken);
                }
                return (username, password);
            }
            catch (VaultRequestException ex)
            {
                Log.Error(ex,"Error");
                await Console.Error.WriteLineAsync(ex.Message);
                if (options.VaultDebug)
                {
                    Log.Debug("Response: {response}", ex.Response);
                    await Console.Error.WriteLineAsync(ex.Response);
                }
                throw new MktoolException( ExitCode.VaultRequestError);
            }
            catch (VaultMissingKeyException ex)
            {
                Log.Error(ex, "Error");
                await Console.Error.WriteLineAsync(ex.Message);
                if (options.VaultDebug)
                {
                    Log.Debug("Response: {response}", ex.Response);
                    await Console.Error.WriteLineAsync(ex.Response);
                }
                throw new MktoolException( ExitCode.VaultMissingKey);
            }
            catch (VaultTokenException ex)
            {
                Log.Error(ex, "Error");
                await Console.Error.WriteLineAsync(ex.Message);
                throw new MktoolException( ExitCode.VaultMissingToken);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "Error");
                await Console.Error.WriteLineAsync($"Error: HTTP request to Vault failed. {ex.Message}");
                throw new MktoolException( ExitCode.VaultHttpError);
            }
        }
    }
}
