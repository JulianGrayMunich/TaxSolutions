namespace TaxSolutions
{
    public partial class Form1
    {
        #region Revision 021 tabbed interface
        private readonly List<TabPage> _bankPages = new();
        private readonly List<GroupBox> _inputHosts = new();
        private readonly List<TextBox> _comments = new();
        private Panel _sharedInput = null!;
        private int _lastActiveTabIndex = -1;

        private void BuildInterface()
        {
            _sharedInput = new Panel { Size = new Size(width: 892, height: 74), Dock = DockStyle.Fill };
            Label pathLabel = new() { Text = "CSV file:", Location = new Point(x: 16, y: 31), AutoSize = true };
            CsvFilePathTextBox.SetBounds(x: 156, y: 25, width: 560, height: 26);
            CsvFilePathTextBox.ReadOnly = true;
            CsvFilePathTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            BrowseCsvButton.Text = "Browse...";
            BrowseCsvButton.SetBounds(x: 732, y: 23, width: 130, height: 28);
            BrowseCsvButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            BrowseCsvButton.Click += BrowseCsvButton_Click;
            _sharedInput.Controls.Add(value: pathLabel);
            _sharedInput.Controls.Add(value: CsvFilePathTextBox);
            _sharedInput.Controls.Add(value: BrowseCsvButton);

            AddApplicationTab(title: "Revolut", action: RevolutExportButton, actionText: "Convert Revolut", handler: RevolutExportButton_Click);
            AddApplicationTab(title: "Halifax", action: HalifaxExportButton, actionText: "Convert Halifax", handler: HalifaxExportButton_Click);
            AddApplicationTab(title: "Volksbank", action: new Button(), actionText: "Convert Volksbank", handler: VolksbankExportButton_Click);
            AddApplicationTab(title: "Deutschebank", action: DeutscheBankFormatButton, actionText: "Convert Deutschebank", handler: DeutscheBankFormatButton_Click);
            BuildExcludedMerchantsTab();
            BuildConfigurationTab();
            BuildRevolutSelectors();
            BuildVolksbankSelectors();
            MainTabControl.TabPages.Remove(value: _bankPages[3]);
            BuildMasterTab();
            InitializeConversionAvailability();
            MainTabControl.SelectedIndexChanged += SelectInputHost;
            SelectInputHost(sender: this, e: EventArgs.Empty);
            RefreshInterfaceComments(sender: this, e: EventArgs.Empty);
        }

        private void AddApplicationTab(string title, Button? action, string actionText, EventHandler? handler)
        {
            TabPage page = new() { Text = title, BackColor = Color.White, Padding = new Padding(all: 12) };
            MainTabControl.TabPages.Add(value: page);
            _bankPages.Add(item: page);
            Button help = new() { Text = "?", AccessibleName = "Open TaxSolutions help", Font = new Font(familyName: "Arial", emSize: 16F, style: FontStyle.Bold), Size = new Size(width: 34, height: 34), Location = new Point(x: 878, y: 20), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            help.Click += ShowInterfaceHelp;
            page.Controls.Add(value: help);
            GroupBox input = new() { Text = "Bank Statement", Location = new Point(x: 20, y: 70), Size = new Size(width: 896, height: 94), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            page.Controls.Add(value: input);
            _inputHosts.Add(item: input);
            GroupBox actions = new() { Name = "ActionsGroup", Text = title, Location = new Point(x: 20, y: 186), Size = new Size(width: 896, height: 72), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            page.Controls.Add(value: actions);
            Label caption = new() { Text = "Statement conversion:", Location = new Point(x: 16, y: 30), AutoSize = true };
            actions.Controls.Add(value: caption);
            if (action is not null)
            {
                action.Text = actionText;
                action.SetBounds(x: 196, y: 23, width: 210, height: 30);
                if (handler is not null) action.Click += handler;
                actions.Controls.Add(value: action);
            }
            else
            {
                Label pending = new() { Text = "Volksbank conversion will be configured in a subsequent revision.", AutoSize = true, Location = new Point(x: 196, y: 30) };
                actions.Controls.Add(value: pending);
            }
            GroupBox comments = new() { Text = "Comments", Location = new Point(x: 12, y: 362), Size = new Size(width: 912, height: 126), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, Padding = new Padding(all: 8) };
            TextBox text = new() { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
            comments.Controls.Add(value: text);
            page.Controls.Add(value: comments);
            _comments.Add(item: text);
        }

        // TrackScan's WPF dimensions are device-independent units (96 units per inch).
        private int DisplayUnits(int value) => (int)Math.Round(a: value * DeviceDpi / 96.0);

        private void ApplyTrackScanWindowDimensions(object? sender, EventArgs e)
        {
            Size = new Size(width: DisplayUnits(value: 960), height: DisplayUnits(value: 590));
            lblCompiledDate.Height = DisplayUnits(value: 24);
            SelectInputHost(sender: sender, e: e);
        }

        private void SelectInputHost(object? sender, EventArgs e)
        {
            int selected = MainTabControl.SelectedIndex;
            if (selected < 0) return;
            if (_lastActiveTabIndex != selected)
            {
                ClearBankCsvSelection(target: CsvFilePathTextBox);
                ClearBankCsvSelection(target: _revolutEurPath);
                ClearBankCsvSelection(target: _revolutGbpPath);
                ClearBankCsvSelection(target: _volksbankGiroPath);
                ClearBankCsvSelection(target: _volksbankCardPath);
                _lastActiveTabIndex = selected;
            }
            if (MainTabControl.SelectedTab == _excludedMerchantsTab)
            {
                LayoutExcludedMerchantsTab();
                return;
            }
            if (MainTabControl.SelectedTab == _configurationTab)
            {
                LayoutConfigurationTab();
                return;
            }
            if (MainTabControl.SelectedTab == _masterTab) { RefreshRetainedFolder(); LayoutMasterTab(); return; }
            TabPage page = MainTabControl.SelectedTab!;
            selected = _bankPages.IndexOf(item: page);
            if (selected < 0) return;
            _inputHosts[selected].SetBounds(x: DisplayUnits(value: 20), y: DisplayUnits(value: 70), width: page.ClientSize.Width - DisplayUnits(value: 40), height: DisplayUnits(value: 94));
            page.Controls["ActionsGroup"]!.SetBounds(x: DisplayUnits(value: 20), y: DisplayUnits(value: 186), width: page.ClientSize.Width - DisplayUnits(value: 40), height: DisplayUnits(value: 72));
            _comments[selected].Parent!.SetBounds(x: DisplayUnits(value: 12), y: page.ClientSize.Height - DisplayUnits(value: 138), width: page.ClientSize.Width - DisplayUnits(value: 24), height: DisplayUnits(value: 126));
            page.Controls[0].Location = new Point(x: page.ClientSize.Width - DisplayUnits(value: 54), y: DisplayUnits(value: 20));
            if (selected == 0)
            {
                _inputHosts[0].Height = DisplayUnits(value: 126);
                LayoutRevolutSelectors();
                page.Controls["ActionsGroup"]!.Top = DisplayUnits(value: 220);
            }
            else if (selected == 2)
            {
                _inputHosts[2].Height = DisplayUnits(value: 126);
                LayoutVolksbankSelectors();
                page.Controls["ActionsGroup"]!.Top = DisplayUnits(value: 220);
            }
            else
            {
                _inputHosts[selected].Controls.Add(value: _sharedInput);
            }
            _sharedInput.Visible = selected != 0 && selected != 2;
        }

        private void RefreshInterfaceComments(object? sender, EventArgs e)
        {
            for (int i = 0; i < _comments.Count; i++)
                _comments[i].Text = "Select a statement CSV file and use the conversion button for its bank.";
            _comments[2].Text = "Select Giro CSV and Credit Card CSV, confirm German format, then convert.";
        }


        #endregion
    }
}













