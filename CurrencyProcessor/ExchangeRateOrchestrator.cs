using CurrencyProcessor.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace CurrencyProcessor;

public static class ExchangeRateOrchestrator
{
    [FunctionName("ExchangeRateOrchestrator")]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context,
        ILogger log)
    {
        var currencyCodes = context.GetInput<string[]>();
        var pairs = GenerateCurrencyPairs(currencyCodes);
        log.LogInformation($"Processing exchange rates for {currencyCodes.Length} currencies");
        var tasks = new List<Task<Dictionary<string, decimal>>>();

        foreach (var baseCurrency in pairs)
        {
            var task = context.CallActivityAsync<Dictionary<string, decimal>>(
                "GetExchangeRatesActivity",
                (baseCurrency.BaseCurrency, baseCurrency.TargetCurrencies.ToArray()));
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        var cosmosDbConnectionString = await context.CallActivityAsync<string>(
            "GetCosmosDbConnectionStringActivity",
            null);

        foreach (var completedTask in tasks)
        {
            var rates = completedTask.Result;
            if (rates == null) continue;

            foreach (var rate in rates)
            {
                var currencyRate = new CurrencyRate
                {
                    Id = $"{rate.Key.Split('_')[0]}_{rate.Key.Split('_')[1]}",
                    From = rate.Key.Split('_')[0],
                    To = rate.Key.Split('_')[1],
                    Rate = rate.Value,
                    Timestamp = new DateTimeOffset(context.CurrentUtcDateTime).ToUnixTimeMilliseconds().ToString(),
                };

                await context.CallActivityAsync(
                    "SaveRateToCosmosDbActivity",
                    (currencyRate, cosmosDbConnectionString));
            }
        }

        log.LogInformation("Orchestration completed successfully");
    }

    private static List<CurrencyPairGroup> GenerateCurrencyPairs(string[] currencies)
    {
        var pairs = new List<CurrencyPairGroup>();

        for (int i = 0; i < currencies.Length; i++)
        {
            string baseCurrency = currencies[i];
            var targets = new List<string>();

            for (int j = i + 1; j < currencies.Length; j++)
            {
                targets.Add(currencies[j]);
            }

            if (targets.Count > 0)
            {
                pairs.Add(new CurrencyPairGroup { BaseCurrency = baseCurrency, TargetCurrencies = targets });
            }
        }

        return pairs;
    }
}