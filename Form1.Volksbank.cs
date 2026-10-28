using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace TaxSolutions
{
    public partial class Form1
    {
        #region Volksbank source recognition and parsing
        private sealed record VolksbankSource(List<(long Line, string[] Fields)> Rows, int HeaderIndex);

        private static VolksbankSource ReadVolksbankSource(string path, bool creditCard, bool germanFormat)
        {
            string content;
            try { content = File.ReadAllText(path: path, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)); }
            catch (DecoderFallbackException)
            {
                Encoding.RegisterProvider(provider: CodePagesEncodingProvider.Instance);
                content = File.ReadAllText(path: path, encoding: Encoding.GetEncoding(codepage: 1252));
            }
            using StringReader reader = new(s: content);
            using TextFieldParser parser = new(reader: reader);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(delimiters: new[] { germanFormat ? ";" : "," });
            parser.HasFieldsEnclosedInQuotes = true;
            parser.TrimWhiteSpace = false;
            List<(long Line, string[] Fields)> rows = new();
            bool identified = false;
            int header = -1;
            string marker = creditCard ? "Kreditkartenkonto" : "VR-Girokonto Online";
            while (!parser.EndOfData)
            {
                long line = parser.LineNumber;
                string[] fields;
                try { fields = parser.ReadFields() ?? Array.Empty<string>(); }
                catch (MalformedLineException ex) { throw new InvalidDataException(message: $"Volksbank line {line}: malformed CSV. No exports were written.", innerException: ex); }
                if (fields.Length > 0 && fields[0].Contains(value: marker, comparisonType: StringComparison.OrdinalIgnoreCase)) identified = true;
                if (fields.Length >= 12 && fields[4].Trim() == "Buchungstag" && fields[11].Trim() == "Betrag")
                {
                    if (header >= 0) throw new InvalidDataException(message: "Volksbank contains repeated transaction headers. No exports were written.");
                    if (!creditCard && fields[6].Trim() != "Name Zahlungsbeteiligter")
                        throw new InvalidDataException(message: "Volksbank Giro column G must be Name Zahlungsbeteiligter.");
                    if (creditCard && fields[10].Trim() is not ("Verwendungszweck" or "Verwendingswek"))
                        throw new InvalidDataException(message: "Volksbank Credit Card column K must be Verwendungszweck.");
                    header = rows.Count;
                }
                rows.Add(item: (line, fields));
            }
            if (!identified) throw new InvalidDataException(message: creditCard ? "This is not a Volksbank Credit Card file" : "This is not a Volksbank Giro file");
            if (header < 0) throw new InvalidDataException(message: "Volksbank transaction headers were not found in the expected columns E to L. Check German format.");
            return new VolksbankSource(Rows: rows, HeaderIndex: header);
        }

        private void ReadVolksbankTransactions(VolksbankSource source, bool creditCard, bool germanFormat, ReportConfiguration settings,
            DateTime firstDay, DateTime lastDay, List<BankReportRow> income, List<BankReportRow> expenses,
            ref int ignored, ref int outsidePeriod, ref int zeroAmounts)
        {
            for (int i = source.HeaderIndex + 1; i < source.Rows.Count; i++)
            {
                (long line, string[] fields) = source.Rows[i];
                if (Array.TrueForAll(array: fields, match: field => string.IsNullOrWhiteSpace(value: field))) continue;
                if (fields.Length < 12) throw new InvalidDataException(message: $"Volksbank line {line}: expected at least twelve columns. No exports were written.");
                if (creditCard && fields[9].Contains(value: "Gutschrift", comparisonType: StringComparison.OrdinalIgnoreCase) &&
                    fields[10].Contains(value: "Abrechnung vom", comparisonType: StringComparison.OrdinalIgnoreCase)) { ignored++; continue; }
                if (Array.Exists(array: fields, match: field => field.Contains(value: "Julian Andre Le Fraper Gray", comparisonType: StringComparison.OrdinalIgnoreCase))) { ignored++; continue; }
                string originalDescription = fields[creditCard ? 10 : 6];
                if (IsExcludedMerchant(description: originalDescription)) { ignored++; continue; }
                string description = creditCard ? Regex.Split(input: originalDescription.Trim(), pattern: " {3,}")[0].Trim() : originalDescription.Trim();
                if (string.Equals(a: description, b: "meine Volksbank Raiffeisenbank eG", comparisonType: StringComparison.OrdinalIgnoreCase)) { ignored++; continue; }
                if (string.IsNullOrWhiteSpace(value: description)) description = "Volksbank bank charges";
                string[] dateFormats = { "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" };
                if (!DateTime.TryParseExact(s: fields[4].Trim(), formats: dateFormats, provider: CultureInfo.InvariantCulture, style: DateTimeStyles.None, result: out DateTime date))
                    throw new InvalidDataException(message: $"Volksbank line {line}: invalid Buchungstag '{fields[4]}'. No exports were written.");
                if (date < firstDay || date > lastDay) { outsidePeriod++; continue; }
                string amountText = fields[11].Trim();
                string pattern = germanFormat ? @"^[+-]?[0-9]+(?:,[0-9]{1,2})?$" : @"^[+-]?[0-9]+(?:\.[0-9]{1,2})?$";
                CultureInfo culture = germanFormat ? CultureInfo.GetCultureInfo(name: "de-DE") : CultureInfo.InvariantCulture;
                if (!Regex.IsMatch(input: amountText, pattern: pattern) || !decimal.TryParse(s: amountText, style: NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, provider: culture, result: out decimal amount))
                    throw new InvalidDataException(message: $"Volksbank line {line}: invalid Betrag '{amountText}' for the selected format. No exports were written.");
                if (amount == 0m) { zeroAmounts++; continue; }
                string monthKey = date.ToString(format: "yyyy-MM", provider: CultureInfo.InvariantCulture);
                if (!settings.MonthlyGbpToEurRates.TryGetValue(key: monthKey, value: out decimal factor) || factor <= 0m ||
                    decimal.Round(d: factor, decimals: RateDecimalPlaces, mode: MidpointRounding.AwayFromZero) != factor)
                    throw new InvalidDataException(message: "Save a positive six-decimal GBP-to-EUR factor for " + monthKey + " before converting Volksbank. No exports were written.");
                decimal euros = decimal.Abs(value: amount);
                decimal pounds = decimal.Round(d: euros / factor, decimals: 2, mode: MidpointRounding.AwayFromZero);
                BankReportRow row = new(Date: date, Description: NormalizeBankDescription(description: description), Amount: pounds, EuroAmount: euros, ConversionFactor: factor);
                if (amount < 0m) expenses.Add(item: row); else income.Add(item: row);
            }
        }
        #endregion

        #region Combined Volksbank conversion and publication
        private void VolksbankExportButton_Click(object? sender, EventArgs e)
        {
            try
            {
                BankExportResult result = ConvertVolksbank(giroCsvPath: SelectedBankCsvPath(target: _volksbankGiroPath), cardCsvPath: SelectedBankCsvPath(target: _volksbankCardPath), germanFormat: _volksbankGermanFormat.Checked);
                _comments[2].Text = result.Summary;
                ShowExportSuccess(summary: result.Summary);
            }
            catch (Exception ex)
            {
                _comments[2].Text = ex.Message;
                MessageBox.Show(owner: this, text: ex.Message, caption: "Volksbank Conversion", buttons: MessageBoxButtons.OK, icon: ex is InvalidDataException ? MessageBoxIcon.Warning : MessageBoxIcon.Error);
            }
        }

        private BankExportResult ConvertVolksbank(string giroCsvPath, string cardCsvPath, bool germanFormat)
        {
            if (string.IsNullOrWhiteSpace(value: giroCsvPath) || string.IsNullOrWhiteSpace(value: cardCsvPath))
                throw new InvalidDataException(message: "Select both the Giro CSV and Credit Card CSV on the Volksbank tab.");
            string giroPath = Path.GetFullPath(path: giroCsvPath);
            string cardPath = Path.GetFullPath(path: cardCsvPath);
            if (string.Equals(a: giroPath, b: cardPath, comparisonType: StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(message: "Select separate Giro and Credit Card source files.");
            VolksbankSource giro = ReadVolksbankSource(path: giroPath, creditCard: false, germanFormat: germanFormat);
            VolksbankSource card = ReadVolksbankSource(path: cardPath, creditCard: true, germanFormat: germanFormat);
            ReportConfiguration settings = ReadBankReportConfiguration(bankName: "Volksbank");
            DateTime firstDay = new(year: settings.Year, month: settings.FromMonth, day: 1);
            DateTime lastDay = new(year: settings.Year, month: settings.ToMonth, day: DateTime.DaysInMonth(year: settings.Year, month: settings.ToMonth));
            List<BankReportRow> income = new();
            List<BankReportRow> expenses = new();
            int ignored = 0, outsidePeriod = 0, zeroAmounts = 0;
            ReadVolksbankTransactions(source: giro, creditCard: false, germanFormat: germanFormat, settings: settings, firstDay: firstDay, lastDay: lastDay, income: income, expenses: expenses, ignored: ref ignored, outsidePeriod: ref outsidePeriod, zeroAmounts: ref zeroAmounts);
            ReadVolksbankTransactions(source: card, creditCard: true, germanFormat: germanFormat, settings: settings, firstDay: firstDay, lastDay: lastDay, income: income, expenses: expenses, ignored: ref ignored, outsidePeriod: ref outsidePeriod, zeroAmounts: ref zeroAmounts);
            income = DescriptionDateSort(rows: income, getDate: row => row.Date, getColumn2: row => row.Description);
            expenses = DescriptionDateSort(rows: expenses, getDate: row => row.Date, getColumn2: row => row.Description);
            string folder = Path.Combine(path1: Path.GetDirectoryName(path: giroPath) ?? throw new InvalidDataException(message: "The Giro source folder is unavailable."), path2: ConvertedFolderName);
            string dates = firstDay.ToString(format: "yyyy-MM-dd", provider: CultureInfo.InvariantCulture) + "_" + lastDay.ToString(format: "yyyy-MM-dd", provider: CultureInfo.InvariantCulture);
            List<BankOutputFile> outputs = new();
            foreach (bool isIncome in new[] { true, false })
            {
                string destination = Path.Combine(path1: folder, path2: "Volksbank_" + (isIncome ? "Income_" : "Expenses_") + dates + ".csv");
                string unique = Guid.NewGuid().ToString(format: "N");
                outputs.Add(item: new BankOutputFile { Destination = destination, Header = isIncome ? IncomeExportHeader : ExpensesExportHeader,
                    Rows = isIncome ? income : expenses, SourceBank = SourceBankVolksbank, TemporaryPath = destination + "." + unique + ".tmp", BackupPath = destination + "." + unique + ".bak" });
            }
            PublishBankOutputs(outputs: outputs);
            string summary = $"Volksbank conversion completed.\r\nReport period: {firstDay:yyyy-MM-dd} to {lastDay:yyyy-MM-dd}.\r\nIncome: {income.Count}; expenses: {expenses.Count}; excluded: {ignored}; outside period: {outsidePeriod}; zero amounts: {zeroAmounts}.\r\nSave folder: {folder}\r\n";
            foreach (BankOutputFile output in outputs) summary += Path.GetFileName(path: output.Destination) + "\r\n";
            return new BankExportResult(Summary: summary.TrimEnd());
        }
        #endregion
    }
}



