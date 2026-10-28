namespace TaxSolutions
{
    public partial class Form1
    {
        #region Shared transaction description replacements
        private static string NormalizeBankDescription(string description)
        {
            description = description.Trim();
            if (string.Equals(a: description, b: "LNK HUNTINGDON", comparisonType: StringComparison.OrdinalIgnoreCase)) return "HUNTINGDON OSTEOPA";
            if (description.Contains(value: "Frankfurt Flugha", comparisonType: StringComparison.OrdinalIgnoreCase)) return "Flughawe Frankfurt ";
            if (string.Equals(a: description, b: "SURVEY SUPPLIES LI", comparisonType: StringComparison.OrdinalIgnoreCase)) return "SURVEY SUPPLIES IRELAND";
            if (string.Equals(a: description, b: "Deutsche Post AG", comparisonType: StringComparison.OrdinalIgnoreCase)) return "DE Pension: Deutsche Post AG";
            if (string.Equals(a: description, b: "WWW.HEATHROWEXPRESS.CO", comparisonType: StringComparison.OrdinalIgnoreCase)) return "Heathrow Express";
            if (string.Equals(a: description, b: "OPENAI *CHATGPT SUBSCR", comparisonType: StringComparison.OrdinalIgnoreCase)) return "OpenAI ChatGPT";
            if (string.Equals(a: description, b: "AMZNMktplace", comparisonType: StringComparison.OrdinalIgnoreCase)) return "Amazon";
            if (description.Contains(value: "AMZN Mktp", comparisonType: StringComparison.OrdinalIgnoreCase)) return "Amazon";
            if (description.Contains(value: "eBay", comparisonType: StringComparison.OrdinalIgnoreCase)) return "eBay";
            if (description.Contains(value: "LUFTHAN", comparisonType: StringComparison.OrdinalIgnoreCase)) return "Lufthansa";
            return description;
        }

        private static decimal RequiredMonthlyGbpToEurFactor(ReportConfiguration settings, DateTime date, string bankName)
        {
            string monthKey = date.ToString(format: "yyyy-MM", provider: System.Globalization.CultureInfo.InvariantCulture);
            if (!settings.MonthlyGbpToEurRates.TryGetValue(key: monthKey, value: out decimal factor) || factor <= 0m ||
                decimal.Round(d: factor, decimals: RateDecimalPlaces, mode: MidpointRounding.AwayFromZero) != factor)
                throw new InvalidDataException(message: "Save a positive six-decimal GBP-to-EUR factor for " + monthKey + " before converting " + bankName + ". No exports were written.");
            return factor;
        }
        #endregion
    }
}




