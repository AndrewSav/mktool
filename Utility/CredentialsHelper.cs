﻿using mktool.CommandLine;
using Serilog;
using Serilog.Events;
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
            Log.Information("Retreiving username and password from Vault");
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
                Log.Error(ex,"Error");
                errorWriter.WriteLine(ex.Message);
                if (options.VaultDiag)
                {
                    Log.Debug("Response: {response}", ex.Response);
                    errorWriter.WriteLine(ex.Response);
                }
                return (string.Empty, string.Empty, (int)ExitCode.VaultRequestError);
            }
            catch (VaultNoAddressException ex)
            {
                Log.Error(ex, "Error");
                errorWriter.WriteLine(ex.Message);
                return (string.Empty, string.Empty, (int)ExitCode.VaultMissingAddress);
            }
            catch (VaultMissingKeyException ex)
            {
                Log.Error(ex, "Error");
                errorWriter.WriteLine(ex.Message);
                if (options.VaultDiag)
                {
                    Log.Debug("Response: {response}", ex.Response);
                    errorWriter.WriteLine(ex.Response);
                }
                return (string.Empty, string.Empty, (int)ExitCode.VaultMissingKey);
            }
            catch (VaultTokenException ex)
            {
                Log.Error(ex, "Error");
                errorWriter.WriteLine(ex.Message);
                return (string.Empty, string.Empty, (int)ExitCode.VaultMissingToken);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "Error");
                errorWriter.WriteLine(ex.Message);
                return (string.Empty, string.Empty, (int)ExitCode.VaultHttpError);

            }
        }
    }
}