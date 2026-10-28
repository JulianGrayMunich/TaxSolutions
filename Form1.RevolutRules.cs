namespace TaxSolutions
{
    public partial class Form1
    {
        #region Revolut exclusion rules against original descriptions
        private readonly record struct RevolutExclusionRule(string Type, string Description);
        private static readonly RevolutExclusionRule[] RevolutExclusionRules =
        {
            new(Type: "Deposit", Description: "Payment from J GRAY"),
            new(Type: "Deposit", Description: "Payment from JULIAN GRAY"),
            new(Type: "Transfer", Description: "To violeta kolkowsky"),
            new(Type: "Transfer", Description: "SWIFT transfer to Gerda Gl Gray"),
            new(Type: "Transfer", Description: "To Raxhimi Suzana"),
            new(Type: "Transfer", Description: "To Olgita Adhamidhi"),
            new(Type: "Transfer", Description: "To PAS Dr. Hammerl"),
            new(Type: "Transfer", Description: "SWIFT transfer to julian gray")
        };

        private static readonly string[] RevolutExcludedCardMerchants =
        {
            "EDEKA",
            "REWE.de",
            "Munchen Airport Center",
            "ROSSMANN",
            "St. Leonhards-Apotheke",
            "PENNY",
            "LIDL",
            "Catawiki",
            "Rosi Kaffeehaus",
            "Fielmann",
            "Airport Oliva",
            "ALDI",
            "Tesco",
            "Sainsbury's",
            "Sandford House",
            "Pret A Manger",
            "Milano Cafe Pizzeria",
            "Robinsons",
            "Relay",
            "Caffè Nero",
            "Boots",
            "H.Samuel",
            "WhSmith",
            "Tg Jones Huntin",
            "delink.teebbi.com",
            "FCB Coffee",
            "Neptun Riviera",
            "Otavalo Handel",
            "Rosemarie Schw",
            "Swi24",
            "Lorenc Zeneli Person Fi",
            "Fam Asia Market",
            "Gasthof Siegertsbrunn Fra",
            "Jj Darboven",
            "Mvz Dermatologie Suedb"
        };

        #endregion

        #region Revolut output description normalization
        private static string NormalizeRevolutDescription(string description)
        {
            description = RemoveLeadingRevolutPhrase(description: description, phrase: "Payment from");
            description = RemoveLeadingRevolutPhrase(description: description, phrase: "To");
            if (string.Equals(a: description, b: "Metal plan fee", comparisonType: StringComparison.OrdinalIgnoreCase))
                return "Bank charges";
            return description;
        }
        private static string RemoveLeadingRevolutPhrase(string description, string phrase)
        {
            if (!description.StartsWith(value: phrase, comparisonType: StringComparison.OrdinalIgnoreCase)) return description;
            if (description.Length == phrase.Length) return string.Empty;
            if (!char.IsWhiteSpace(c: description[phrase.Length])) return description;
            return description.Substring(startIndex: phrase.Length).TrimStart();
        }
        #endregion
    }
}

