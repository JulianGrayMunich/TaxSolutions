using System.Text;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace TaxSolutions
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            SetConversionButtonsEnabled(false);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SetCompiledDateLabel();
        }


        private void BrowseCsvButton_Click(object sender, EventArgs e)
        {
            using OpenFileDialog dialog = new OpenFileDialog();

            dialog.Title = "Select CSV File";
            dialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
            dialog.Multiselect = false;

            DialogResult result = dialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                CsvFilePathTextBox.Text = dialog.FileName;
            }
        }

        private void DeutscheBankFormatButton_Click(object sender, EventArgs e)
        {
            string? csvPath = TryGetSelectedCsvPath();
            if (csvPath is null)
            {
                return;
            }

            try
            {
                ConvertDeutscheBank(csvPath);

                MessageBox.Show(
                    this,
                    "Deutsche Bank conversion completed.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Conversion Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }





        private static string[] SplitHalifaxCsvLine(string line)
        {
            // Your sample contains a leading apostrophe in Sort Code field and no quoted commas.
            // Deterministic/simple split by comma is therefore correct for the provided format.
            return line.Split(',', StringSplitOptions.None);
        }

        private static DateTime? TryParseHalifaxDate(string value)
        {
            // Halifax sample uses dd/MM/yyyy.
            string[] formats = new[]
            {
        "dd/MM/yyyy",
        "d/M/yyyy"
    };

            if (DateTime.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsed))
            {
                return parsed.Date;
            }

            return null;
        }

        private void ConvertDeutscheBank(string inputCsvPath)
        {
            string[] allLines = File.ReadAllLines(inputCsvPath);

            if (allLines.Length < 10)
            {
                throw new InvalidOperationException("Input file does not contain sufficient rows for Deutsche Bank conversion.");
            }

            // Strip rows 1–8 (1-based) => remove first 8 lines (0..7)
            // Strip last row => remove final line


            int headerIndex = 7;          // 0-based row 8
            int firstDataIndex = 8;       // 0-based row 9
            int endExclusive = allLines.Length - 1; // strip last row


            if (endExclusive <= firstDataIndex)
            {
                throw new InvalidOperationException("After stripping header/footer rows, no data remains to process.");
            }

            // Transform and parse
            // The first remaining row should be the header row.
            string headerLine = NormalizeDeutscheBankLine(allLines[headerIndex]);

            if (!headerLine.StartsWith("Booking date", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected Deutsche Bank header row was not found after stripping rows 1–8.");
            }

            List<OutputRow> dataRows = new List<OutputRow>();

            for (int i = firstDataIndex; i < endExclusive; i++)
            {
                string normalized = NormalizeDeutscheBankLine(allLines[i]);

                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                string[] cols = SplitCsvLine(normalized);

                // Need columns 1,4,16,17,18 => indices 0,3,15,16,17
                if (cols.Length <= 17)
                {
                    // Deterministic: skip malformed lines rather than guessing structure.
                    continue;
                }

                string debitRaw = cols[15].Trim();    // source col 16
                string credit = cols[16].Trim();      // source col 17
                string currency = cols[17].Trim();    // source col 18

                // Your existing logic flips col 16 (multiply by -1) for the OUTPUT debit field.
                // Keep that same behaviour here:
                string debit = "";

                if (!string.IsNullOrWhiteSpace(debitRaw))
                {
                    if (!decimal.TryParse(
                            debitRaw,
                            NumberStyles.Number | NumberStyles.AllowLeadingSign,
                            CultureInfo.InvariantCulture,
                            out decimal debitValue))
                    {
                        throw new InvalidOperationException($"Unable to parse column 16 as a number: '{debitRaw}'.");
                    }

                    debit = (debitValue * -1m).ToString(CultureInfo.InvariantCulture);
                }

                string bookingDateRaw = cols[0].Trim();
                DateTime? bookingDate = TryParseDbankDate(bookingDateRaw);

                // Deterministic: rows without a valid booking date cannot participate in month grouping.
                if (bookingDate is null)
                {
                    continue;
                }

                string beneficiary = cols[3].Trim();

                // Skip internal transfer line (deterministic exclusion rule)
                if (string.Equals(
                        beneficiary,
                        "DEUTSCHE KREDITBANK AKTIENGESELLSCH AFT",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                // Column 2 sort key (keep your existing hardening)
                string beneficiarySortKey = MakeColumn2SortKey(beneficiary);

                // Extract already-normalised values from projected string is NOT acceptable.
                // We must reconstruct directly from parsed columns.



                // projected format currently:
                // 0 = BookingDate
                // 1 = Invoice (blank)
                // 2 = Beneficiary
                // 3 = Credit
                // 4 = Debit
                // 5 = Currency
                // 6 = TaxCode (blank)

                dataRows.Add(new OutputRow(
                    BookingDate: bookingDate.Value.Date,
                    Invoice: "",
                    Beneficiary: beneficiary,
                    Credit: credit,
                    Debit: debit,
                    Currency: currency,
                    TaxCode: "",
                    SortKey: beneficiarySortKey
                ));

            }

            if (dataRows.Count == 0)
            {
                throw new InvalidOperationException("No transaction rows were parsed after stripping and normalization.");
            }

            // Sort by column 4 (string), then by column 1 (date if parseable, else raw string)
            dataRows = MonthGroupedSort(dataRows, r => r.BookingDate, r => r.SortKey);


            string yyyymm = dataRows[0].BookingDate.ToString("yyyyMM", CultureInfo.InvariantCulture);

            WriteOutputCsv(
                inputCsvPath,
                "Dbank",
                yyyymm,
                dataRows,
                TaxCodeRules);

        }

        private static string NormalizeDeutscheBankLine(string line)
        {
            // Remove all double quotes (if any), remove all commas, replace semicolons with commas.
            // Note: removing commas is per your stated specification, even though it can alter free-text fields.
            string normalized = line.Replace("\"", string.Empty, StringComparison.Ordinal);

            normalized = normalized.Replace(",", string.Empty, StringComparison.Ordinal);

            normalized = normalized.Replace(";", ",", StringComparison.Ordinal);

            return normalized;
        }

        private static string[] SplitCsvLine(string csvLine)
        {
            // After normalization, commas are structural separators and do not occur in data.
            return csvLine.Split(',', StringSplitOptions.None);
        }

        private static string ProjectColumns(string[] cols)
        {
            // Columns 1,4,16,17,18 => indices 0,3,15,16,17
            if (cols.Length <= 17)
            {
                return string.Empty;
            }

            string c1Raw = cols[0].Trim();
            string c4 = cols[3].Trim();

            string c16Raw = cols[15].Trim();
            string c17 = cols[16].Trim();
            string c18 = cols[17].Trim();

            // ---- Column 1: Normalize date to yyyy-MM-dd ----
            string c1;

            if (string.IsNullOrWhiteSpace(c1Raw))
            {
                c1 = string.Empty;
            }
            else
            {
                DateTime? parsedDate = TryParseDbankDate(c1Raw);

                if (parsedDate is null)
                {
                    throw new InvalidOperationException($"Unable to parse column 1 as a date: '{c1Raw}'.");
                }

                c1 = parsedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            // ---- Column 16: Multiply by -1 if present ----
            string c16;

            if (string.IsNullOrWhiteSpace(c16Raw))
            {
                c16 = string.Empty;
            }
            else
            {
                if (!decimal.TryParse(
                        c16Raw,
                        NumberStyles.Number | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out decimal c16Value))
                {
                    throw new InvalidOperationException($"Unable to parse column 16 as a number: '{c16Raw}'.");
                }

                decimal c16Flipped = c16Value * -1m;
                c16 = c16Flipped.ToString(CultureInfo.InvariantCulture);
            }

            // Insert blank Invoice between columns 1 and 2; swap Debit/Credit positions
            return string.Join(",", c1, string.Empty, c4, c17, c16, c18, string.Empty);

        }

        private static DateTime? TryParseDbankDate(string value)
        {
            // Explicit formats only. Extend as required once you confirm all encountered formats.
            string[] formats = new[]
            {
        "M/d/yyyy",
        "MM/dd/yyyy",
        "d/M/yyyy",
        "dd/MM/yyyy",
        "yyyy-MM-dd"
    };

            if (DateTime.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsed))
            {
                return parsed.Date;
            }

            return null;
        }


        private readonly record struct DbankRow(
    DateTime OutputDate,
    string OutputCol2SortKey,
    string ProjectedLine);







        private string? TryGetSelectedCsvPath()
        {
            string? csvPath = CsvFilePathTextBox.Text;

            if (string.IsNullOrWhiteSpace(csvPath))
            {
                MessageBox.Show(
                    this,
                    "Select a CSV file first.",
                    "Missing Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }

            if (!File.Exists(csvPath))
            {
                MessageBox.Show(this,
                    "The selected file does not exist.\nReselect the CSV file.",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return null;
            }

            return csvPath;
        }


        private void ConvertHalifax(string inputCsvPath)
        {
            string[] allLines = File.ReadAllLines(inputCsvPath);

            if (allLines.Length < 2)
            {
                throw new InvalidOperationException("Input file does not contain sufficient rows for Halifax conversion.");
            }

            // Row 1 is header. Data starts at row 2.
            int headerIndex = 0;
            int firstDataIndex = 1;
            int endExclusive = allLines.Length;

            string headerLine = allLines[headerIndex];

            if (!headerLine.StartsWith("Transaction Date", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Input file does not appear to be a Halifax export (header mismatch).");
            }

            List<OutputRow> dataRows = new();

            for (int i = firstDataIndex; i < endExclusive; i++)
            {
                string line = allLines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cols = SplitHalifaxCsvLine(line);

                // Need columns 1,5,6,7 => indices 0,4,5,6
                if (cols.Length < 7)
                {
                    continue;
                }

                string dateRaw = cols[0].Trim();
                DateTime? bookingDate = TryParseHalifaxDate(dateRaw);

                if (bookingDate is null)
                {
                    throw new InvalidOperationException($"Unable to parse column 1 as a date: '{dateRaw}'.");
                }

                string transactionType = cols[1].Trim(); // Column 2
                string description = cols[4].Trim();     // Column 5

                // Your existing Halifax rule
                if (string.Equals(transactionType, "TFR", StringComparison.OrdinalIgnoreCase))
                {
                    description = "(TFR) " + description;
                }

                if (string.Equals(description, "OUR CHARGE", StringComparison.OrdinalIgnoreCase))
                {
                    description = "Bank charges";
                }

                if (string.Equals(description, "JULIAN ANDRE LE FR", StringComparison.OrdinalIgnoreCase))
                {
                    description = "(TFR) J GRAY";
                }
                // Optional: your already-tested Halifax replacement work may be here already.
                // Example (if you already implemented it elsewhere, keep it there):
                // if (string.Equals(description, "OUR CHARGE", StringComparison.OrdinalIgnoreCase))
                // {
                //     description = "Bank charges";
                // }

                // Source col 6/7 are already in "Debit" and "Credit" positions for Halifax exports.
                // Destination requires Credit then Debit (you already swapped in your earlier output formatting).
                string debit = cols[5].Trim();   // Source col 6
                string credit = cols[6].Trim();  // Source col 7







                string sortKey = description;

                dataRows.Add(new OutputRow(
                    BookingDate: bookingDate.Value.Date,
                    Invoice: "",
                    Beneficiary: description,
                    Credit: credit,
                    Debit: debit,
                    Currency: "GBP",
                    TaxCode: "",
                    SortKey: sortKey
                ));
            }

            if (dataRows.Count == 0)
            {
                throw new InvalidOperationException("No data rows were parsed for Halifax conversion.");
            }

            dataRows = MonthGroupedSort(dataRows, r => r.BookingDate, r => r.SortKey);

            string yyyymm = dataRows[0].BookingDate.ToString("yyyyMM", CultureInfo.InvariantCulture);

            WriteOutputCsv(
                inputCsvPath,
                "Halifax",
                yyyymm,
                dataRows,
                TaxCodeRules);
        }











        private void HalifaxExportButton_Click(object sender, EventArgs e)
        {
            string? csvPath = TryGetSelectedCsvPath();
            if (csvPath is null)
            {
                return;
            }

            try
            {
                ConvertHalifax(csvPath);

                MessageBox.Show(this,
                    "Halifax conversion completed.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    ex.Message,
                    "Conversion Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }









        private void RevolutExportButton_Click(object sender, EventArgs e)
        {
            string? csvPath = TryGetSelectedCsvPath();
            if (csvPath is null)
            {
                return;
            }

            try
            {
                ConvertRevolut(csvPath);

                MessageBox.Show(this,
                    "Revolut conversion completed.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    ex.Message,
                    "Conversion Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        private void ConvertRevolut(string inputCsvPath)
        {
            string[] allLines = File.ReadAllLines(inputCsvPath);

            if (allLines.Length < 2)
            {
                throw new InvalidOperationException("Input file does not contain sufficient rows for Revolut conversion.");
            }

            // Row 1 is header (1-based). Ignore it.
            int headerIndex = 0;
            int firstDataIndex = 1;
            int endExclusive = allLines.Length;

            string headerLine = RemoveQuotes(allLines[headerIndex]);

            if (!headerLine.StartsWith("Art,Produkt,Datum des Beginns", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Input file does not appear to be a Revolut export (header mismatch).");
            }

            List<OutputRow> dataRows = new();

            for (int i = firstDataIndex; i < endExclusive; i++)
            {
                string line = RemoveQuotes(allLines[i]);

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cols = SplitRevolutCsvLine(line);

                // Need source columns: 3,5,6,8 => indices 2,4,5,7
                if (cols.Length < 8)
                {
                    continue;
                }

                string dateRaw = cols[2].Trim();          // Source col 3
                string description = cols[4].Trim();      // Source col 5
                string amountRaw = cols[5].Trim();        // Source col 6
                string currency = cols[7].Trim();         // Source col 8


                if (description.StartsWith("Umgetauscht in ", StringComparison.OrdinalIgnoreCase))
                {
                    description = "(Private) Inter-account transfer";
                }

                if (description.StartsWith("Zahlung von ", StringComparison.OrdinalIgnoreCase))
                {
                    description = "(Private) Inter-account transfer";
                }

                DateTime? bookingDate = TryParseRevolutDate(dateRaw);
                if (bookingDate is null)
                {
                    throw new InvalidOperationException($"Unable to parse column 3 as a date: '{dateRaw}'.");
                }

                if (!decimal.TryParse(
                        amountRaw,
                        NumberStyles.Number | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out decimal amount))
                {
                    throw new InvalidOperationException($"Unable to parse column 6 as a number: '{amountRaw}'.");
                }

                string credit = "";
                string debit = "";

                if (amount > 0m)
                {
                    credit = amount.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    // amount < 0 => multiply by -1; amount == 0 => treat as debit (0)
                    debit = (amount * -1m).ToString(CultureInfo.InvariantCulture);
                }

                dataRows.Add(new OutputRow(
                    BookingDate: bookingDate.Value.Date,
                    Invoice: "",
                    Beneficiary: description,
                    Credit: credit,
                    Debit: debit,
                    Currency: currency,
                    TaxCode: "",
                    SortKey: description
                ));
            }

            if (dataRows.Count == 0)
            {
                throw new InvalidOperationException("No data rows were parsed for Revolut conversion.");
            }

            dataRows = MonthGroupedSort(dataRows, r => r.BookingDate, r => r.SortKey);

            string yyyymm = dataRows[0].BookingDate.ToString("yyyyMM", CultureInfo.InvariantCulture);

            string fileCurrency = dataRows[0].Currency;
            if (string.IsNullOrWhiteSpace(fileCurrency))
            {
                throw new InvalidOperationException("Unable to determine currency for file naming (currency was empty on first sorted row).");
            }

            WriteOutputCsv(
                inputCsvPath,
                "Revolut",
                $"{yyyymm}_{fileCurrency}",
                dataRows,
                TaxCodeRules);
        }




        private static string RemoveQuotes(string value)
        {
            return value.Replace("\"", string.Empty, StringComparison.Ordinal);
        }

        private static string[] SplitRevolutCsvLine(string line)
        {
            // Sample data has no quoted commas. Deterministic split by comma.
            return line.Split(',', StringSplitOptions.None);
        }

        private static DateTime? TryParseRevolutDate(string value)
        {
            // Confirmed: yyyy-MM-dd HH:mm:ss
            string[] formats = new[]
            {
        "yyyy-MM-dd HH:mm:ss"
    };

            if (DateTime.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsed))
            {
                return parsed.Date;
            }

            return null;
        }


        private static string MakeColumn2SortKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // Normalise and remove Control/Format characters, then trim ALL Unicode whitespace.
            string normalised = value.Normalize(NormalizationForm.FormKC);

            StringBuilder sb = new StringBuilder(normalised.Length);

            for (int i = 0; i < normalised.Length; i++)
            {
                char c = normalised[i];
                UnicodeCategory category = char.GetUnicodeCategory(c);

                if (category == UnicodeCategory.Control || category == UnicodeCategory.Format)
                {
                    continue;
                }

                sb.Append(c);
            }

            // Trim Unicode whitespace explicitly (handles NBSP and related).
            return sb.ToString().Trim(UnicodeWhitespaceChars);
        }



        private static string ProjectHeaderColumns(string[] headerCols)
        {
            // Columns 1,4,16,17,18 => indices 0,3,15,16,17
            if (headerCols.Length <= 17)
            {
                throw new InvalidOperationException("Header row does not contain the expected number of columns.");
            }

            string h1 = headerCols[0].Trim();
            string h4 = headerCols[3].Trim();
            string h16 = headerCols[15].Trim();
            string h17 = headerCols[16].Trim();
            string h18 = headerCols[17].Trim();

            // Insert Invoice between columns 1 and 2; swap Debit/Credit positions
            return string.Join(",", h1, "Invoice", h4, h17, h16, h18);

        }


        private static List<T> MonthGroupedSort<T>(
    List<T> rows,
    Func<T, DateTime> getDate,
    Func<T, string> getColumn2)
        {
            if (rows is null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            // Step 1: Global chronological order (required by spec).
            rows.Sort((a, b) => DateTime.Compare(getDate(a), getDate(b)));

            // Step 2: Group by month key derived from the date.
            // Step 3: Within each month group, sort by column 2 then by date.
            Dictionary<string, List<T>> monthBuckets = new Dictionary<string, List<T>>(StringComparer.Ordinal);

            for (int i = 0; i < rows.Count; i++)
            {
                T row = rows[i];
                string monthKey = getDate(row).ToString("yyyyMM", CultureInfo.InvariantCulture);

                if (!monthBuckets.TryGetValue(monthKey, out List<T>? bucket))
                {
                    bucket = new List<T>();
                    monthBuckets.Add(monthKey, bucket);
                }

                bucket.Add(row);
            }

            // Month keys are yyyyMM, so Ordinal sort is chronological.
            List<string> orderedMonths = monthBuckets.Keys.ToList();
            orderedMonths.Sort(StringComparer.Ordinal);

            List<T> final = new List<T>(rows.Count);

            for (int m = 0; m < orderedMonths.Count; m++)
            {
                string month = orderedMonths[m];
                List<T> bucket = monthBuckets[month];

                bucket.Sort((x, y) =>
                {
                    int byCol2 = string.Compare(getColumn2(x), getColumn2(y), StringComparison.OrdinalIgnoreCase);
                    if (byCol2 != 0)
                    {
                        return byCol2;
                    }

                    return DateTime.Compare(getDate(x), getDate(y));
                });

                final.AddRange(bucket);
            }

            return final;
        }

        private static readonly char[] UnicodeWhitespaceChars =
{
    ' ', '\t', '\r', '\n',
    '\u00A0', // NO-BREAK SPACE
    '\u1680', // OGHAM SPACE MARK
    '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A',
    '\u202F', // NARROW NO-BREAK SPACE
    '\u205F', // MEDIUM MATHEMATICAL SPACE
    '\u3000'  // IDEOGRAPHIC SPACE
};







        private void ConvertCreditCard(string inputCsvPath)
        {
            string[] allLines = File.ReadAllLines(inputCsvPath);

            if (allLines.Length < 4)
            {
                throw new InvalidOperationException("Input file does not contain sufficient rows for Credit Card conversion.");
            }

            // Row 3 is header (1-based) => index 2
            int headerIndex = 2;
            int firstDataIndex = 3;

            string headerLine = allLines[headerIndex].Trim();

            if (!headerLine.StartsWith("Authorised on;", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Input file does not appear to be a Miles & More Credit Card export (header mismatch on row 3).");
            }

            List<(DateTime Date, string AmountRaw, string Currency, string Description)> parsed = new();

            for (int i = firstDataIndex; i < allLines.Length; i++)
            {
                string line = allLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Source file is semicolon-separated.
                string[] cols = line.Split(';', StringSplitOptions.None);

                // Need at least: col1 date, col3 amount, col4 currency, col5 description => indices 0,2,3,4
                if (cols.Length < 5)
                {
                    continue;
                }

                string dateRaw = cols[0].Trim();       // col 1
                string amountRaw = cols[2].Trim();     // col 3
                string currency = cols[3].Trim();      // col 4
                string description = cols[4].Trim();   // col 5

                // Skip internal transfer line (deterministic exclusion rule)
                if (string.Equals(
                        description,
                        "Lastschrift",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }



                if (string.Equals(
                    description,
                    "Einzahlung",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }





                if (string.Equals(
                    description,
                    "Monthly bank charges",
                    StringComparison.OrdinalIgnoreCase))
                {
                    description = "Bank charges";
                }


                if (!DateTime.TryParseExact(
                        dateRaw,
                        "dd.MM.yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime dt))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(amountRaw))
                {
                    continue;
                }

                parsed.Add((dt.Date, amountRaw, currency, description));
            }

            if (parsed.Count == 0)
            {
                throw new InvalidOperationException("No usable Credit Card data rows were parsed.");
            }

            // Latest date = max date in col1
            DateTime latest = parsed.Max(x => x.Date);

            // Earliest included = first day of the month, 3 months before latest month (inclusive) => 4-month window
            DateTime start = new DateTime(latest.Year, latest.Month, 1).AddMonths(-3);

            // Filter to last 4 months subset
            var subset = parsed
                .Where(x => x.Date >= start && x.Date <= latest)
                .ToList();

            if (subset.Count == 0)
            {
                throw new InvalidOperationException("After applying the 4-month filter, no rows remained.");
            }

            List<OutputRow> dataRows = new();

            for (int i = 0; i < subset.Count; i++)
            {
                var r = subset[i];

                // ---- Amount normalisation ----
                // Replace "," with "." in amount text (German decimal comma)
                string amt = r.AmountRaw.Replace(",", ".", StringComparison.Ordinal);

                // Rule: if char 7 from right is '.' then remove it (thousands separator)
                if (amt.Length >= 7)
                {
                    int idx = amt.Length - 7;
                    if (amt[idx] == '.')
                    {
                        amt = amt.Remove(idx, 1);
                    }
                }

                if (!decimal.TryParse(
                        amt,
                        NumberStyles.Number | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out decimal amount))
                {
                    continue;
                }

                // ---- Description normalisation ----
                string desc = r.Description;

                desc = desc.Replace("*", string.Empty, StringComparison.Ordinal);

                if (desc.Contains("1,95% für Währungsumrechn", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("1.95% für Währungsumrechn", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("1.95% fÃ¼r WÃ¤hrungsumrechn", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("1,95% fÃ¼r WÃ¤hrungsumrechn", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("monatlicher Kartenpreis", StringComparison.OrdinalIgnoreCase)

                    )
                {
                    desc = "Bank charge";
                }

                string credit = "";
                string debit = "";

                if (amount >= 0m)
                {
                    credit = amount.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    debit = (amount * -1m).ToString(CultureInfo.InvariantCulture);
                }

                dataRows.Add(new OutputRow(
                    BookingDate: r.Date,
                    Invoice: "",
                    Beneficiary: desc,
                    Credit: credit,
                    Debit: debit,
                    Currency: r.Currency,
                    TaxCode: "",
                    SortKey: desc
                ));
            }

            if (dataRows.Count == 0)
            {
                throw new InvalidOperationException("No output rows remained after parsing/normalisation.");
            }

            // Collapse "Bank charge" lines per month into a single "Monthly bank charges" line.
            dataRows = CollapseMonthlyBankCharges(dataRows);


            dataRows = MonthGroupedSort(dataRows, x => x.BookingDate, x => x.SortKey);

            string yyyymm = dataRows[0].BookingDate.ToString("yyyyMM", CultureInfo.InvariantCulture);

            WriteOutputCsv(
                inputCsvPath,
                "CreditCard",
                yyyymm,
                dataRows,
                TaxCodeRules);
        }





        private void CreditCardExportButton_Click(object sender, EventArgs e)
        {
            string? csvPath = TryGetSelectedCsvPath();
            if (csvPath is null) return;

            try
            {
                ConvertCreditCard(csvPath);
                MessageBox.Show(this, "Credit Card conversion completed.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Single, unified destination header for all converters.
        private const string OutputHeader =
            "Booking date,Invoice,Beneficiary / Originator,Credit,Debit,Currency,Tax code";

        // Single, unified destination row model for all converters.
        private readonly record struct OutputRow(
            DateTime BookingDate,
            string Invoice,
            string Beneficiary,
            string Credit,
            string Debit,
            string Currency,
            string TaxCode,
            string SortKey
        );

        private static string FormatOutputRow(OutputRow row)
        {
            return string.Join(",",
                row.BookingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.Invoice,
                row.Beneficiary,
                row.Credit,
                row.Debit,
                row.Currency,
                row.TaxCode
            );
        }

        private static void WriteOutputCsv(
            string inputCsvPath,
            string filePrefix,
            string yyyymm,
            IEnumerable<OutputRow> rows,
            IReadOnlyList<TaxCodeRule> taxCodeRules)
        {
            if (string.IsNullOrWhiteSpace(inputCsvPath))
                throw new ArgumentException("Input path was empty.", nameof(inputCsvPath));

            if (string.IsNullOrWhiteSpace(filePrefix))
                throw new ArgumentException("File prefix was empty.", nameof(filePrefix));

            if (string.IsNullOrWhiteSpace(yyyymm))
                throw new ArgumentException("YYYYMM was empty.", nameof(yyyymm));

            if (rows is null)
                throw new ArgumentNullException(nameof(rows));

            if (taxCodeRules is null)
                throw new ArgumentNullException(nameof(taxCodeRules));

            string inputFolder = Path.GetDirectoryName(inputCsvPath)
                ?? throw new InvalidOperationException("Unable to determine input folder.");

            string outputPath = Path.Combine(inputFolder, $"{filePrefix}_{yyyymm}.csv");

            using StreamWriter writer = new StreamWriter(outputPath, false);
            writer.WriteLine(OutputHeader);
            foreach (var r in rows)
            {
                string taxCode = ResolveTaxCode(r.Beneficiary, taxCodeRules, defaultTaxCode: "999");

                taxCode = ApplyCreditTaxCodeOverride(r, taxCode);

                OutputRow withTax = r with { TaxCode = taxCode };

                writer.WriteLine(ToOutputCsvLine(withTax));
            }
        }


        private static string ToOutputCsvLine(OutputRow r)
        {
            // Deterministic CSV line generation (consistent ordering with OUTPUT_HEADER).
            return string.Join(",",
                r.BookingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.Invoice ?? string.Empty,
                r.Beneficiary ?? string.Empty,
                r.Credit ?? string.Empty,
                r.Debit ?? string.Empty,
                r.Currency ?? string.Empty,
                r.TaxCode ?? string.Empty);
        }


        private static List<OutputRow> CollapseMonthlyBankCharges(List<OutputRow> rows)
        {
            if (rows is null) throw new ArgumentNullException(nameof(rows));

            // Separate bank-charge rows from everything else.
            List<OutputRow> nonCharges = new(rows.Count);
            List<OutputRow> charges = new();

            for (int i = 0; i < rows.Count; i++)
            {
                OutputRow r = rows[i];

                if (string.Equals(r.Beneficiary, "Bank charge", StringComparison.OrdinalIgnoreCase))
                {
                    charges.Add(r);
                }
                else
                {
                    nonCharges.Add(r);
                }
            }

            // If there are no bank charges, return unchanged.
            if (charges.Count == 0)
            {
                return rows;
            }

            // Group charges by yyyyMM.
            Dictionary<string, List<OutputRow>> byMonth = new(StringComparer.Ordinal);

            for (int i = 0; i < charges.Count; i++)
            {
                OutputRow r = charges[i];
                string key = r.BookingDate.ToString("yyyyMM", CultureInfo.InvariantCulture);

                if (!byMonth.TryGetValue(key, out List<OutputRow>? bucket))
                {
                    bucket = new List<OutputRow>();
                    byMonth.Add(key, bucket);
                }

                bucket.Add(r);
            }

            // For each month, replace with one summary row.
            foreach (var kvp in byMonth)
            {
                List<OutputRow> bucket = kvp.Value;

                DateTime latestDate = bucket[0].BookingDate;
                decimal debitSum = 0m;
                decimal creditSum = 0m;
                string currency = bucket[0].Currency;

                for (int i = 0; i < bucket.Count; i++)
                {
                    OutputRow r = bucket[i];

                    if (r.BookingDate > latestDate) latestDate = r.BookingDate;

                    // Currency: keep deterministic; first non-empty wins.
                    if (string.IsNullOrWhiteSpace(currency) && !string.IsNullOrWhiteSpace(r.Currency))
                    {
                        currency = r.Currency;
                    }

                    debitSum += ParseInvariantDecimalOrZero(r.Debit);
                    creditSum += ParseInvariantDecimalOrZero(r.Credit);
                }

                decimal net = debitSum - creditSum; // as specified

                string debit = "";
                string credit = "";

                if (net > 0m)
                {
                    debit = net.ToString(CultureInfo.InvariantCulture);
                }
                else if (net < 0m)
                {
                    credit = (-net).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    // Preserve a deterministic value for a zero-net month.
                    debit = "0";
                }

                // Add the single replacement row
                nonCharges.Add(new OutputRow(
                    BookingDate: latestDate,
                    Invoice: "",
                    Beneficiary: "Monthly bank charges",
                    Credit: credit,
                    Debit: debit,
                    Currency: currency,
                    TaxCode: "",
                    SortKey: "Monthly bank charges"
                ));
            }

            return nonCharges;
        }

        private static decimal ParseInvariantDecimalOrZero(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0m;

            if (decimal.TryParse(
                    value,
                    NumberStyles.Number | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out decimal parsed))
            {
                return parsed;
            }

            // Deterministic: treat malformed numeric as 0 rather than guessing.
            return 0m;
        }

        private void TaxCodeImportButton_Click(object sender, EventArgs e)
        {
            string? csvPath = TryGetSelectedCsvPath();
            if (csvPath is null)
            {
                return;
            }

            string[] allLines = File.ReadAllLines(csvPath);

            List<TaxCodeRule> rules = new(allLines.Length);

            for (int i = 0; i < allLines.Length; i++)
            {
                string line = allLines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                char delimiter = DetectDelimiter(line);

                // Deterministic split; if description contains delimiters, re-join tail.
                string[] cols = line.Split(delimiter, StringSplitOptions.None);

                if (cols.Length < 3)
                {
                    // Deterministic: skip malformed lines.
                    continue;
                }

                string sollHaben = UnquoteAndTrim(cols[0]);
                string taxCode = UnquoteAndTrim(cols[1]);

                string description;
                if (cols.Length == 3)
                {
                    description = UnquoteAndTrim(cols[2]);
                }
                else
                {
                    description = UnquoteAndTrim(string.Join(delimiter, cols.Skip(2)));
                }

                if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(taxCode))
                {
                    continue;
                }

                rules.Add(new TaxCodeRule(
                    SollHaben: sollHaben,
                    TaxCode: taxCode,
                    Description: description
                ));
            }

            if (rules.Count == 0)
            {
                _taxCodeRules = new List<TaxCodeRule>();
                SetConversionButtonsEnabled(false);

                MessageBox.Show(this,
                    "No usable tax code rows were parsed.\nExpected format per line: Soll/Haben,Tax code,Description (no header).",
                    "Import Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            // Deterministic ordering (stable use later when scanning).
            rules.Sort((a, b) =>
            {
                int byDesc = string.Compare(a.Description, b.Description, StringComparison.OrdinalIgnoreCase);
                if (byDesc != 0) return byDesc;

                int byCode = string.Compare(a.TaxCode, b.TaxCode, StringComparison.OrdinalIgnoreCase);
                if (byCode != 0) return byCode;

                return string.Compare(a.SollHaben, b.SollHaben, StringComparison.OrdinalIgnoreCase);
            });

            _taxCodeRules = rules;

            SetConversionButtonsEnabled(true);

            MessageBox.Show(this,
                $"Tax code import completed.\nRows loaded: {_taxCodeRules.Count}",
                "Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            CsvFilePathTextBox.Text = string.Empty;


        }




        // Tax code mapping row loaded from CSV (Soll/Haben, Tax code, Description).
        private readonly record struct TaxCodeRule(
            string SollHaben,
            string TaxCode,
            string Description
        );

        // In-memory rules loaded by TaxCodeImportButton.
        private List<TaxCodeRule> _taxCodeRules = new();

        // Optional: expose read-only view for later use.
        private IReadOnlyList<TaxCodeRule> TaxCodeRules => _taxCodeRules;

        private void SetConversionButtonsEnabled(bool enabled)
        {
            DeutscheBankFormatButton.Enabled = enabled;
            HalifaxExportButton.Enabled = enabled;
            RevolutExportButton.Enabled = enabled;
            CreditCardExportButton.Enabled = enabled;

            DeutscheBankFormatButton.Visible = enabled;
            HalifaxExportButton.Visible = enabled;
            RevolutExportButton.Visible = enabled;
            CreditCardExportButton.Visible = enabled;
        }

        private static char DetectDelimiter(string line)
        {
            bool hasSemicolon = line.Contains(';', StringComparison.Ordinal);
            bool hasComma = line.Contains(',', StringComparison.Ordinal);

            if (hasSemicolon && !hasComma) return ';';
            if (hasComma && !hasSemicolon) return ',';
            if (hasSemicolon && hasComma) return ';';

            // Deterministic fallback (common in German exports).
            return ';';
        }

        private static string UnquoteAndTrim(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\"", string.Empty, StringComparison.Ordinal).Trim();
        }

        private static string ResolveTaxCode(
            string beneficiary,
            IReadOnlyList<TaxCodeRule> taxCodeRules,
            string defaultTaxCode)
        {
            if (string.IsNullOrWhiteSpace(defaultTaxCode))
                throw new ArgumentException("Default tax code was empty.", nameof(defaultTaxCode));

            if (string.IsNullOrEmpty(beneficiary))
                return defaultTaxCode;

            // Deterministic: first match wins, rule order defines precedence.
            for (int i = 0; i < taxCodeRules.Count; i++)
            {
                TaxCodeRule rule = taxCodeRules[i];

                if (string.IsNullOrWhiteSpace(rule.Description))
                    continue;

                if (beneficiary.IndexOf(rule.Description, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // If a rule tax-code is empty, fall back deterministically.
                    if (string.IsNullOrWhiteSpace(rule.TaxCode))
                        return defaultTaxCode;

                    return rule.TaxCode;
                }
            }

            return defaultTaxCode;
        }




        private void SetCompiledDateLabel()
        {
            try
            {
                string? exePath = Environment.ProcessPath;

                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                {
                    lblCompiledDate.Text = "© GNAsoftware       gnajag2025@gmail.com";
                    return;
                }

                DateTime buildDate = File.GetLastWriteTime(exePath);
                string formattedDate = buildDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

                lblCompiledDate.Text = $"© GNAsoftware       gnajag2025@gmail.com       {formattedDate}";
            }
            catch
            {
                // Deterministic fallback: never block or throw.
                lblCompiledDate.Text = "GNAsoftware";
            }
        }


        private static string ApplyCreditTaxCodeOverride(OutputRow row, string currentTaxCode)
        {
            if (string.IsNullOrWhiteSpace(row.Credit))
            {
                return currentTaxCode;
            }

            // If the existing tax code is 999, do not override.
            if (string.Equals(currentTaxCode, "999", StringComparison.Ordinal))
            {
                return currentTaxCode;
            }

            string currency = (row.Currency ?? string.Empty).Trim();

            if (string.Equals(currency, "GBP", StringComparison.OrdinalIgnoreCase))
            {
                return "4338-1";
            }

            return "2180";
        }






    }
}
