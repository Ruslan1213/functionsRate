using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CurrencyProcessor.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace CurrencyProcessor;

public static class ActivityFunctions
{
    private static readonly HttpClient httpClient = new();

    [FunctionName("GetExchangeRatesActivity")]
    public static async Task<Dictionary<string, decimal>> GetExchangeRates(
        [ActivityTrigger] (string baseCurrency, string[] targetCurrencies) input,
        ILogger log)
    {
        var apiKey = await GetSecretFromKeyVault("ExchangeRateApiKey");

        var rates = new Dictionary<string, decimal>();
        var apiUrl = $"https://api.exchangerate-api.com/v4/latest/{input.baseCurrency}";

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await httpClient.GetAsync(apiUrl);

        if (!response.IsSuccessStatusCode)
        {
            log.LogError($"Failed to get rates for {input.baseCurrency}. Status: {response.StatusCode}");
            return rates;
        }

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);

        if (jsonDoc.RootElement.TryGetProperty("rates", out var ratesElement))
        {
            foreach (var targetCurrency in input.targetCurrencies)
            {
                if (ratesElement.TryGetProperty(targetCurrency, out var rateElement))
                {
                    var rate = rateElement.GetDecimal();
                    rates.Add($"{input.baseCurrency}_{targetCurrency}", rate);
                    log.LogInformation($"Rate {input.baseCurrency}->{targetCurrency}: {rate}");
                }
            }
        }

        return rates;
    }

    [FunctionName("GetCosmosDbConnectionStringActivity")]
    public static async Task<string> GetCosmosDbConnectionString(
        [ActivityTrigger] object input,
        ILogger log)
    {
        log.LogInformation("Retrieving Cosmos DB connection string from Key Vault");
        return await GetSecretFromKeyVault("CosmosDbConnectionString");
    }

    [FunctionName("SaveRateToCosmosDbActivity")]
    public static async Task SaveRateToCosmosDb(
    [ActivityTrigger] (CurrencyRate rate, string connectionString) input,
    ILogger log)
    {
        var rate = input.rate;
        var currencyPair = new[] { rate.From, rate.To }.OrderBy(c => c).ToArray();
        var normalizedFrom = currencyPair[0];
        var normalizedTo = currencyPair[1];

        log.LogInformation($"Saving normalized rate {normalizedFrom}->{normalizedTo} to Cosmos DB");

        try
        {
            var cosmosClient = new CosmosClient(input.connectionString);
            var database = cosmosClient.GetDatabase("ExchangeRatesDB");
            var container = database.GetContainer("Rates");
            var doc = new Dictionary<string, object>
        {
            { "id", $"{normalizedFrom}_{normalizedTo}" },
            { "from", normalizedFrom },
            { "to", normalizedTo },
            { "rate", rate.Rate },
            { "timestamp", rate.Timestamp }
        };

            await container.UpsertItemAsync(doc, new PartitionKey(normalizedFrom));
            log.LogInformation($"Cosmos DB Rate saved: {normalizedFrom}->{normalizedTo}");
        }
        catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.BadRequest)
        {
            log.LogError($"Cosmos DB BadRequest: {ce.Message}");
            log.LogError($"Cosmos DB Diagnostics: {ce.Data}");
            throw;
        }
        catch (Exception ex)
        {
            log.LogError(ex, $"Failed to save rate {rate.Id} to Cosmos DB");
            throw;
        }
    }

    private static async Task<string> GetSecretFromKeyVault(string secretName)
    {
        var keyVaultUrl = Environment.GetEnvironmentVariable("KeyVaultUrl") 
            ?? "https://currencyprocessorkv.vault.azure.net/";

        if (string.IsNullOrEmpty(keyVaultUrl))
        {
            throw new ArgumentNullException($"{nameof(keyVaultUrl)} is null");
        }

        var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
        var secret = await client.GetSecretAsync(secretName);

        return secret.Value.Value;
    }
}