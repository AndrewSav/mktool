﻿using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace mktool
{

    public static class Vault
    {
        public static async Task<string> GetVaultValue(string path, string vaultKey, string? vaultAddress = null, string? vaultToken = null)
        {
            Log.Information("Getting value from Vault at '{path}', for key '{vaultKey}'", path, vaultKey);
            Log.Debug("Vault address passed: {vaultAddress}", vaultAddress);

            vaultAddress ??= Environment.GetEnvironmentVariable("VAULT_ADDR");
            Log.Debug("Vault address after environment lookup: {vaultAddress}", vaultAddress);

            if (string.IsNullOrWhiteSpace(vaultAddress))
            {
                throw new VaultNoAddressException("Could not determine VaultAddress");
            }

            string token = GetVaultToken(vaultToken);
            Log.Debug("Token present");

            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage();
            request.Headers.Add("X-Vault-Token", token);
            if(!Uri.TryCreate($"{vaultAddress}/v1/{path}", UriKind.Absolute, out Uri? uri))
            {
                throw new VaultNoAddressException($"Cannot parse URI: '{vaultAddress}/v1/{path}'");
            }
            request.RequestUri = uri;
            Log.Debug("Sending HTTP request at {uri}", request.RequestUri);
            HttpResponseMessage? result = await client.SendAsync(request);
            string? response = await result.Content.ReadAsStringAsync();

            if (!result.IsSuccessStatusCode)
            {
                string additionalInfo = "";
                if (result.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    additionalInfo = " Have you logged in?";
                }
                throw new VaultRequestException($"Error: querying vault at {vaultAddress}/v1/{path}. {(int)result.StatusCode} {result.ReasonPhrase}{additionalInfo}", response);
            }

            if (!(JObject.Parse(response)["data"] is JObject j))
            {
                throw new VaultDataException("Cannot parse 'data' element of Vault response as a json object", response);
            }

            // KV2
            if (j["data"] is JObject jd)
            {
                j = jd;
            }

            if (vaultKey == "*")
            {
                return j.ToString();
            }

            JToken? vaultValue = j[vaultKey];

            if (vaultValue == null)
            {
                throw new VaultMissingKeyException($"Error: object at '{path}' does not contain requested key '{vaultKey}'", response);
            }

            return vaultValue.ToString();
        }

        private static string GetVaultToken(string? vaultToken)
        {
            Log.Information("Getting vault token");
            Log.Debug("Token passed: {tokenPassed}", vaultToken != null);
            vaultToken ??= Environment.GetEnvironmentVariable("VAULT_TOKEN");
            Log.Debug("Token value provided after environment lookup: {tokenPassed}", vaultToken != null);

            if (!string.IsNullOrWhiteSpace(vaultToken))
            {
                return vaultToken;
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string tokenFile = Path.Combine(home, ".vault-token");
            Log.Debug("Home: {home}, token file: {tokenFile}", home, tokenFile);

            if (!File.Exists(tokenFile))
            {
                throw new VaultTokenException($"Error: {tokenFile} is not found. Please login to vault first with 'vault login'");
            }

            string token;
            try
            {
                token = File.ReadAllText(tokenFile);
            }
            catch (Exception ex)
            {
                throw new VaultTokenException("Cannot read token file", ex);
            }
            return token;
        }
    }
}
