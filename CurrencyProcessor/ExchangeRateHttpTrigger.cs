using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Text.Json;

namespace CurrencyProcessor
{
    public static class ExchangeRateHttpTrigger
    {
        private static readonly List<string> AllowedCurrencies = new()
        {
            "USD", "EUR", "CHF", "GBP", "JPY", "CAD", "AUD", "CNY", "SEK", "NOK"
        };

        [FunctionName("ExchangeRateHttpTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]
            HttpRequest request,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            log.LogInformation("Processing currency request...");
            List<string>? inputCurrencies = null;

            if (request.Method == HttpMethods.Get)
            {
                inputCurrencies = request.Query["values"].ToList();
            }
            else if (request.Method == HttpMethods.Post)
            {
                try
                {
                    using var reader = new StreamReader(request.Body);
                    var body = await reader.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(body))
                    {
                        inputCurrencies = JsonSerializer.Deserialize<List<string>>(body, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }) ?? new List<string>();
                    }
                }
                catch
                {
                    return new BadRequestObjectResult("Invalid JSON format in request body.");
                }
            }
            else
            {
                return new BadRequestObjectResult("Unsupported HTTP method.");
            }

            if (inputCurrencies == null || !inputCurrencies.Any() || inputCurrencies.Count < 1)
            {
                return new BadRequestObjectResult("No currency codes provided.");
            }

            var distinctCurrencies = inputCurrencies.Select(c => c.ToUpperInvariant()).Distinct().ToList();

            if (distinctCurrencies.Any(c => !AllowedCurrencies.Contains(c)))
            {
                return new BadRequestObjectResult($"One or more currency codes are invalid.");
            }

            if (distinctCurrencies.Count < 1)
            {
                return new BadRequestObjectResult($"One or more currency codes are invalid.");
            }

            var instanceId = await starter.StartNewAsync("ExchangeRateOrchestrator", null, distinctCurrencies);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(request, instanceId);
        }
    }
}
