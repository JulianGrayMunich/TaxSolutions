namespace TaxSolutions
{
    public partial class Form1
    {
        #region Revolut currency-specific selectors and source validation
        private const string RevolutSourceHeader = "Type,Product,Started Date,Completed Date,Description,Amount,Fee,Currency,State,Balance";
        private TextBox _revolutEurPath = null!;
        private TextBox _revolutGbpPath = null!;
        private readonly List<(Label Label, TextBox Path, Button Browse, int Y)> _revolutSelectorRows = new();

        private void BuildRevolutSelectors()
        {
            GroupBox host = _inputHosts[0];
            host.Text = "Revolut Bank Statements";
            _revolutEurPath = AddRevolutSelector(host: host, currency: "EUR", y: 24);
            _revolutGbpPath = AddRevolutSelector(host: host, currency: "GBP", y: 72);
        }

        private TextBox AddRevolutSelector(GroupBox host, string currency, int y)
        {
            Label label = new() { Text = currency + " CSV", AutoSize = true, Location = new Point(x: 16, y: y + 5) };
            TextBox path = new() { ReadOnly = true, Location = new Point(x: 156, y: y), Size = new Size(width: 560, height: 26), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Button browse = new() { Text = "Browse...", Location = new Point(x: 732, y: y - 2), Size = new Size(width: 130, height: 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            browse.Click += (sender, e) => BrowseRevolutFile(currency: currency, target: path);
            host.Controls.Add(value: label);
            host.Controls.Add(value: path);
            host.Controls.Add(value: browse);
            _revolutSelectorRows.Add(item: (label, path, browse, y));
            return path;
        }

        private void LayoutRevolutSelectors()
        {
            GroupBox host = _inputHosts[0];
            foreach ((Label Label, TextBox Path, Button Browse, int Y) row in _revolutSelectorRows)
            {
                row.Label.Location = new Point(x: DisplayUnits(value: 16), y: DisplayUnits(value: row.Y + 5));
                row.Path.SetBounds(x: DisplayUnits(value: 156), y: DisplayUnits(value: row.Y), width: host.ClientSize.Width - DisplayUnits(value: 336), height: DisplayUnits(value: 26));
                row.Browse.SetBounds(x: host.ClientSize.Width - DisplayUnits(value: 146), y: DisplayUnits(value: row.Y - 2), width: DisplayUnits(value: 130), height: DisplayUnits(value: 28));
            }
        }

        private static void ValidateRevolutSource(string path, string expectedCurrency)
        {
            using StreamReader reader = new(path: path);
            string header = reader.ReadLine() ?? string.Empty;
            if (!string.Equals(a: header.TrimStart(trimChar: '\uFEFF'), b: RevolutSourceHeader, comparisonType: StringComparison.Ordinal))
                throw new InvalidDataException(message: InvalidRevolutFileMessage);
            string secondLine = reader.ReadLine() ?? string.Empty;
            if (!secondLine.Contains(value: "," + expectedCurrency + ",", comparisonType: StringComparison.Ordinal))
                throw new InvalidDataException(message: InvalidRevolutFileMessage);
        }

        private void BrowseRevolutFile(string currency, TextBox target)
        {
            using OpenFileDialog dialog = CreateCsvFileDialog();
            dialog.Title = "Select Revolut " + currency + " CSV";
            if (dialog.ShowDialog(owner: this) != DialogResult.OK) return;
            try
            {
                ValidateRevolutSource(path: dialog.FileName, expectedCurrency: currency);
                SetBankCsvSelection(target: target, selectedPath: dialog.FileName, bankIndex: 0);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                MessageBox.Show(owner: this, text: ex.Message, caption: "Revolut File Selection", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Warning);
            }
        }
        #endregion
    }
}

