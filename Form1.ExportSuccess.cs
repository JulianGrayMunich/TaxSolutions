namespace TaxSolutions
{
    public partial class Form1
    {
        #region Readable export success dialog
        private Form CreateExportSuccessDialog(string summary)
        {
            Form dialog = new()
            {
                Text = "Conversion Successful",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimizeBox = false,
                MaximizeBox = false,
                Font = Font,
                ForeColor = ForeColor,
                BackColor = Color.White,
                AutoScaleMode = AutoScaleMode.None
            };
            int longestLine = 0;
            string[] lines = summary.Replace(oldValue: "\r", newValue: string.Empty).Split(separator: '\n');
            foreach (string line in lines)
            {
                Size measured = TextRenderer.MeasureText(text: line, font: dialog.Font, proposedSize: Size.Empty,
                    flags: TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                longestLine = Math.Max(val1: longestLine, val2: measured.Width);
            }
            Rectangle workingArea = Screen.FromControl(control: this).WorkingArea;
            int desiredWidth = Math.Max(val1: DisplayUnits(value: 720), val2: longestLine + DisplayUnits(value: 70));
            int width = Math.Min(val1: desiredWidth, val2: workingArea.Width - DisplayUnits(value: 40));
            int height = Math.Min(val1: Math.Max(val1: DisplayUnits(value: 290), val2: (lines.Length + 2) * dialog.Font.Height + DisplayUnits(value: 80)),
                val2: workingArea.Height - DisplayUnits(value: 40));
            dialog.ClientSize = new Size(width: width, height: height);
            dialog.MinimumSize = new Size(width: Math.Min(val1: DisplayUnits(value: 600), val2: width), height: DisplayUnits(value: 220));
            TextBox details = new()
            {
                Name = "ExportDetails", Text = summary, Multiline = true, ReadOnly = true,
                WordWrap = false, ScrollBars = ScrollBars.Both, BorderStyle = BorderStyle.None,
                BackColor = Color.White, Dock = DockStyle.Fill, TabIndex = 0
            };
            Panel content = new() { Dock = DockStyle.Fill, Padding = new Padding(all: DisplayUnits(value: 18)) };
            content.Controls.Add(value: details);
            Panel footer = new() { Dock = DockStyle.Bottom, Height = DisplayUnits(value: 52) };
            Button close = new() { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(width: DisplayUnits(value: 100), height: DisplayUnits(value: 30)), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            footer.Controls.Add(value: close);
            footer.Resize += (sender, e) => close.Location = new Point(x: footer.ClientSize.Width - close.Width - DisplayUnits(value: 18), y: DisplayUnits(value: 8));
            dialog.Controls.Add(value: content);
            dialog.Controls.Add(value: footer);
            close.Location = new Point(x: footer.ClientSize.Width - close.Width - DisplayUnits(value: 18), y: DisplayUnits(value: 8));
            dialog.Shown += (sender, e) => { details.Select(start: 0, length: 0); close.Focus(); };
            dialog.AcceptButton = close;
            dialog.CancelButton = close;
            return dialog;
        }

        private void ShowExportSuccess(string summary)
        {
            using Form dialog = CreateExportSuccessDialog(summary: summary);
            dialog.ShowDialog(owner: this);
        }
        #endregion
    }
}

