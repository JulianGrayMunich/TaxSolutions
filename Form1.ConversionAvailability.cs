using Microsoft.VisualBasic.FileIO;
namespace TaxSolutions
{
    public partial class Form1
    {
        #region Conversion availability after validated selection
        private void InitializeConversionAvailability()
        {
            foreach (TextBox box in new[] { CsvFilePathTextBox, _revolutEurPath, _revolutGbpPath, _volksbankGiroPath, _volksbankCardPath })
                box.TextChanged += (sender, e) => RefreshConversionAvailability();
            RefreshConversionAvailability();
        }

        private void RefreshConversionAvailability()
        {
            RevolutExportButton.Enabled = HasSelectedBankCsv(target: _revolutEurPath) && HasSelectedBankCsv(target: _revolutGbpPath) &&
                !string.Equals(a: SelectedBankCsvPath(target: _revolutEurPath), b: SelectedBankCsvPath(target: _revolutGbpPath), comparisonType: StringComparison.OrdinalIgnoreCase);
            HalifaxExportButton.Enabled = HasSelectedBankCsv(target: CsvFilePathTextBox);
            _bankPages[2].Controls["ActionsGroup"]!.Controls[1].Enabled = HasSelectedBankCsv(target: _volksbankGiroPath) && HasSelectedBankCsv(target: _volksbankCardPath) &&
                !string.Equals(a: SelectedBankCsvPath(target: _volksbankGiroPath), b: SelectedBankCsvPath(target: _volksbankCardPath), comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSelectedBankCsv(TextBox target) => File.Exists(path: SelectedBankCsvPath(target: target));

        private void ValidateBankCsvSelection(TextBox target, string path, int bankIndex)
        {
            if (bankIndex == 0) ValidateRevolutSource(path: path, expectedCurrency: ReferenceEquals(objA: target, objB: _revolutEurPath) ? "EUR" : "GBP");
            else if (bankIndex == 2) ReadVolksbankSource(path: path, creditCard: ReferenceEquals(objA: target, objB: _volksbankCardPath), germanFormat: _volksbankGermanFormat.Checked);
            else
            {
                using TextFieldParser parser = new(path: path);
                parser.TextFieldType = FieldType.Delimited; parser.SetDelimiters(delimiters: new[] { "," }); parser.HasFieldsEnclosedInQuotes = true;
                try { ReadHalifaxHeaders(parser: parser); }
                catch (MalformedLineException ex) { throw new InvalidDataException(message: InvalidHalifaxFileMessage, innerException: ex); }
            }
        }
        #endregion
    }
}
