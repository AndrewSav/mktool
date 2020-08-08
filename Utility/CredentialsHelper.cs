using mktool.CommandLine;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace mktool.Utility
{
    class CredentialsHelper
    {
        public static async Task<(string username, string password, int code)> GetUsernameAndPassword(RootOptions options)
        {
            TextWriter errorWriter = Console.Error;
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
                return (username, password, 0);
            }
            catch (VaultRequestException ex)
            {
                errorWriter.WriteLine(ex.Message);
                if (options.VaultDiag)
                {
                    errorWriter.WriteLine(ex.Response);
                }
                return (string.Empty, string.Empty, (int)ExitCode.VaultRequestError);
            }
            catch (VaultNoAddressException ex)
            {
                errorWriter.WriteLine(ex.Message);
                return (string.Empty, string.Empty, (int)ExitCode.VaultMissingAddress);
            }
            catch (VaultMissingKeyException ex)
            {
                errorWriter.WriteLine(ex.Message);
                if (options.VaultDiag)
                {
                    errorWriter.WriteLine(ex.Response);
                }
                return (string.Empty, string.Empty, (int)ExitCode.VaultMissingKey);
            }
            catch (VaultTokenException ex)
            {
                errorWriter.WriteLine(ex.Message);
                return (string.Empty, string.Empty, (int)ExitCode.VaultMissingToken);
            }
            catch (HttpRequestException ex)
            {
                errorWriter.WriteLine(ex.Message);
                return (string.Empty, string.Empty, (int)ExitCode.VaultHttpError);

            }
        }
    }
}
