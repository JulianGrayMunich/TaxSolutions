namespace TaxSolutions
{
    public partial class Form1
    {
        #region Volksbank statement selectors
        private TextBox _volksbankGiroPath = null!;
        private TextBox _volksbankCardPath = null!;
        private CheckBox _volksbankGermanFormat = null!;
        private readonly List<(Label Label, TextBox Path, Button Browse, int Y)> _volksbankSelectorRows = new();

        private void BuildVolksbankSelectors()
        {
            GroupBox host = _inputHosts[2];
            host.Text = "Volksbank Bank Statements";
            _volksbankGiroPath = AddVolksbankSelector(host: host, caption: "Giro CSV", creditCard: false, y: 24);
            _volksbankCardPath = AddVolksbankSelector(host: host, caption: "Credit Card CSV", creditCard: true, y: 72);
            _volksbankGermanFormat = new CheckBox { Text = "German format", Checked = true, AutoSize = true };
            _volksbankGermanFormat.CheckedChanged += (sender, e) =>
            {
                ClearBankCsvSelection(target: _volksbankGiroPath);
                ClearBankCsvSelection(target: _volksbankCardPath);
                _comments[2].Text = "Input format changed. Select both CSV files again.";
            };
            _bankPages[2].Controls.Add(value: _volksbankGermanFormat);
        }

        private TextBox AddVolksbankSelector(GroupBox host, string caption, bool creditCard, int y)
        {
            Label label = new() { Text = caption, AutoSize = true };
            TextBox path = new() { ReadOnly = true };
            Button browse = new() { Text = "Browse..." };
            browse.Click += (sender, e) => BrowseVolksbankFile(creditCard: creditCard, target: path);
            host.Controls.Add(value: label);
            host.Controls.Add(value: path);
            host.Controls.Add(value: browse);
            _volksbankSelectorRows.Add(item: (label, path, browse, y));
            return path;
        }

        private void LayoutVolksbankSelectors()
        {
            GroupBox host = _inputHosts[2];
            foreach (var row in _volksbankSelectorRows)
            {
                row.Label.Location = new Point(x: DisplayUnits(value: 16), y: DisplayUnits(value: row.Y + 5));
                row.Path.SetBounds(x: DisplayUnits(value: 156), y: DisplayUnits(value: row.Y), width: host.ClientSize.Width - DisplayUnits(value: 336), height: DisplayUnits(value: 26));
                row.Browse.SetBounds(x: host.ClientSize.Width - DisplayUnits(value: 146), y: DisplayUnits(value: row.Y - 2), width: DisplayUnits(value: 130), height: DisplayUnits(value: 28));
            }
            _volksbankGermanFormat.Location = new Point(x: DisplayUnits(value: 36), y: DisplayUnits(value: 302));
        }

        private void BrowseVolksbankFile(bool creditCard, TextBox target)
        {
            using OpenFileDialog dialog = CreateCsvFileDialog();
            dialog.Title = creditCard ? "Select Volksbank Credit Card CSV" : "Select Volksbank Giro CSV";
            if (dialog.ShowDialog(owner: this) != DialogResult.OK) return;
            try
            {
                ReadVolksbankSource(path: dialog.FileName, creditCard: creditCard, germanFormat: _volksbankGermanFormat.Checked);
                SetBankCsvSelection(target: target, selectedPath: dialog.FileName, bankIndex: 2);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                MessageBox.Show(owner: this, text: ex.Message, caption: "Volksbank File Selection", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Warning);
            }
        }
        #endregion
    }
}


