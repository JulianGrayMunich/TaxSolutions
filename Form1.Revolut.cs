using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace TaxSolutions
{
    public partial class Form1
    {
        #region Revolut combined income and expenses
        private void RevolutExportButton_Click(object? sender, EventArgs e)
        {
            try
            {
                BankExportResult result = ConvertRevolut(eurCsvPath: SelectedBankCsvPath(target: _revolutEurPath), gbpCsvPath: SelectedBankCsvPath(target: _revolutGbpPath));
                _comments[0].Text = result.Summary;
                ShowExportSuccess(summary: result.Summary);
            }
            catch (InvalidDataException ex)
            {
                _comments[0].Text = ex.Message;
                MessageBox.Show(owner: this, text: ex.Message, caption: "Revolut Conversion", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                _comments[0].Text = ex.Message;
                MessageBox.Show(owner: this, text: ex.Message, caption: "Conversion Failed", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Error);
            }
        }

        private BankExportResult ConvertRevolut(string eurCsvPath, string gbpCsvPath)
        {
            if (string.IsNullOrWhiteSpace(value: eurCsvPath) || string.IsNullOrWhiteSpace(value: gbpCsvPath))
                throw new InvalidDataException(message: "Select both the EUR CSV and GBP CSV on the Revolut tab.");
            string eurPath = Path.GetFullPath(path: eurCsvPath);
            string gbpPath = Path.GetFullPath(path: gbpCsvPath);
            if (string.Equals(a: eurPath, b: gbpPath, comparisonType: StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(message: "Select separate EUR and GBP source files.");
            ValidateRevolutSource(path: eurPath, expectedCurrency: "EUR");
            ValidateRevolutSource(path: gbpPath, expectedCurrency: "GBP");
            ReportConfiguration settings = ReadBankReportConfiguration(bankName: "Revolut");
            DateTime firstDay = new(year: settings.Year, month: settings.FromMonth, day: 1);
            DateTime lastDay = new(year: settings.Year, month: settings.ToMonth, day: DateTime.DaysInMonth(year: settings.Year, month: settings.ToMonth));
            List<BankReportRow> income = new();
            List<BankReportRow> expenses = new();
            int ignored = 0;
            int outsidePeriod = 0;
            int zeroAmounts = 0;
            ReadRevolutTransactions(path: eurPath, expectedCurrency: "EUR", settings: settings, firstDay: firstDay, lastDay: lastDay,
                income: income, expenses: expenses, ignored: ref ignored, outsidePeriod: ref outsidePeriod, zeroAmounts: ref zeroAmounts);
            ReadRevolutTransactions(path: gbpPath, expectedCurrency: "GBP", settings: settings, firstDay: firstDay, lastDay: lastDay,
                income: income, expenses: expenses, ignored: ref ignored, outsidePeriod: ref outsidePeriod, zeroAmounts: ref zeroAmounts);
            income = DescriptionDateSort(rows: income, getDate: row => row.Date, getColumn2: row => row.Description);
            expenses = DescriptionDateSort(rows: expenses, getDate: row => row.Date, getColumn2: row => row.Description);
            string folder = Path.Combine(path1: Path.GetDirectoryName(path: eurPath) ?? throw new InvalidDataException(message: "The EUR source folder is unavailable."), path2: ConvertedFolderName);
            string dates = firstDay.ToString(format: "yyyy-MM-dd", provider: CultureInfo.InvariantCulture) + "_" + lastDay.ToString(format: "yyyy-MM-dd", provider: CultureInfo.InvariantCulture);
            List<BankOutputFile> outputs = new()
            {
                CreateRevolutOutput(folder: folder, fileName: "Revolut_Income_" + dates + ".csv", header: IncomeExportHeader, rows: income),
                CreateRevolutOutput(folder: folder, fileName: "Revolut_Expenses_" + dates + ".csv", header: ExpensesExportHeader, rows: expenses)
            };
            PublishBankOutputs(outputs: outputs);
            string summary = $"Revolut conversion completed.\r\nReport period: {firstDay:yyyy-MM-dd} to {lastDay:yyyy-MM-dd}.\r\n" +
                $"Income: {income.Count}; expenses: {expenses.Count}; excluded types: {ignored}; outside period: {outsidePeriod}; zero amounts: {zeroAmounts}.\r\n" +
                "Save folder: " + folder + "\r\n";
            foreach (BankOutputFile output in outputs) summary += Path.GetFileName(path: output.Destination) + "\r\n";
            return new BankExportResult(Summary: summary.TrimEnd());
        }
        #endregion

        #region Revolut source classification and currency conversion
        private void ReadRevolutTransactions(string path, string expectedCurrency, ReportConfiguration settings,
            DateTime firstDay, DateTime lastDay, List<BankReportRow> income, List<BankReportRow> expenses,
            ref int ignored, ref int outsidePeriod, ref int zeroAmounts)
        {
            using TextFieldParser parser = new(path: path);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(delimiters: new[] { "," });
            parser.HasFieldsEnclosedInQuotes = true;
            parser.TrimWhiteSpace = false;
            parser.ReadFields(); // Header was validated in full before reading either source.
            while (!parser.EndOfData)
            {
                long lineNumber = parser.LineNumber;
                string[] fields;
                try { fields = parser.ReadFields() ?? Array.Empty<string>(); }
                catch (MalformedLineException ex) { throw new InvalidDataException(message: $"Revolut {expectedCurrency} line {lineNumber}: malformed CSV. No exports were written.", innerException: ex); }
                if (fields.Length != 10)
                    throw new InvalidDataException(message: $"Revolut {expectedCurrency} line {lineNumber}: expected ten columns. No exports were written.");
                if (!string.Equals(a: fields[7].Trim(), b: expectedCurrency, comparisonType: StringComparison.Ordinal))
                    throw new InvalidDataException(message: $"Revolut {expectedCurrency} line {lineNumber}: Currency must be {expectedCurrency}. No exports were written.");
                string description = fields[4].Trim();
                if (IsExcludedMerchant(description: description))
                {
                    ignored++;
                    continue;
                }
                description = NormalizeBankDescription(description: NormalizeRevolutDescription(description: description));
                DateTime? date = TryParseRevolutDate(value: fields[2].Trim());
                if (date is null) throw new InvalidDataException(message: $"Revolut {expectedCurrency} line {lineNumber}: invalid Started Date '{fields[2]}'. No exports were written.");
                if (date.Value < firstDay || date.Value > lastDay) { outsidePeriod++; continue; }
                string amountText = fields[5].Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(input: amountText, pattern: @"^[+-]?[0-9]+(?:\.[0-9]{1,2})?$") ||
                    !decimal.TryParse(s: amountText, style: NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, provider: CultureInfo.InvariantCulture, result: out decimal amount))
                    throw new InvalidDataException(message: $"Revolut {expectedCurrency} line {lineNumber}: invalid Amount '{amountText}'. No exports were written.");
                if (amount == 0m) { zeroAmounts++; continue; }
                bool sourceIsGbp = expectedCurrency == "GBP";
                decimal factor = RequiredMonthlyGbpToEurFactor(settings: settings, date: date.Value, bankName: "Revolut");
                if (amount > 0m)
                {
                    decimal gbp = sourceIsGbp ? amount : decimal.Round(d: amount / factor, decimals: 2, mode: MidpointRounding.AwayFromZero);
                    income.Add(item: new BankReportRow(Date: date.Value, Description: description, Amount: gbp, EuroAmount: sourceIsGbp ? decimal.Round(d: amount * factor, decimals: 2, mode: MidpointRounding.AwayFromZero) : amount, ConversionFactor: factor));
                }
                else
                {
                    decimal originalDebit = decimal.Negate(d: amount);
                    decimal gbp = sourceIsGbp ? originalDebit : decimal.Round(d: originalDebit / factor, decimals: 2, mode: MidpointRounding.AwayFromZero);
                    decimal eur = sourceIsGbp ? decimal.Round(d: originalDebit * factor, decimals: 2, mode: MidpointRounding.AwayFromZero) : originalDebit;
                    expenses.Add(item: new BankReportRow(Date: date.Value, Description: description, Amount: gbp, EuroAmount: eur, ConversionFactor: factor));
                }
                // State and Fee are deliberately not used: retain the previous treatment, without new exclusions or fee adjustments.
            }
        }
        #endregion

        #region Revolut output destinations
        private static BankOutputFile CreateRevolutOutput(string folder, string fileName, string header, List<BankReportRow> rows)
        {
            string destination = Path.Combine(path1: folder, path2: fileName);
            string unique = Guid.NewGuid().ToString(format: "N");
            return new BankOutputFile
            {
                Destination = destination, Header = header, Rows = rows, SourceBank = SourceBankRevolut,
                TemporaryPath = destination + "." + unique + ".tmp",
                BackupPath = destination + "." + unique + ".bak"
            };
        }
        #endregion
    }
}


