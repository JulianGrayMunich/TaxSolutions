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
            SetConversionButtonsEnabled(true);
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            SetCompiledDateLabel();
            LoadFileSelectionPreferences();
        }


        private void DeutscheBankFormatButton_Click(object? sender, EventArgs e)
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

            // Strip rows 1�8 (1-based) => remove first 8 lines (0..7)
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

            if (!(headerLine.StartsWith(value: "Booking date", comparisonType: StringComparison.OrdinalIgnoreCase) ||
                  headerLine.StartsWith(value: "Buchungstag", comparisonType: StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(message: "Expected Deutsche Bank header row was not found after stripping rows 1�8.");
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

                string beneficiary = cols[3].Trim();
                if (IsExcludedDeutscheBankMerchant(sourceLine: allLines[i], normalizedDescription: beneficiary)) continue;

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

                // Deterministic: rows without a valid booking date cannot participate in date sorting.
                if (bookingDate is null)
                {
                    continue;
                }



                // Skip internal transfer line (deterministic exclusion rule)
                //if (string.Equals(
                //        beneficiary,
                //        "DEUTSCHE KREDITBANK AKTIENGESELLSCH AFT",
                //        StringComparison.OrdinalIgnoreCase))
                //{
                //    continue;
                //}


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

                dataRows.Add(new OutputRow(
                    BookingDate: bookingDate.Value.Date,
                    Invoice: "",
                    Beneficiary: NormalizeBankDescription(description: beneficiary),
                    Credit: credit,
                    Debit: debit,
                    Currency: currency,
                    SortKey: NormalizeBankDescription(description: beneficiary)
                ));

            }

            if (dataRows.Count == 0)
            {
                throw new InvalidOperationException("No transaction rows were parsed after stripping and normalization.");
            }

            // Sort by transaction description, then transaction date across the entire report.
            dataRows = DescriptionDateSort(dataRows, r => r.BookingDate, r => r.SortKey);


            string yyyymm = dataRows.Min(selector: row => row.BookingDate).ToString("yyyyMM", CultureInfo.InvariantCulture);

            WriteOutputCsv(
                inputCsvPath,
                "Dbank",
                yyyymm,
                rows: dataRows, sourceBank: SourceBankDeutscheBank);

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
            return string.Join(",", c1, string.Empty, c4, c17, c16, c18);

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
            string? csvPath = SelectedBankCsvPath(target: CsvFilePathTextBox);

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


        private void HalifaxExportButton_Click(object? sender, EventArgs e)
        {
            string? csvPath = TryGetSelectedCsvPath();
            if (csvPath is null)
            {
                return;
            }

            try
            {
                BankExportResult result = ConvertHalifax(inputCsvPath: csvPath);
                _comments[1].Text = result.Summary;

                ShowExportSuccess(summary: result.Summary);
            }
            catch (InvalidDataException ex)
            {
                _comments[1].Text = ex.Message;
                MessageBox.Show(owner: this, text: ex.Message, caption: "Halifax Conversion", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Warning);
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


        #region Description and date ordering
        private static List<T> DescriptionDateSort<T>(List<T> rows, Func<T, DateTime> getDate, Func<T, string> getColumn2)
        {
            ArgumentNullException.ThrowIfNull(argument: rows);
            ArgumentNullException.ThrowIfNull(argument: getDate);
            ArgumentNullException.ThrowIfNull(argument: getColumn2);
            List<(T Row, int Index)> ordered = new(capacity: rows.Count);
            for (int i = 0; i < rows.Count; i++) ordered.Add(item: (rows[i], i));
            ordered.Sort(comparison: (left, right) =>
            {
                int description = string.Compare(strA: getColumn2(left.Row), strB: getColumn2(right.Row), comparisonType: StringComparison.OrdinalIgnoreCase);
                if (description != 0) return description;
                int date = DateTime.Compare(t1: getDate(left.Row), t2: getDate(right.Row));
                return date != 0 ? date : left.Index.CompareTo(value: right.Index);
            });
            List<T> result = new(capacity: rows.Count);
            foreach ((T Row, int Index) entry in ordered) result.Add(item: entry.Row);
            return result;
        }
        #endregion

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
                if (IsExcludedMerchant(description: description)) continue;

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

                if (desc.Contains("1,95% f�r W�hrungsumrechn", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("1.95% f�r W�hrungsumrechn", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("1.95% für Währungsumrechn", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("1,95% für Währungsumrechn", StringComparison.OrdinalIgnoreCase) ||
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
                    Beneficiary: NormalizeBankDescription(description: desc),
                    Credit: credit,
                    Debit: debit,
                    Currency: r.Currency,
                    SortKey: NormalizeBankDescription(description: desc)
                ));
            }

            if (dataRows.Count == 0)
            {
                throw new InvalidOperationException("No output rows remained after parsing/normalisation.");
            }

            // Collapse "Bank charge" lines per month into a single "Monthly bank charges" line.
            dataRows = CollapseMonthlyBankCharges(dataRows);


            dataRows = DescriptionDateSort(dataRows, x => x.BookingDate, x => x.SortKey);

            string yyyymm = dataRows.Min(selector: row => row.BookingDate).ToString("yyyyMM", CultureInfo.InvariantCulture);

            WriteOutputCsv(
                inputCsvPath,
                "CreditCard",
                yyyymm,
                rows: dataRows, sourceBank: "Credit Card");
        }





        // Shared destination header for the non-Halifax converters.
        private const string ConvertedFolderName = "Converted";
        private const string OutputHeader =
            "Booking date,Invoice,Beneficiary / Originator,Credit,Debit,Currency,Source Bank";

        // Shared destination row model for the non-Halifax converters.
        private readonly record struct OutputRow(
            DateTime BookingDate,
            string Invoice,
            string Beneficiary,
            string Credit,
            string Debit,
            string Currency,
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
                row.Currency
            );
        }

        private static void WriteOutputCsv(
            string inputCsvPath,
            string filePrefix,
            string yyyymm,
            IEnumerable<OutputRow> rows, string sourceBank)
        {
            if (string.IsNullOrWhiteSpace(inputCsvPath))
                throw new ArgumentException("Input path was empty.", nameof(inputCsvPath));

            if (string.IsNullOrWhiteSpace(filePrefix))
                throw new ArgumentException("File prefix was empty.", nameof(filePrefix));

            if (string.IsNullOrWhiteSpace(yyyymm))
                throw new ArgumentException("YYYYMM was empty.", nameof(yyyymm));

            if (rows is null)
                throw new ArgumentNullException(nameof(rows));


            string inputFolder = Path.GetDirectoryName(inputCsvPath)
                ?? throw new InvalidOperationException("Unable to determine input folder.");

            inputFolder = Path.Combine(path1: inputFolder, path2: ConvertedFolderName);
            Directory.CreateDirectory(path: inputFolder);
            string outputPath = Path.Combine(inputFolder, $"{filePrefix}_{yyyymm}.csv");

            using StreamWriter writer = new StreamWriter(outputPath, false);
            writer.WriteLine(OutputHeader);
            foreach (var r in rows)
            {
                writer.WriteLine(value: ToOutputCsvLine(r: r, sourceBank: sourceBank));
            }
        }


        private static string ToOutputCsvLine(OutputRow r, string sourceBank)
        {
            // Deterministic CSV line generation (consistent ordering with OUTPUT_HEADER).
            return string.Join(",",
                r.BookingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.Invoice ?? string.Empty,
                NormalizeBankDescription(description: r.Beneficiary ?? string.Empty),
                r.Credit ?? string.Empty,
                r.Debit ?? string.Empty,
                r.Currency ?? string.Empty,
                sourceBank);
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

        private void SetConversionButtonsEnabled(bool enabled)
        {
            DeutscheBankFormatButton.Enabled = enabled;
            HalifaxExportButton.Enabled = enabled;
            RevolutExportButton.Enabled = enabled;

            DeutscheBankFormatButton.Visible = enabled;
            HalifaxExportButton.Visible = enabled;
            RevolutExportButton.Visible = enabled;
            RefreshConversionAvailability();
        }

        #region Application footer
        private const string ApplicationRevision = "021";

        private void SetCompiledDateLabel()
        {
            string assemblyLocation = typeof(Form1).Assembly.Location;
            DateTime buildDate = string.IsNullOrWhiteSpace(value: assemblyLocation)
                ? DateTime.Today
                : File.GetLastWriteTime(path: assemblyLocation).Date;
            lblCompiledDate.Text = $"© GNA Software {buildDate:yyyyMMdd} Revision {ApplicationRevision}";
        }
        #endregion

    }
}









