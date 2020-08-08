using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace mktool.Utility
{

    static class Vault
	{
		public static async Task<string> GetVaultValue(string path, string vaultKey, string? vaultAddress = null, string? vaultToken = null)
		{
            vaultAddress ??= Environment.GetEnvironmentVariable("VAULT_ADDR");

			if (string.IsNullOrWhiteSpace(vaultAddress))
			{
				throw new VaultException("Could not determine VaultAddress");
			}

			string token = GetVaultToken(vaultToken);

			HttpClient client = new HttpClient();
			HttpRequestMessage request = new HttpRequestMessage();
			request.Headers.Add("X-Vault-Token", token);
			request.RequestUri = new Uri($"{vaultAddress}/v1/{path}");
            HttpResponseMessage? result = await client.SendAsync(request);
            string? response = await result.Content.ReadAsStringAsync();

			if (!result.IsSuccessStatusCode)
			{
				throw new VaultRequestException($"Error: querring vault at {vaultAddress}/v1/{path}. {(int)result.StatusCode} {result.ReasonPhrase}", response);
			}

			if (!(JObject.Parse(response)["data"] is JObject j))
            {
				throw new VaultDataException("Cannot parse 'data' element of Vault reponse as a json object",response);
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
			vaultToken ??= Environment.GetEnvironmentVariable("VAULT_TOKEN");

			if (!string.IsNullOrWhiteSpace(vaultToken))
			{
				return vaultToken;
			}

			string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			string tokenFile = Path.Combine(home, ".vault-token");

			if (!File.Exists(tokenFile))
			{
				throw new VaultTokenException($"Error: {tokenFile} is not found. Please login to vault first with 'vault login'");
			}

			string token = File.ReadAllText(tokenFile);
			return token;
		}
	}
}
