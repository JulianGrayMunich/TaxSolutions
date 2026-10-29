using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace TaxSolutions
{
    public partial class Form1
    {
        #region Master tab and missing-file decisions
        private TabPage _masterTab = null!;
        private TextBox _masterFolder = null!;
        private TextBox _masterComments = null!;
        private GroupBox _masterSourceCard = null!;
        private GroupBox _masterActionCard = null!;
        private GroupBox _masterCommentsCard = null!;

        private Button _masterBrowse = null!;
        private Button _masterGenerate = null!;
        private Button _masterHelp = null!;
        private Button _submissionNotes = null!;
        private Label _masterFolderLabel = null!;

        private void BuildMasterTab()
        {
            _masterTab = new TabPage { Text = "Master", BackColor = Color.White };
            _masterSourceCard = new GroupBox { Text = "Converted folder" };
            _masterFolderLabel = new Label { Text = "Converted folder:", AutoSize = true };
            _masterFolder = new TextBox { ReadOnly = true };
            _masterBrowse = new Button { Text = "Browse..." };
            _masterBrowse.Click += (sender, e) =>
            {
                using FolderBrowserDialog dialog = new() { Description = "Select the folder for bank exports and combined reports", UseDescriptionForTitle = true, ShowNewFolderButton = true,
                    SelectedPath = Directory.Exists(path: _masterFolder.Text) ? _masterFolder.Text : RetainedBrowseFolder };
                if (dialog.ShowDialog(owner: this) != DialogResult.OK) return;
                try { SelectMasterFolder(folder: dialog.SelectedPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _masterComments.Text = "Folder selected for this session, but the preference could not be saved: " + ex.Message;
                }
            };
            _masterSourceCard.Controls.Add(value: _masterBrowse);
            _masterSourceCard.Controls.Add(value: _masterFolderLabel);
            _masterSourceCard.Controls.Add(value: _masterFolder);

            _masterActionCard = new GroupBox { Text = "Combined reports" };
            _masterGenerate = new Button { Text = "Generate combined CSV files" };
            _masterGenerate.Click += MasterGenerate_Click;
            _masterActionCard.Controls.Add(value: _masterGenerate);
            _masterCommentsCard = new GroupBox { Text = "Comments", Padding = new Padding(all: 8) };
            _masterComments = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Color.White, ScrollBars = ScrollBars.Vertical,
                Text = "Save Configuration, convert each bank, then select their common Converted folder and generate combined files." };
            _masterCommentsCard.Controls.Add(value: _masterComments);
            _masterHelp = new Button { Text = "?", AccessibleName = "Open TaxSolutions help", Font = new Font(familyName: "Arial", emSize: 16F, style: FontStyle.Bold) };
            _masterHelp.Click += ShowInterfaceHelp;
            _submissionNotes = new Button { Text = "Submission Notes", AccessibleName = "Submission Notes", Image = CreateSubmissionNotesImage(), TextImageRelation = TextImageRelation.ImageBeforeText };
            _submissionNotes.Click += (sender, e) => OpenTaxSolutionsHelp(fragment: "excel-submission");
            _submissionNotes.Disposed += (sender, e) => _submissionNotes.Image?.Dispose();
            _masterTab.Controls.Add(value: _submissionNotes);
            _masterTab.Controls.Add(value: _masterSourceCard);
            _masterTab.Controls.Add(value: _masterActionCard);
            _masterTab.Controls.Add(value: _masterCommentsCard);
            _masterTab.Controls.Add(value: _masterHelp);
            MainTabControl.TabPages.Insert(index: 0, tabPage: _masterTab);
            MainTabControl.SelectedTab = _masterTab;
        }

        private static Bitmap CreateSubmissionNotesImage()
        {
            Bitmap image = new(width: 32, height: 32);
            using Graphics graphics = Graphics.FromImage(image: image);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using SolidBrush background = new(color: Color.FromArgb(red: 224, green: 224, blue: 224));
            using Pen outline = new(color: Color.FromArgb(red: 96, green: 96, blue: 96), width: 2F);
            using SolidBrush foreground = new(color: Color.FromArgb(red: 64, green: 64, blue: 64));
            using Font symbol = new(familyName: "Arial", emSize: 22F, style: FontStyle.Bold, unit: GraphicsUnit.Pixel);
            using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.FillEllipse(brush: background, x: 2, y: 2, width: 28, height: 28);
            graphics.DrawEllipse(pen: outline, x: 2, y: 2, width: 28, height: 28);
            graphics.DrawString(s: "i", font: symbol, brush: foreground, layoutRectangle: new RectangleF(x: 2, y: 2, width: 28, height: 28), format: format);
            return image;
        }

        private void LayoutMasterTab()
        {
            int width = _masterTab.ClientSize.Width;
            _masterSourceCard.SetBounds(x: DisplayUnits(value: 20), y: DisplayUnits(value: 70), width: width - DisplayUnits(value: 40), height: DisplayUnits(value: 72));
            _masterFolderLabel.Location = new Point(x: DisplayUnits(value: 16), y: DisplayUnits(value: 31));
            _masterFolder.SetBounds(x: DisplayUnits(value: 156), y: DisplayUnits(value: 25), width: _masterSourceCard.ClientSize.Width - DisplayUnits(value: 336), height: DisplayUnits(value: 26));
            _masterBrowse.SetBounds(x: _masterSourceCard.ClientSize.Width - DisplayUnits(value: 146), y: DisplayUnits(value: 23), width: DisplayUnits(value: 130), height: DisplayUnits(value: 28));
            _masterActionCard.SetBounds(x: DisplayUnits(value: 20), y: DisplayUnits(value: 186), width: width - DisplayUnits(value: 40), height: DisplayUnits(value: 72));
            _masterGenerate.SetBounds(x: (_masterActionCard.ClientSize.Width - DisplayUnits(value: 270)) / 2, y: DisplayUnits(value: 23), width: DisplayUnits(value: 270), height: DisplayUnits(value: 30));
            _masterCommentsCard.SetBounds(x: DisplayUnits(value: 12), y: _masterTab.ClientSize.Height - DisplayUnits(value: 138), width: width - DisplayUnits(value: 24), height: DisplayUnits(value: 126));
            _submissionNotes.SetBounds(x: _masterActionCard.Right - DisplayUnits(value: 180), y: _masterActionCard.Bottom + (_masterCommentsCard.Top - _masterActionCard.Bottom - DisplayUnits(value: 34)) / 2, width: DisplayUnits(value: 180), height: DisplayUnits(value: 34));
            _masterHelp.SetBounds(x: width - DisplayUnits(value: 54), y: DisplayUnits(value: 20), width: DisplayUnits(value: 34), height: DisplayUnits(value: 34));
        }

        private void MasterGenerate_Click(object? sender, EventArgs e)
        {
            try
            {
                RefreshRetainedFolder();
                BankExportResult? result = CombineMasterFiles(folder: _masterFolder.Text, confirmMissing: ConfirmMissingMasterFiles);
                _masterComments.Text = result?.Summary ?? "Combined export cancelled. Existing combined files were not changed.";
                if (result is not null) ShowExportSuccess(summary: result.Summary);
            }
            catch (Exception ex)
            {
                _masterComments.Text = ex.Message;
                MessageBox.Show(owner: this, text: ex.Message, caption: "Combined Reports", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Warning);
            }
        }

        private bool ConfirmMissingMasterFiles(string category, string[] missingFiles)
        {
            using Form dialog = new() { Text = "Missing " + category + " files", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(width: DisplayUnits(value: 720), height: DisplayUnits(value: 270)), Font = Font, MinimizeBox = false, MaximizeBox = false };
            TextBox message = new() { ReadOnly = true, Multiline = true, WordWrap = false, ScrollBars = ScrollBars.Both, BorderStyle = BorderStyle.None, BackColor = SystemColors.Control,
                Text = "Missing " + category + " files for the saved report period:\r\n\r\n" + string.Join(separator: "\r\n", value: missingFiles) + "\r\n\r\nProceed with the available files, or Cancel without changing combined exports.",
                Location = new Point(x: 12, y: 12), Size = new Size(width: dialog.ClientSize.Width - 24, height: dialog.ClientSize.Height - 70), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Button proceed = new() { Text = "Proceed", DialogResult = DialogResult.OK, Size = new Size(width: 110, height: 30), Location = new Point(x: dialog.ClientSize.Width - 244, y: dialog.ClientSize.Height - 44), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = proceed.Size, Location = new Point(x: dialog.ClientSize.Width - 122, y: proceed.Top), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            dialog.Controls.Add(value: message); dialog.Controls.Add(value: proceed); dialog.Controls.Add(value: cancel);
            dialog.AcceptButton = cancel; dialog.CancelButton = cancel;
            return dialog.ShowDialog(owner: this) == DialogResult.OK;
        }
        #endregion

        #region Master source checks and combined publication
        private static BankExportResult? CombineMasterFiles(string folder, Func<string, string[], bool> confirmMissing)
        {
            if (string.IsNullOrWhiteSpace(value: folder) || !Directory.Exists(path: folder))
                throw new InvalidDataException(message: "Select the Converted folder containing the bank exports.");
            folder = Path.GetFullPath(path: folder);
            ReportConfiguration settings = ReadBankReportConfiguration(bankName: "Master");
            DateTime firstDay = new(year: settings.Year, month: settings.FromMonth, day: 1);
            DateTime lastDay = new(year: settings.Year, month: settings.ToMonth, day: DateTime.DaysInMonth(year: settings.Year, month: settings.ToMonth));
            string dates = firstDay.ToString(format: "yyyy-MM-dd", provider: CultureInfo.InvariantCulture) + "_" + lastDay.ToString(format: "yyyy-MM-dd", provider: CultureInfo.InvariantCulture);
            List<(string Path, string Bank, bool Income)> sources = new();
            List<string> omissions = new();
            foreach (bool income in new[] { true, false })
            {
                string category = income ? "Income" : "Expenses";
                List<string> missing = new();
                foreach (string bank in new[] { SourceBankRevolut, SourceBankHalifax, SourceBankVolksbank })
                {
                    string name = bank + "_" + category + "_" + dates + ".csv";
                    string path = Path.Combine(path1: folder, path2: name);
                    if (File.Exists(path: path)) sources.Add(item: (path, bank, income)); else missing.Add(item: name);
                }
                if (missing.Count > 0)
                {
                    if (!confirmMissing(arg1: category, arg2: missing.ToArray())) return null;
                    omissions.AddRange(collection: missing);
                }
            }
            List<BankReportRow> incomeRows = new();
            List<BankReportRow> expenseRows = new();
            foreach (var source in sources)
                ReadMasterSource(path: source.Path, bank: source.Bank, income: source.Income, firstDay: firstDay, lastDay: lastDay, rows: source.Income ? incomeRows : expenseRows);
            incomeRows = DescriptionDateSort(rows: incomeRows, getDate: row => row.Date, getColumn2: row => row.Description);
            expenseRows = DescriptionDateSort(rows: expenseRows, getDate: row => row.Date, getColumn2: row => row.Description);
            List<BankOutputFile> outputs = new();
            foreach (bool income in new[] { true, false })
            {
                string destination = Path.Combine(path1: folder, path2: "Combined_" + (income ? "Income_" : "Expenses_") + dates + ".csv");
                string unique = Guid.NewGuid().ToString(format: "N");
                outputs.Add(item: new BankOutputFile { Destination = destination, Header = income ? IncomeExportHeader : ExpensesExportHeader, Rows = income ? incomeRows : expenseRows,
                    SourceBank = string.Empty, TemporaryPath = destination + "." + unique + ".tmp", BackupPath = destination + "." + unique + ".bak" });
            }
            PublishBankOutputs(outputs: outputs);
            string summary = $"Combined export completed.\r\nReport period: {firstDay:yyyy-MM-dd} to {lastDay:yyyy-MM-dd}.\r\nIncome: {incomeRows.Count}; expenses: {expenseRows.Count}; source files: {sources.Count}.\r\nSave folder: {folder}\r\n";
            foreach (BankOutputFile output in outputs) summary += Path.GetFileName(path: output.Destination) + "\r\n";
            if (omissions.Count > 0) summary += "Missing files omitted:\r\n" + string.Join(separator: "\r\n", value: omissions.ToArray());
            return new BankExportResult(Summary: summary.TrimEnd());
        }

        private static void ReadMasterSource(string path, string bank, bool income, DateTime firstDay, DateTime lastDay, List<BankReportRow> rows)
        {
            using TextFieldParser parser = new(path: path);
            parser.TextFieldType = FieldType.Delimited; parser.SetDelimiters(delimiters: new[] { "," }); parser.HasFieldsEnclosedInQuotes = true; parser.TrimWhiteSpace = false;
            try
            {
                string[] header = parser.ReadFields() ?? Array.Empty<string>();
                string expected = income ? IncomeExportHeader : ExpensesExportHeader;
                if (!header.SequenceEqual(second: expected.Split(separator: ','), comparer: StringComparer.Ordinal))
                    throw new InvalidDataException(message: Path.GetFileName(path: path) + ": expected the current seven-column export header. Reconvert this bank before combining.");
                while (!parser.EndOfData)
                {
                    long line = parser.LineNumber;
                    string[] fields = parser.ReadFields() ?? Array.Empty<string>();
                    if (fields.Length != 7 || fields[1] != string.Empty || fields[6] != bank ||
                        !DateTime.TryParseExact(s: fields[0], format: "yyyy-MM-dd", provider: CultureInfo.InvariantCulture, style: DateTimeStyles.None, result: out DateTime date) || date < firstDay || date > lastDay)
                        throw new InvalidDataException(message: $"{Path.GetFileName(path: path)} line {line}: invalid columns, bank label or report date. No combined files were written.");
                    decimal gbp = ParseMasterValue(text: fields[3], decimals: 2, path: path, line: line);
                    decimal eur = ParseMasterValue(text: fields[4], decimals: 2, path: path, line: line);
                    decimal factor = ParseMasterValue(text: fields[5], decimals: 6, path: path, line: line);
                    if (factor <= 0m) throw new InvalidDataException(message: $"{Path.GetFileName(path: path)} line {line}: conversion factor must be positive.");
                    rows.Add(item: new BankReportRow(Date: date, Description: fields[2], Amount: gbp, EuroAmount: eur, ConversionFactor: factor, SourceBank: fields[6]));
                }
            }
            catch (MalformedLineException ex) { throw new InvalidDataException(message: Path.GetFileName(path: path) + ": malformed CSV. No combined files were written.", innerException: ex); }
        }

        private static decimal ParseMasterValue(string text, int decimals, string path, long line)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(input: text, pattern: @"^[0-9]+\.[0-9]{" + decimals.ToString(provider: CultureInfo.InvariantCulture) + "}$") ||
                !decimal.TryParse(s: text, style: NumberStyles.AllowDecimalPoint, provider: CultureInfo.InvariantCulture, result: out decimal value))
                throw new InvalidDataException(message: $"{Path.GetFileName(path: path)} line {line}: invalid amount or conversion factor. Reconvert this bank before combining.");
            return value;
        }
        #endregion
    }
}






