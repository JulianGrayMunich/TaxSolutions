using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaxSolutions
{
    public partial class Form1
    {
        #region Report configuration model and storage
        private const string ConfigurationFileName = "TaxSolutions.settings.json";
        private const string RateDefinition = "1 GBP = X EUR";
        private const int MaximumReportMonths = 3;
        private const int RateDecimalPlaces = 6;
        private const string RateDisplayFormat = "F6";
        private const int CurrencyPairCount = 3;
        private readonly string[] _currencyPairs = { "GBP to EUR", "GBP to ZAR", "EUR to ZAR" };
        private const int DefaultReportYear = 2026;
        private sealed class ReportConfiguration
        {
            public int Year { get; set; } = DefaultReportYear;
            public int FromMonth { get; set; } = 1;
            public int ToMonth { get; set; } = 3;
            public string ExchangeRateDefinition { get; set; } = RateDefinition;
            public Dictionary<string, decimal> MonthlyGbpToEurRates { get; set; } = new();
            public Dictionary<string, decimal> MonthlyGbpToZarRates { get; set; } = new();
            public Dictionary<string, decimal> MonthlyEurToZarRates { get; set; } = new();
        }

        private readonly Dictionary<string, decimal>[] _monthlyRates =
        {
            new(comparer: StringComparer.Ordinal), new(comparer: StringComparer.Ordinal), new(comparer: StringComparer.Ordinal)
        };
        private NumericUpDown _reportYear = null!;
        private ComboBox _fromMonth = null!;
        private ComboBox _toMonth = null!;
        private readonly Label[,] _rateMonths = new Label[CurrencyPairCount, MaximumReportMonths];
        private readonly TextBox[,] _rateValues = new TextBox[CurrencyPairCount, MaximumReportMonths];
        private TabPage _configurationTab = null!;
        private GroupBox _periodCard = null!;
        private readonly GroupBox[] _ratesCards = new GroupBox[CurrencyPairCount];
        private Button _configurationHelp = null!;
        private GroupBox _configurationCommentsCard = null!;
        private TextBox _configurationComments = null!;
        private Button _saveConfiguration = null!;
        private bool _loadingConfiguration;

        private static string ConfigurationPath => Path.Combine(
            path1: Path.GetDirectoryName(path: typeof(Form1).Assembly.Location) ?? AppContext.BaseDirectory,
            path2: ConfigurationFileName);
        #endregion

        #region Configuration cards
        private void BuildConfigurationTab()
        {
            TabPage page = new() { Text = "Configuration", BackColor = Color.White };
            _configurationTab = page;
            MainTabControl.TabPages.Add(value: page);
            _periodCard = new GroupBox { Text = "Report Time Period", Location = new Point(x: 20, y: 12), Size = new Size(width: 836, height: 68) };
            _configurationCommentsCard = new GroupBox { Text = "Comments", Location = new Point(x: 12, y: 362), Size = new Size(width: 912, height: 126), Padding = new Padding(all: 8) };
            _configurationComments = new TextBox { ReadOnly = true, Multiline = true, BorderStyle = BorderStyle.None, BackColor = Color.White, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
            _configurationCommentsCard.Controls.Add(value: _configurationComments);
            page.Controls.Add(value: _periodCard);
            page.Controls.Add(value: _configurationCommentsCard);
            _configurationHelp = new() { Text = "?", AccessibleName = "Open TaxSolutions help", Location = new Point(x: 878, y: 20), Size = new Size(width: 34, height: 34) };
            _configurationHelp.Click += ShowInterfaceHelp;
            page.Controls.Add(value: _configurationHelp);

            AddConfigurationLabel(parent: _periodCard, text: "Year:", x: 16, y: 34);
            _reportYear = new NumericUpDown { Minimum = 1900, Maximum = 9999, Value = DefaultReportYear, Location = new Point(x: 65, y: 29), Width = 90 };
            _periodCard.Controls.Add(value: _reportYear);
            AddConfigurationLabel(parent: _periodCard, text: "From:", x: 195, y: 34);
            _fromMonth = CreateMonthSelector(x: 245, month: 1);
            AddConfigurationLabel(parent: _periodCard, text: "To:", x: 450, y: 34);
            _toMonth = CreateMonthSelector(x: 485, month: 3);
            for (int pair = 0; pair < CurrencyPairCount; pair++)
            {
                GroupBox card = new() { Text = "Conversion factor: " + _currencyPairs[pair], Location = new Point(x: 20, y: 94 + pair * 84), Size = new Size(width: 896, height: 78) };
                _ratesCards[pair] = card;
                page.Controls.Add(value: card);
                for (int month = 0; month < MaximumReportMonths; month++)
                {
                    _rateMonths[pair, month] = new Label { AutoSize = true, Location = new Point(x: 16 + month * 280, y: 20) };
                    TextBox entry = new() { Location = new Point(x: 16 + month * 280, y: 42), Width = 240, MaxLength = 30 };
                    entry.Leave += FormatRateEntry;
                    _rateValues[pair, month] = entry;
                    card.Controls.Add(value: _rateMonths[pair, month]);
                    card.Controls.Add(value: entry);
                }
            }
            _saveConfiguration = new Button { Text = "Save Configuration", Location = new Point(x: 20, y: 350), Size = new Size(width: 180, height: 28) };
            _saveConfiguration.Click += SaveConfiguration_Click;
            page.Controls.Add(value: _saveConfiguration);
            _reportYear.ValueChanged += ReportPeriodChanged;
            _fromMonth.SelectedIndexChanged += ReportPeriodChanged;
            _toMonth.SelectedIndexChanged += ReportPeriodChanged;
            LoadReportConfiguration();
        }

        private static void AddConfigurationLabel(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(value: new Label { Text = text, AutoSize = true, Location = new Point(x: x, y: y) });
        }

        private ComboBox CreateMonthSelector(int x, int month)
        {
            ComboBox selector = new() { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(x: x, y: 29), Width = 170 };
            for (int i = 1; i <= 12; i++) selector.Items.Add(item: CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month: i));
            selector.SelectedIndex = month - 1;
            _periodCard.Controls.Add(value: selector);
            return selector;
        }

        private void LayoutConfigurationTab()
        {
            TabPage page = _configurationTab;
            _periodCard.SetBounds(x: DisplayUnits(value: 20), y: DisplayUnits(value: 12), width: page.ClientSize.Width - DisplayUnits(value: 100), height: DisplayUnits(value: 68));
            for (int pair = 0; pair < CurrencyPairCount; pair++)
                _ratesCards[pair].SetBounds(x: DisplayUnits(value: 20), y: DisplayUnits(value: 94 + pair * 84), width: page.ClientSize.Width - DisplayUnits(value: 40), height: DisplayUnits(value: 78));
            _saveConfiguration.SetBounds(x: DisplayUnits(value: 20), y: DisplayUnits(value: 350), width: DisplayUnits(value: 180), height: DisplayUnits(value: 28));
            _configurationCommentsCard.SetBounds(x: DisplayUnits(value: 12), y: DisplayUnits(value: 394), width: page.ClientSize.Width - DisplayUnits(value: 24), height: page.ClientSize.Height - DisplayUnits(value: 406));
            _configurationHelp.Location = new Point(x: page.ClientSize.Width - DisplayUnits(value: 54), y: DisplayUnits(value: 20));
        }
        #endregion

        #region Period and exchange rate validation
        private static bool TryParseRate(string text, out decimal rate)
        {
            // Accept one decimal separator; never interpret a grouping separator.
            string normalized = text.Trim().Replace(oldChar: ',', newChar: '.');
            bool parsed = decimal.TryParse(s: normalized, style: NumberStyles.AllowDecimalPoint, provider: CultureInfo.InvariantCulture, result: out rate);
            rate = decimal.Round(d: rate, decimals: RateDecimalPlaces, mode: MidpointRounding.AwayFromZero);
            return parsed && rate > 0m;
        }

        private void ReportPeriodChanged(object? sender, EventArgs e)
        {
            if (_loadingConfiguration) return;
            CacheEnteredRates();
            RefreshRateMonths();
        }

        private static void FormatRateEntry(object? sender, EventArgs e)
        {
            if (sender is TextBox entry && TryParseRate(text: entry.Text, rate: out decimal rate))
                entry.Text = rate.ToString(format: RateDisplayFormat, provider: CultureInfo.InvariantCulture);
        }

        private void CacheEnteredRates()
        {
            for (int pair = 0; pair < CurrencyPairCount; pair++)
            for (int month = 0; month < MaximumReportMonths; month++)
            {
                TextBox entry = _rateValues[pair, month];
                if (entry.Tag is not string key) continue;
                if (TryParseRate(text: entry.Text, rate: out decimal rate)) _monthlyRates[pair][key] = rate;
                else _monthlyRates[pair].Remove(key: key);
            }
        }

        private void RefreshRateMonths()
        {
            int first = _fromMonth.SelectedIndex + 1;
            int last = _toMonth.SelectedIndex + 1;
            int count = last - first + 1;
            bool validPeriod = count >= 1 && count <= MaximumReportMonths;
            _saveConfiguration.Enabled = validPeriod;
            for (int pair = 0; pair < CurrencyPairCount; pair++)
            for (int monthIndex = 0; monthIndex < MaximumReportMonths; monthIndex++)
            {
                bool active = validPeriod && monthIndex < count;
                TextBox entry = _rateValues[pair, monthIndex];
                entry.Enabled = active;
                entry.Tag = null;
                entry.Text = string.Empty;
                _rateMonths[pair, monthIndex].Text = "Not required";
                if (!active) continue;
                DateTime month = new(year: (int)_reportYear.Value, month: first + monthIndex, day: 1);
                string key = month.ToString(format: "yyyy-MM", provider: CultureInfo.InvariantCulture);
                _rateMonths[pair, monthIndex].Text = month.ToString(format: "MMMM yyyy", provider: CultureInfo.InvariantCulture);
                entry.Tag = key;
                if (!_monthlyRates[pair].TryGetValue(key: key, value: out decimal rate))
                {
                    rate = 1.000000m;
                    _monthlyRates[pair].Add(key: key, value: rate);
                }
                entry.Text = rate.ToString(format: RateDisplayFormat, provider: CultureInfo.InvariantCulture);
            }
            _configurationComments.Text = validPeriod
                ? "Enter destination currency per 1 unit of source currency for each month. Values are stored and displayed to six decimal places.\r\nSave Configuration stores all three currency pairs. Halifax conversion uses the saved report period and GBP-to-EUR factors."
                : "Select one to three consecutive months within the selected year. The To month must not precede the From month.";
        }
        #endregion

        #region Six decimal place JSON storage
        private sealed class SixDecimalRateConverter : JsonConverter<decimal>
        {
            public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();

            public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
            {
                writer.WriteRawValue(json: value.ToString(format: RateDisplayFormat, provider: CultureInfo.InvariantCulture));
            }
        }

        private static JsonSerializerOptions CreateConfigurationJsonOptions()
        {
            JsonSerializerOptions options = new() { WriteIndented = true };
            options.Converters.Add(item: new SixDecimalRateConverter());
            return options;
        }
        #endregion

        #region Load and save report configuration
        private void LoadReportConfiguration()
        {
            string? warning = null;
            _loadingConfiguration = true;
            try
            {
                if (File.Exists(path: ConfigurationPath))
                {
                    ReportConfiguration settings = JsonSerializer.Deserialize<ReportConfiguration>(json: File.ReadAllText(path: ConfigurationPath))
                        ?? throw new InvalidDataException(message: "The configuration is empty.");
                    if (settings.Year < 1900 || settings.Year > 9999 || settings.FromMonth < 1 || settings.ToMonth > 12 || settings.ToMonth < settings.FromMonth || settings.ToMonth - settings.FromMonth >= MaximumReportMonths || settings.ExchangeRateDefinition != RateDefinition || settings.MonthlyGbpToEurRates is null || settings.MonthlyGbpToZarRates is null || settings.MonthlyEurToZarRates is null)
                        throw new InvalidDataException(message: "The saved report period or exchange-rate definition is invalid.");
                    Dictionary<string, decimal>[] savedPairs = { settings.MonthlyGbpToEurRates, settings.MonthlyGbpToZarRates, settings.MonthlyEurToZarRates };
                    foreach (Dictionary<string, decimal> savedPair in savedPairs)
                    foreach (KeyValuePair<string, decimal> entry in savedPair)
                    {
                        if (!DateTime.TryParseExact(s: entry.Key, format: "yyyy-MM", provider: CultureInfo.InvariantCulture, style: DateTimeStyles.None, result: out _) || decimal.Round(d: entry.Value, decimals: RateDecimalPlaces, mode: MidpointRounding.AwayFromZero) <= 0m)
                            throw new InvalidDataException(message: "The saved monthly conversion factors are invalid.");
                    }
                    _reportYear.Value = settings.Year;
                    _fromMonth.SelectedIndex = settings.FromMonth - 1;
                    _toMonth.SelectedIndex = settings.ToMonth - 1;
                    for (int pair = 0; pair < CurrencyPairCount; pair++)
                    foreach (KeyValuePair<string, decimal> entry in savedPairs[pair])
                        _monthlyRates[pair].Add(key: entry.Key, value: decimal.Round(d: entry.Value, decimals: RateDecimalPlaces, mode: MidpointRounding.AwayFromZero));
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidDataException)
            {
                warning = "Configuration could not be loaded: " + ex.Message + "\r\nThe saved file has not been changed. Enter and save valid settings to replace it.";
            }
            finally { _loadingConfiguration = false; }
            RefreshRateMonths();
            if (warning is not null) _configurationComments.Text = warning;
        }

        private void SaveConfiguration_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!_saveConfiguration.Enabled) throw new InvalidOperationException(message: "Select a valid report period of up to three months.");
                for (int pair = 0; pair < CurrencyPairCount; pair++)
                for (int i = 0; i < MaximumReportMonths; i++)
                {
                    if (_rateValues[pair, i].Enabled && !TryParseRate(text: _rateValues[pair, i].Text, rate: out _))
                    {
                        _rateValues[pair, i].Focus();
                        throw new InvalidOperationException(message: "Enter a positive conversion factor for " + _currencyPairs[pair] + " / " + _rateMonths[pair, i].Text + ".");
                    }
                }
                CacheEnteredRates();
                ReportConfiguration settings = new() { Year = (int)_reportYear.Value, FromMonth = _fromMonth.SelectedIndex + 1, ToMonth = _toMonth.SelectedIndex + 1, MonthlyGbpToEurRates = new Dictionary<string, decimal>(dictionary: _monthlyRates[0], comparer: StringComparer.Ordinal), MonthlyGbpToZarRates = new Dictionary<string, decimal>(dictionary: _monthlyRates[1], comparer: StringComparer.Ordinal), MonthlyEurToZarRates = new Dictionary<string, decimal>(dictionary: _monthlyRates[2], comparer: StringComparer.Ordinal) };
                string temporaryPath = ConfigurationPath + "." + Guid.NewGuid().ToString(format: "N") + ".tmp";
                try
                {
                    File.WriteAllText(path: temporaryPath, contents: JsonSerializer.Serialize(value: settings, options: CreateConfigurationJsonOptions()));
                    File.Move(sourceFileName: temporaryPath, destFileName: ConfigurationPath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(path: temporaryPath)) File.Delete(path: temporaryPath);
                }
                RefreshRateMonths();
                _configurationComments.Text = "Configuration saved successfully.\r\n" + ConfigurationPath + "\r\nAll three currency pairs saved to six decimal places.";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                _configurationComments.Text = "Configuration was not saved: " + ex.Message;
            }
        }
        #endregion
    }
}

