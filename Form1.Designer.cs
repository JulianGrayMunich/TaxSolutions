#nullable enable
namespace TaxSolutions
{
    partial class Form1
    {
        #region Windows Form Designer generated code
        private System.ComponentModel.IContainer? components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing) components?.Dispose();
            base.Dispose(disposing: disposing);
        }

        private void InitializeComponent()
        {
            MainTabControl = new TabControl();
            CsvFilePathTextBox = new TextBox();
            BrowseCsvButton = new Button();
            DeutscheBankFormatButton = new Button();
            HalifaxExportButton = new Button();
            RevolutExportButton = new Button();
            lblCompiledDate = new Label();
            SuspendLayout();
            AutoScaleDimensions = new SizeF(width: 96F, height: 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font(familyName: "Arial", emSize: 9F, style: FontStyle.Regular);
            ForeColor = Color.Black;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "TaxSolutions";
            Name = "Form1";
            Size = new Size(width: 960, height: 590);
            MainTabControl.Dock = DockStyle.Fill;
            MainTabControl.Name = "MainTabControl";
            lblCompiledDate.Dock = DockStyle.Bottom;
            lblCompiledDate.Height = 24;
            lblCompiledDate.BackColor = Color.LightGray;
            lblCompiledDate.ForeColor = Color.DarkSlateGray;
            lblCompiledDate.TextAlign = ContentAlignment.MiddleLeft;
            lblCompiledDate.Padding = new Padding(left: 8, top: 0, right: 0, bottom: 0);
            lblCompiledDate.Font = new Font(familyName: "Century Gothic", emSize: 6.75F);
            lblCompiledDate.Text = "© GNA Software Revision 012";
            Controls.Add(value: MainTabControl);
            Controls.Add(value: lblCompiledDate);
            BuildInterface();
            Load += ApplyTrackScanWindowDimensions;
            Load += Form1_Load;
            Shown += SelectInputHost;
            ResumeLayout(performLayout: true);
        }
        #endregion

        #region Interface controls
        private TabControl MainTabControl = null!;
        private TextBox CsvFilePathTextBox = null!;
        private Button BrowseCsvButton = null!;
        private Button DeutscheBankFormatButton = null!;
        private Button HalifaxExportButton = null!;
        private Button RevolutExportButton = null!;
        private Label lblCompiledDate = null!;
        #endregion
    }
}


