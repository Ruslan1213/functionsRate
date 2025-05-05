namespace CurrencyProcessor.Models
{
    public class CurrencyRate
    {
        public string Id { get; set; } = string.Empty;

        public string From { get; set; } = string.Empty;

        public string To { get; set; } = string.Empty;

        public decimal Rate { get; set; }

        public string Timestamp { get; set; } = string.Empty;

    }
}
