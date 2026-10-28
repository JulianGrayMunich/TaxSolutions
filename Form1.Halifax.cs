using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace TaxSolutions
{
    public partial class Form1
    {
        #region Bank export contracts
        private static readonly string[] HalifaxRequiredHeaders =
        {
            "Transaction Date", "Transaction Type", "Sort Code", "Account Number",
            "Transaction Description", "Debit Amount", "Credit Amount", "Balance"
        };
        private const string IncomeExportHeader = "Transaction Date,,Transaction Description,Credit (GBP),Credit (EUR),Conversion Factor,Source Bank";
        private const string ExpensesExportHeader = "Transaction Date,,Transaction Description,Debit (GBP),Debit (EUR),Conversion Factor,Source Bank";
        private const string HalifaxCurrencyToken = "GBP";
        private const string HalifaxIncomeToken = "Income";
        private const string HalifaxExpensesToken = "Expenses";

        private readonly record struct BankReportRow(DateTime Date, string Description, decimal Amount, decimal? EuroAmount = null, decimal ConversionFactor = 1m, string? SourceBank = null);
        private sealed record BankExportResult(string Summary);

        private sealed class BankOutputFile
        {
            public required string Destination { get; init; }
            public required string Header { get; init; }
            public required string SourceBank { get; init; }
            public required List<BankReportRow> Rows { get; init; }
            public required string TemporaryPath { get; init; }
            public required string BackupPath { get; init; }
            public bool OriginalMoved { get; set; }
            public bool Published { get; set; }
        }
        #endregion

        #region Halifax configuration and header validation
        private static ReportConfiguration ReadBankReportConfiguration(string bankName)
        {
            if (!File.Exists(path: ConfigurationPath))
                throw new InvalidDataException(message: "Save the report period and conversion factors on Configuration before converting " + bankName + ".");
            ReportConfiguration settings;
            try
            {
                settings = JsonSerializer.Deserialize<ReportConfiguration>(json: File.ReadAllText(path: ConfigurationPath))
                    ?? throw new InvalidDataException(message: "The saved report configuration is empty.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(message: "The saved report configuration is invalid. Review and save Configuration.", innerException: ex);
            }
            if (settings.Year < 1900 || settings.Year > 9999 || settings.FromMonth < 1 || settings.ToMonth > 12 ||
                settings.ToMonth < settings.FromMonth || settings.ToMonth - settings.FromMonth >= MaximumReportMonths ||
                settings.ExchangeRateDefinition != RateDefinition || settings.MonthlyGbpToEurRates is null)
                throw new InvalidDataException(message: "The saved report period or GBP-to-EUR factor definition is invalid. Review and save Configuration.");
            return settings;
        }

        private static Dictionary<string, int> ReadHalifaxHeaders(TextFieldParser parser)
        {
            string[] headers = parser.ReadFields() ?? Array.Empty<string>();
            Dictionary<string, int> columns = new(comparer: StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i].Trim().TrimStart(trimChar: '\uFEFF');
                if (!columns.TryAdd(key: header, value: i))
                    throw new InvalidDataException(message: InvalidHalifaxFileMessage);
            }
            List<string> missing = new();
            foreach (string expected in HalifaxRequiredHeaders)
                if (!columns.ContainsKey(key: expected)) missing.Add(item: expected);
            if (missing.Count > 0 || headers.Length != HalifaxRequiredHeaders.Length)
                throw new InvalidDataException(message: InvalidHalifaxFileMessage);
            return columns;
        }

        private static decimal ParseHalifaxAmount(string text, long lineNumber, string columnName)
        {
            if (string.IsNullOrWhiteSpace(value: text)) return 0m;
            if (!System.Text.RegularExpressions.Regex.IsMatch(input: text.Trim(), pattern: @"^\+?(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)(?:\.[0-9]{1,2})?$") ||
                !decimal.TryParse(s: text.Trim(), style: NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                    provider: CultureInfo.GetCultureInfo(name: "en-GB"), result: out decimal amount) || amount < 0m ||
                decimal.Round(d: amount, decimals: 2, mode: MidpointRounding.AwayFromZero) != amount)
                throw new InvalidDataException(message: $"Halifax line {lineNumber}: {columnName} must be a nonnegative GBP amount with no more than two decimal places: '{text}'.");
            return amount;
        }
        #endregion

        #region Halifax classification and monthly conversion
        private BankExportResult ConvertHalifax(string inputCsvPath)
        {
            // Validate the source before opening any output file.
            using TextFieldParser parser = new(path: inputCsvPath);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(delimiters: new[] { "," });
            parser.HasFieldsEnclosedInQuotes = true;
            parser.TrimWhiteSpace = false;
            Dictionary<string, int> columns;
            try { columns = ReadHalifaxHeaders(parser: parser); }
            catch (MalformedLineException ex) { throw new InvalidDataException(message: InvalidHalifaxFileMessage, innerException: ex); }
            ReportConfiguration settings = ReadBankReportConfiguration(bankName: "Halifax");
            DateTime firstDay = new(year: settings.Year, month: settings.FromMonth, day: 1);
            DateTime lastDay = new(year: settings.Year, month: settings.ToMonth, day: DateTime.DaysInMonth(year: settings.Year, month: settings.ToMonth));
            List<BankReportRow> income = new();
            List<BankReportRow> expenses = new();
            int ignored = 0;
            int outsidePeriod = 0;
            int zeroAmounts = 0;
            while (!parser.EndOfData)
            {
                long lineNumber = parser.LineNumber;
                string[] fields;
                try { fields = parser.ReadFields() ?? Array.Empty<string>(); }
                catch (MalformedLineException ex) { throw new InvalidDataException(message: $"Halifax line {lineNumber}: malformed CSV. No exports were written.", innerException: ex); }
                if (fields.Length != HalifaxRequiredHeaders.Length)
                    throw new InvalidDataException(message: $"Halifax line {lineNumber}: expected eight columns; found {fields.Length}. No exports were written.");
                string type = fields[columns["Transaction Type"]].Trim();
                string description = fields[columns["Transaction Description"]].Trim();
                if (IsExcludedMerchant(description: description)) { ignored++; continue; }
                if (string.Equals(a: type, b: "TFR", comparisonType: StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a: type, b: "SO", comparisonType: StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(a: type, b: "FPO", comparisonType: StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(a: description, b: "JULIAN ANDRE LE FR", comparisonType: StringComparison.OrdinalIgnoreCase)))
                {
                    ignored++;
                    continue;
                }
                string dateText = fields[columns["Transaction Date"]].Trim();
                DateTime? date = TryParseHalifaxDate(value: dateText);
                if (date is null) throw new InvalidDataException(message: $"Halifax line {lineNumber}: invalid Transaction Date '{dateText}'. No exports were written.");
                if (date.Value < firstDay || date.Value > lastDay) { outsidePeriod++; continue; }
                if (string.Equals(a: description, b: "OUR CHARGE", comparisonType: StringComparison.OrdinalIgnoreCase)) description = "Bank charges";
                decimal credit = ParseHalifaxAmount(text: fields[columns["Credit Amount"]], lineNumber: lineNumber, columnName: "Credit Amount");
                decimal debit = ParseHalifaxAmount(text: fields[columns["Debit Amount"]], lineNumber: lineNumber, columnName: "Debit Amount");
                if (credit > 0m && debit > 0m)
                    throw new InvalidDataException(message: $"Halifax line {lineNumber}: both Credit Amount and Debit Amount are positive. No exports were written.");
                if (credit > 0m)
                {
                    decimal incomeFactor = RequiredMonthlyGbpToEurFactor(settings: settings, date: date.Value, bankName: "Halifax");
                    income.Add(item: new BankReportRow(Date: date.Value, Description: description, Amount: credit, EuroAmount: decimal.Round(d: credit * incomeFactor, decimals: 2, mode: MidpointRounding.AwayFromZero), ConversionFactor: incomeFactor));
                    continue;
                }
                if (string.Equals(a: type, b: "FPI", comparisonType: StringComparison.OrdinalIgnoreCase) && debit > 0m)
                    throw new InvalidDataException(message: $"Halifax line {lineNumber}: FPI has a debit but no credit. No exports were written.");
                if (debit == 0m) { zeroAmounts++; continue; }
                string monthKey = date.Value.ToString(format: "yyyy-MM", provider: CultureInfo.InvariantCulture);
                if (!settings.MonthlyGbpToEurRates.TryGetValue(key: monthKey, value: out decimal factor) || factor <= 0m ||
                    decimal.Round(d: factor, decimals: RateDecimalPlaces, mode: MidpointRounding.AwayFromZero) != factor)
                    throw new InvalidDataException(message: "Save a positive six-decimal GBP-to-EUR factor for " + monthKey + " before converting Halifax. No exports were written.");
                decimal euros = decimal.Round(d: debit * factor, decimals: 2, mode: MidpointRounding.AwayFromZero);
                expenses.Add(item: new BankReportRow(Date: date.Value, Description: description, Amount: debit, EuroAmount: euros, ConversionFactor: factor));
            }
            income = income.ConvertAll(converter: row => row with { Description = NormalizeBankDescription(description: row.Description) });
            expenses = expenses.ConvertAll(converter: row => row with { Description = NormalizeBankDescription(description: row.Description) });
            income = DescriptionDateSort(rows: income, getDate: row => row.Date, getColumn2: row => row.Description);
            expenses = DescriptionDateSort(rows: expenses, getDate: row => row.Date, getColumn2: row => row.Description);
            List<BankOutputFile> outputs = new()
            {
                CreateHalifaxOutput(inputCsvPath: inputCsvPath, token: HalifaxIncomeToken, header: IncomeExportHeader, rows: income),
                CreateHalifaxOutput(inputCsvPath: inputCsvPath, token: HalifaxExpensesToken, header: ExpensesExportHeader, rows: expenses)
            };
            PublishBankOutputs(outputs: outputs);
            string summary = $"Halifax conversion completed.\r\nReport period: {firstDay:yyyy-MM-dd} to {lastDay:yyyy-MM-dd}.\r\n" +
                $"Income: {income.Count}; expenses: {expenses.Count}; excluded types: {ignored}; outside period: {outsidePeriod}; zero amounts: {zeroAmounts}.\r\n";
            summary += "Save folder: " + Path.GetDirectoryName(path: outputs[0].Destination) + "\r\n";
            foreach (BankOutputFile output in outputs) summary += Path.GetFileName(path: output.Destination) + "\r\n";
            return new BankExportResult(Summary: summary.TrimEnd());
        }
        #endregion

        #region Halifax naming and shared CSV publication
        private static BankOutputFile CreateHalifaxOutput(string inputCsvPath, string token, string header, List<BankReportRow> rows)
        {
            string fullInputPath = Path.GetFullPath(path: inputCsvPath);
            string folder = Path.GetDirectoryName(path: fullInputPath) ?? throw new InvalidDataException(message: "The source folder could not be determined.");
            string[] parts = Path.GetFileNameWithoutExtension(path: fullInputPath).Split(separator: '_');
            int currencyIndex = -1;
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.Equals(a: parts[i], b: HalifaxCurrencyToken, comparisonType: StringComparison.OrdinalIgnoreCase)) continue;
                if (currencyIndex >= 0) throw new InvalidDataException(message: "The Halifax source filename must contain exactly one GBP currency token.");
                currencyIndex = i;
            }
            if (currencyIndex < 0) throw new InvalidDataException(message: "The Halifax source filename must contain a GBP currency token separated by underscores.");
            parts[currencyIndex] = token;
            folder = Path.Combine(path1: folder, path2: ConvertedFolderName);
            string destination = Path.Combine(path1: folder, path2: string.Join(separator: "_", value: parts) + ".csv");
            string uniqueSuffix = Guid.NewGuid().ToString(format: "N");
            return new BankOutputFile
            {
                Destination = destination, Header = header, Rows = rows, SourceBank = SourceBankHalifax,
                TemporaryPath = destination + "." + uniqueSuffix + ".tmp",
                BackupPath = destination + "." + uniqueSuffix + ".bak"
            };
        }

        private static string EscapeBankCsvField(string value)
        {
            if (value.IndexOfAny(anyOf: new[] { ',', '"', '\r', '\n' }) < 0) return value;
            return "\"" + value.Replace(oldValue: "\"", newValue: "\"\"", comparisonType: StringComparison.Ordinal) + "\"";
        }

        private static void PublishBankOutputs(List<BankOutputFile> outputs)
        {
            foreach (BankOutputFile output in outputs)
                Directory.CreateDirectory(path: Path.GetDirectoryName(path: output.Destination) ?? throw new InvalidDataException(message: "The Converted folder could not be determined."));
            bool committed = false;
            try
            {
                // Fail before replacing files if an existing export is held open by another application.
                foreach (BankOutputFile output in outputs)
                    if (File.Exists(path: output.Destination))
                    {
                        using FileStream check = new(path: output.Destination, mode: FileMode.Open, access: FileAccess.ReadWrite, share: FileShare.None);
                    }
                foreach (BankOutputFile output in outputs)
                {
                    using StreamWriter writer = new(path: output.TemporaryPath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    writer.WriteLine(value: output.Header);
                    foreach (BankReportRow row in output.Rows)
                    {
                        string[] fields = new string[7];
                        fields[0] = row.Date.ToString(format: "yyyy-MM-dd", provider: CultureInfo.InvariantCulture);
                        fields[1] = string.Empty;
                        fields[2] = row.Description;
                        fields[3] = row.Amount.ToString(format: "F2", provider: CultureInfo.InvariantCulture);
                        fields[4] = (row.EuroAmount ?? throw new InvalidDataException(message: "Missing EUR amount in bank export.")).ToString(format: "F2", provider: CultureInfo.InvariantCulture);
                        fields[5] = row.ConversionFactor.ToString(format: "F6", provider: CultureInfo.InvariantCulture);
                        fields[6] = row.SourceBank ?? output.SourceBank;
                        for (int column = 0; column < fields.Length; column++) fields[column] = EscapeBankCsvField(value: fields[column]);
                        writer.WriteLine(value: string.Join(separator: ",", value: fields));
                    }
                }
                foreach (BankOutputFile output in outputs)
                {
                    if (File.Exists(path: output.Destination))
                    {
                        File.Move(sourceFileName: output.Destination, destFileName: output.BackupPath);
                        output.OriginalMoved = true;
                    }
                    File.Move(sourceFileName: output.TemporaryPath, destFileName: output.Destination);
                    output.Published = true;
                }
                committed = true;
            }
            catch (Exception original)
            {
                List<Exception> failures = new() { original };
                for (int i = outputs.Count - 1; i >= 0; i--)
                {
                    BankOutputFile output = outputs[i];
                    try
                    {
                        if (output.Published) File.Delete(path: output.Destination);
                        if (output.OriginalMoved) File.Move(sourceFileName: output.BackupPath, destFileName: output.Destination);
                    }
                    catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException)
                    {
                        failures.Add(item: new IOException(message: "Could not restore an earlier export. Preserved backup: " + output.BackupPath, innerException: rollback));
                    }
                }
                if (failures.Count > 1) throw new AggregateException(message: "Export publication failed; review preserved backup files.", innerExceptions: failures);
                throw;
            }
            finally
            {
                foreach (BankOutputFile output in outputs)
                {
                    if (File.Exists(path: output.TemporaryPath)) File.Delete(path: output.TemporaryPath);
                    if (committed && File.Exists(path: output.BackupPath)) File.Delete(path: output.BackupPath);
                }
            }
        }
        #endregion
    }
}




