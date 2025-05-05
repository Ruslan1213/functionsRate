namespace CurrencyProcessor.Models
{
    public class CurrencyPairGroup
    {
        public string BaseCurrency { get; set; } = null!;

        public List<string> TargetCurrencies { get; set; } = null!;
    }
}
