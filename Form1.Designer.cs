namespace TaxSolutions
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            MainTabControl = new TabControl();
            ConversionTab = new TabPage();
            lblCompiledDate = new Label();
            ConversionButtonsPanel = new FlowLayoutPanel();
            DeutscheBankFormatButton = new Button();
            HalifaxExportButton = new Button();
            RevolutExportButton = new Button();
            CreditCardExportButton = new Button();
            TaxCodeImportButton = new Button();
            BrowseCsvButton = new Button();
            CsvFilePathTextBox = new TextBox();
            MainTabControl.SuspendLayout();
            ConversionTab.SuspendLayout();
            ConversionButtonsPanel.SuspendLayout();
            SuspendLayout();
            // 
            // MainTabControl
            // 
            MainTabControl.Controls.Add(ConversionTab);
            MainTabControl.Dock = DockStyle.Fill;
            MainTabControl.Location = new Point(0, 0);
            MainTabControl.Name = "MainTabControl";
            MainTabControl.SelectedIndex = 0;
            MainTabControl.Size = new Size(800, 450);
            MainTabControl.TabIndex = 0;
            // 
            // ConversionTab
            // 
            ConversionTab.BackColor = Color.LightSlateGray;
            ConversionTab.Controls.Add(lblCompiledDate);
            ConversionTab.Controls.Add(ConversionButtonsPanel);
            ConversionTab.Controls.Add(BrowseCsvButton);
            ConversionTab.Controls.Add(CsvFilePathTextBox);
            ConversionTab.Location = new Point(4, 29);
            ConversionTab.Name = "ConversionTab";
            ConversionTab.Padding = new Padding(3);
            ConversionTab.Size = new Size(792, 417);
            ConversionTab.TabIndex = 0;
            ConversionTab.Text = "ConversionTab";
            // 
            // lblCompiledDate
            // 
            lblCompiledDate.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblCompiledDate.AutoSize = true;
            lblCompiledDate.Location = new Point(106, 371);
            lblCompiledDate.Name = "lblCompiledDate";
            lblCompiledDate.Size = new Size(97, 20);
            lblCompiledDate.TabIndex = 4;
            lblCompiledDate.Text = "GNAsoftware";
            // 
            // ConversionButtonsPanel
            // 
            ConversionButtonsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            ConversionButtonsPanel.AutoScroll = true;
            ConversionButtonsPanel.Controls.Add(DeutscheBankFormatButton);
            ConversionButtonsPanel.Controls.Add(HalifaxExportButton);
            ConversionButtonsPanel.Controls.Add(RevolutExportButton);
            ConversionButtonsPanel.Controls.Add(CreditCardExportButton);
            ConversionButtonsPanel.Controls.Add(TaxCodeImportButton);
            ConversionButtonsPanel.FlowDirection = FlowDirection.TopDown;
            ConversionButtonsPanel.Location = new Point(102, 80);
            ConversionButtonsPanel.Name = "ConversionButtonsPanel";
            ConversionButtonsPanel.Padding = new Padding(8);
            ConversionButtonsPanel.Size = new Size(657, 275);
            ConversionButtonsPanel.TabIndex = 3;
            ConversionButtonsPanel.WrapContents = false;
            // 
            // DeutscheBankFormatButton
            // 
            DeutscheBankFormatButton.Enabled = false;
            DeutscheBankFormatButton.Location = new Point(11, 11);
            DeutscheBankFormatButton.Name = "DeutscheBankFormatButton";
            DeutscheBankFormatButton.Size = new Size(180, 30);
            DeutscheBankFormatButton.TabIndex = 3;
            DeutscheBankFormatButton.Text = "Deutsche Bank";
            DeutscheBankFormatButton.UseVisualStyleBackColor = true;
            DeutscheBankFormatButton.Visible = false;
            DeutscheBankFormatButton.Click += DeutscheBankFormatButton_Click;
            // 
            // HalifaxExportButton
            // 
            HalifaxExportButton.Enabled = false;
            HalifaxExportButton.Location = new Point(11, 47);
            HalifaxExportButton.Name = "HalifaxExportButton";
            HalifaxExportButton.Size = new Size(180, 30);
            HalifaxExportButton.TabIndex = 4;
            HalifaxExportButton.Text = "Halifax";
            HalifaxExportButton.UseVisualStyleBackColor = true;
            HalifaxExportButton.Visible = false;
            HalifaxExportButton.Click += HalifaxExportButton_Click;
            // 
            // RevolutExportButton
            // 
            RevolutExportButton.Enabled = false;
            RevolutExportButton.Location = new Point(11, 83);
            RevolutExportButton.Name = "RevolutExportButton";
            RevolutExportButton.Size = new Size(180, 30);
            RevolutExportButton.TabIndex = 5;
            RevolutExportButton.Text = "Revolut";
            RevolutExportButton.UseVisualStyleBackColor = true;
            RevolutExportButton.Visible = false;
            RevolutExportButton.Click += RevolutExportButton_Click;
            // 
            // CreditCardExportButton
            // 
            CreditCardExportButton.Enabled = false;
            CreditCardExportButton.Location = new Point(11, 119);
            CreditCardExportButton.Name = "CreditCardExportButton";
            CreditCardExportButton.Size = new Size(180, 30);
            CreditCardExportButton.TabIndex = 6;
            CreditCardExportButton.Text = "Credit Card";
            CreditCardExportButton.UseVisualStyleBackColor = true;
            CreditCardExportButton.Visible = false;
            CreditCardExportButton.Click += CreditCardExportButton_Click;
            // 
            // TaxCodeImportButton
            // 
            TaxCodeImportButton.Location = new Point(11, 155);
            TaxCodeImportButton.Name = "TaxCodeImportButton";
            TaxCodeImportButton.Size = new Size(180, 30);
            TaxCodeImportButton.TabIndex = 7;
            TaxCodeImportButton.Text = "Import Tax Codes";
            TaxCodeImportButton.UseVisualStyleBackColor = true;
            TaxCodeImportButton.Click += TaxCodeImportButton_Click;
            // 
            // BrowseCsvButton
            // 
            BrowseCsvButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            BrowseCsvButton.Location = new Point(616, 20);
            BrowseCsvButton.Name = "BrowseCsvButton";
            BrowseCsvButton.Size = new Size(94, 29);
            BrowseCsvButton.TabIndex = 1;
            BrowseCsvButton.Text = "Browse...";
            BrowseCsvButton.UseVisualStyleBackColor = true;
            BrowseCsvButton.Click += BrowseCsvButton_Click;
            // 
            // CsvFilePathTextBox
            // 
            CsvFilePathTextBox.Location = new Point(103, 22);
            CsvFilePathTextBox.Name = "CsvFilePathTextBox";
            CsvFilePathTextBox.ReadOnly = true;
            CsvFilePathTextBox.Size = new Size(442, 27);
            CsvFilePathTextBox.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(MainTabControl);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Bank statement CSV file converter";
            Load += Form1_Load;
            MainTabControl.ResumeLayout(false);
            ConversionTab.ResumeLayout(false);
            ConversionTab.PerformLayout();
            ConversionButtonsPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TabControl MainTabControl;
        private TabPage ConversionTab;
        private TextBox CsvFilePathTextBox;
        private Button BrowseCsvButton;
        private FlowLayoutPanel ConversionButtonsPanel;
        private Button DeutscheBankFormatButton;
        private Button HalifaxExportButton;
        private Button RevolutExportButton;
        private Label lblCompiledDate;
        private Button CreditCardExportButton;
        private Button TaxCodeImportButton;
    }
}
