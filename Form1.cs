using System.Text;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace TaxSolutions
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            SetCompiledDateLabel();


        }

        private void Form1_Load(object sender, EventArgs e)
        {

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
                    "Deutsche Bank conversion completed.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Conversion Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
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





            string[] headerColumns = SplitCsvLine(headerLine);

            // We will project the required output header from the header row as well.
            string outputHeader = ProjectHeaderColumns(headerColumns);

            List<DbankRow> dataRows = new List<DbankRow>();

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

                string bookingDateRaw = cols[0].Trim();
                string beneficiary = cols[3].Trim();

                DateTime? bookingDate = TryParseDbankDate(bookingDateRaw);

                string projected = ProjectColumns(cols);
                string beneficiarySortKey = MakeColumn2SortKey(beneficiary);

                dataRows.Add(new DbankRow(
                    OutputColumn1RawDate: bookingDateRaw,
                    OutputColumn1Date: bookingDate,
                    OutputColumn2Text: beneficiary,
                    OutputColumn2SortKey: beneficiarySortKey,
                    ProjectedLine: projected));



            }

            if (dataRows.Count == 0)
            {
                throw new InvalidOperationException("No transaction rows were parsed after stripping and normalization.");
            }

            // Sort by column 4 (string), then by column 1 (date if parseable, else raw string)
            dataRows.Sort(CompareDbankRows);

            //=========================


            // Diagnostics: emit the first 30 sorted keys so we can see what is actually being compared.
            string inputFolderForDiag = Path.GetDirectoryName(inputCsvPath) ?? "";
            string diagPath = Path.Combine(inputFolderForDiag, "Dbank_sort_diagnostics.txt");

            using (StreamWriter diag = new StreamWriter(diagPath, false))
            {
                diag.WriteLine("Sorted OutputColumn2SortKey (first 30 rows)");
                diag.WriteLine("------------------------------------------------------------");

                int limit = Math.Min(30, dataRows.Count);

                for (int i = 0; i < limit; i++)
                {
                    diag.WriteLine($"{i + 1:00}: {DescribeLeadingChars(dataRows[i].OutputColumn2SortKey)}");
                }

                diag.WriteLine();
                diag.WriteLine("Compare check:");
                string a = "1+1 Telecom GmbH";
                string b = "Deutsche Post AG";
                diag.WriteLine($"OrdinalIgnoreCase '{a}' vs '{b}' => {string.Compare(a, b, StringComparison.OrdinalIgnoreCase)}");
            }





            //=========================


            // Determine YYYYMM from column 1, row 1 (first data row after sorting)
            DateTime? firstDate = dataRows[0].OutputColumn1Date;

            if (firstDate is null)
            {
                firstDate = TryParseDbankDate(dataRows[0].OutputColumn1RawDate);
            }

            if (firstDate is null)
            {
                throw new InvalidOperationException(
                    $"Unable to determine YYYYMM because the first sorted row booking date could not be parsed: '{dataRows[0].OutputColumn1RawDate}'.");
            }






            string yyyymm = firstDate.Value.ToString("yyyyMM", CultureInfo.InvariantCulture);

            string inputFolder = Path.GetDirectoryName(inputCsvPath)
                ?? throw new InvalidOperationException("Unable to determine input folder.");

            string outputPath = Path.Combine(inputFolder, $"Dbank_{yyyymm}.csv");

            using StreamWriter writer = new StreamWriter(outputPath, false);

            writer.WriteLine(outputHeader);

            for (int i = 0; i < dataRows.Count; i++)
            {
                writer.WriteLine(dataRows[i].ProjectedLine);
            }
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

            return string.Join(",", c1, c4, c16, c17, c18);
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

        private static int CompareDbankRows(DbankRow x, DbankRow y)
        {
            // Sort by Output Column 2 (original column 4)
            int byCol2 = string.Compare(
                x.OutputColumn2SortKey,
                y.OutputColumn2SortKey,
                StringComparison.OrdinalIgnoreCase);


            if (byCol2 != 0)
            {
                return byCol2;
            }

            // Then by Output Column 1 (date)
            if (x.OutputColumn1Date.HasValue && y.OutputColumn1Date.HasValue)
            {
                return DateTime.Compare(
                    x.OutputColumn1Date.Value,
                    y.OutputColumn1Date.Value);
            }

            // Deterministic fallback if parsing failed
            return string.Compare(
                x.OutputColumn1RawDate,
                y.OutputColumn1RawDate,
                StringComparison.OrdinalIgnoreCase);
        }


        private readonly record struct DbankRow(
            string OutputColumn1RawDate,
            DateTime? OutputColumn1Date,
            string OutputColumn2Text,
            string OutputColumn2SortKey,
            string ProjectedLine);






        private string? TryGetSelectedCsvPath()
        {
            string? csvPath = CsvFilePathTextBox.Text;

            if (string.IsNullOrWhiteSpace(csvPath))
            {
                MessageBox.Show(
                    "Select a CSV file first.",
                    "Missing Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }

            if (!File.Exists(csvPath))
            {
                MessageBox.Show(
                    "The selected file does not exist.\nReselect the CSV file.",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return null;
            }

            return csvPath;
        }

        private void HalifaxExportButton_Click(object sender, EventArgs e)
        {
            string? csvPath = TryGetSelectedCsvPath();
            if (csvPath is null)
            {
                return;
            }

            MessageBox.Show(
                "Halifax export is not yet implemented.",
                "Not Implemented",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void RevolutExportButton_Click(object sender, EventArgs e)
        {
            string? csvPath = TryGetSelectedCsvPath();
            if (csvPath is null)
            {
                return;
            }

            MessageBox.Show(
                "Revolut export is not yet implemented.",
                "Not Implemented",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void SetCompiledDateLabel()
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            DateTime buildDate = File.GetLastWriteTime(assemblyPath);

            string formattedDate = buildDate.ToString("yyyyMMdd");

            lblCompiledDate.Text = $"GNAsoftware {formattedDate}";
        }


        private static string SanitizeSortKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // Normalise and remove control/format characters (e.g. BOM, zero-width chars).
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

            return sb.ToString().Trim();
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

        private static readonly char[] UnicodeWhitespaceChars =
        {
    ' ', '\t', '\r', '\n',
    '\u00A0', // NO-BREAK SPACE
    '\u1680', // OGHAM SPACE MARK
    '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A', // EN/EM etc
    '\u202F', // NARROW NO-BREAK SPACE
    '\u205F', // MEDIUM MATHEMATICAL SPACE
    '\u3000'  // IDEOGRAPHIC SPACE
};


        private static string DescribeLeadingChars(string value, int count = 8)
        {
            if (value is null)
            {
                return "<null>";
            }

            int take = Math.Min(count, value.Length);

            StringBuilder sb = new StringBuilder();

            sb.Append('"').Append(value).Append('"');
            sb.Append("  [");

            for (int i = 0; i < take; i++)
            {
                char c = value[i];
                sb.Append("U+").Append(((int)c).ToString("X4"));
                sb.Append('/');
                sb.Append(char.GetUnicodeCategory(c));
                if (i < take - 1) sb.Append(' ');
            }

            sb.Append(']');

            return sb.ToString();
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

            return string.Join(",", h1, h4, h16, h17, h18);
        }





    }
}
