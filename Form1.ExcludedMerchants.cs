using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace TaxSolutions
{
    public partial class Form1
    {
        #region Shared merchant model and versioned storage
        private const string MerchantPreferencesFileName = "TaxSolutions.merchants.json";
        private const int MerchantStorageVersion = 2;

        private sealed class MerchantExclusion
        {
            public required string Id { get; init; }
            public required string Description { get; set; }
            public bool Exclude { get; set; } = true;
        }

        private sealed class MerchantDocument
        {
            public int Version { get; set; } = MerchantStorageVersion;
            public List<MerchantExclusion> Merchants { get; set; } = new();
            public List<string> KnownDefaultIds { get; set; } = new();
        }

        private readonly List<MerchantExclusion> _merchantExclusions = new();
        private readonly HashSet<string> _knownMerchantDefaultIds = new(comparer: StringComparer.OrdinalIgnoreCase);
        private TabPage _excludedMerchantsTab = null!;
        private DataGridView _merchantGrid = null!;
        private TextBox _merchantComments = null!;
        private GroupBox _merchantGridCard = null!;
        private GroupBox _merchantCommentsCard = null!;
        private GroupBox _merchantEditorCard = null!;
        private Label _merchantInstructions = null!;
        private Button _merchantHelp = null!;
        private TextBox _merchantPhrase = null!;
        private Button _addMerchant = null!;
        private Button _editMerchant = null!;
        private Button _deleteMerchant = null!;
        private Button _saveMerchant = null!;
        private bool _updatingMerchantGrid;

        private static string MerchantPreferencesPath => Path.Combine(
            path1: Path.GetDirectoryName(path: typeof(Form1).Assembly.Location) ?? AppContext.BaseDirectory,
            path2: MerchantPreferencesFileName);

        private static List<MerchantExclusion> CreateDefaultMerchantExclusions()
        {
            List<MerchantExclusion> defaults = new()
            {
                new() { Id = "type:Exchange", Description = "Exchange" }
            };
            foreach (string merchant in RevolutExcludedCardMerchants)
                defaults.Add(item: new MerchantExclusion { Id = "card:" + merchant, Description = merchant });
            foreach (RevolutExclusionRule rule in RevolutExclusionRules)
                defaults.Add(item: new MerchantExclusion { Id = "pair:" + rule.Type + ":" + rule.Description, Description = rule.Description });
            foreach (string phrase in new[] { "Bayerischer Rundfunk", "e.on", "Julian Gray", "Julian Gray Land Surveyor", "JULIAN ANDRE LE FR" })
                defaults.Add(item: new MerchantExclusion { Id = "global:" + phrase, Description = phrase });
            return defaults;
        }

        private static MerchantExclusion CopyMerchant(MerchantExclusion merchant) => new()
        {
            Id = merchant.Id, Description = merchant.Description, Exclude = merchant.Exclude
        };

        private static void ValidateMerchantEntries(List<MerchantExclusion> entries)
        {
            HashSet<string> ids = new(comparer: StringComparer.OrdinalIgnoreCase);
            HashSet<string> phrases = new(comparer: StringComparer.OrdinalIgnoreCase);
            foreach (MerchantExclusion entry in entries)
            {
                if (entry is null || string.IsNullOrWhiteSpace(value: entry.Id) || string.IsNullOrWhiteSpace(value: entry.Description) ||
                    !ids.Add(item: entry.Id) || !phrases.Add(item: entry.Description.Trim()))
                    throw new InvalidDataException(message: "Merchant entries must have unique IDs and nonblank, unique phrases (ignoring case).");
                entry.Description = entry.Description.Trim();
            }
        }
        #endregion

        #region Global case-insensitive phrase matching
        private bool IsExcludedMerchant(string description)
        {
            foreach (MerchantExclusion filter in _merchantExclusions)
                if (filter.Exclude && description.Contains(value: filter.Description, comparisonType: StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private bool IsExcludedDeutscheBankMerchant(string sourceLine, string normalizedDescription)
        {
            // Match the original beneficiary, retaining commas and punctuation removed by the legacy converter.
            using StringReader source = new(s: sourceLine);
            using TextFieldParser parser = new(reader: source);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(delimiters: new[] { ";" });
            parser.HasFieldsEnclosedInQuotes = true;
            parser.TrimWhiteSpace = false;
            try
            {
                string[] fields = parser.ReadFields() ?? Array.Empty<string>();
                return IsExcludedMerchant(description: fields.Length > 3 ? fields[3].Trim() : normalizedDescription);
            }
            catch (MalformedLineException)
            {
                // Preserve the legacy normalization fallback for nonstandard exports.
                return IsExcludedMerchant(description: normalizedDescription);
            }
        }
        #endregion

        #region Excluded merchants tab and editor card
        private void BuildExcludedMerchantsTab()
        {
            _excludedMerchantsTab = new TabPage { Text = "Excluded merchants", BackColor = Color.White };
            MainTabControl.TabPages.Add(value: _excludedMerchantsTab);
            _merchantInstructions = new Label
            {
                Text = "All bank files: checked phrases exclude any description containing the complete phrase, ignoring case.\r\nCheckboxes save automatically. Save replaces selection; Add creates a new entry. Normal bank rules still apply.",
                Location = new Point(x: 20, y: 16), Size = new Size(width: 825, height: 40)
            };
            _merchantHelp = new Button { Text = "?", AccessibleName = "Open TaxSolutions help", Location = new Point(x: 878, y: 16), Size = new Size(width: 34, height: 34) };
            _merchantHelp.Click += ShowInterfaceHelp;
            _merchantGridCard = new GroupBox { Text = "Excluded merchant phrases — all banks", Padding = new Padding(all: 8) };
            _merchantGrid = new DataGridView
            {
                Name = "ExcludedMerchantsGrid", Dock = DockStyle.Fill, AutoGenerateColumns = false,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _merchantGrid.Columns.Add(dataGridViewColumn: new DataGridViewCheckBoxColumn
            {
                Name = "Exclude", HeaderText = "Exclude", Width = 75, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ValueType = typeof(bool)
            });
            _merchantGrid.Columns.Add(dataGridViewColumn: new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Merchant / Phrase", ReadOnly = true });
            _merchantGridCard.Controls.Add(value: _merchantGrid);
            _merchantEditorCard = new GroupBox { Text = "Merchant / Phrase" };
            _merchantPhrase = new TextBox { Location = new Point(x: 16, y: 28), Width = 400, AccessibleName = "Merchant phrase" };
            _addMerchant = new Button { Text = "Add", Location = new Point(x: 546, y: 25), Size = new Size(width: 100, height: 30) };
            _editMerchant = new Button { Text = "Edit", Location = new Point(x: 436, y: 25), Size = new Size(width: 100, height: 30) };
            _deleteMerchant = new Button { Text = "Delete", Location = new Point(x: 656, y: 25), Size = new Size(width: 100, height: 30) };
            _saveMerchant = new Button { Text = "Save", Location = new Point(x: 766, y: 25), Size = new Size(width: 100, height: 30) };
            _saveMerchant.Click += (sender, e) => SaveSelectedMerchant();
            _addMerchant.Click += (sender, e) => AddMerchantPhrase(phrase: _merchantPhrase.Text);
            _editMerchant.Click += (sender, e) => EditSelectedMerchant();
            _deleteMerchant.Click += (sender, e) => DeleteSelectedMerchant();
            _merchantEditorCard.Controls.Add(value: _merchantPhrase);
            _merchantEditorCard.Controls.Add(value: _addMerchant);
            _addMerchant.TabIndex = 1;
            _merchantEditorCard.Controls.Add(value: _editMerchant);
            _editMerchant.TabIndex = 0;
            _merchantEditorCard.Controls.Add(value: _saveMerchant);
            _saveMerchant.TabIndex = 3;
            _merchantEditorCard.Controls.Add(value: _deleteMerchant);
            _deleteMerchant.TabIndex = 2;
            _merchantCommentsCard = new GroupBox { Text = "Comments", Padding = new Padding(all: 8) };
            _merchantComments = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White, ScrollBars = ScrollBars.Vertical };
            _merchantCommentsCard.Controls.Add(value: _merchantComments);
            _excludedMerchantsTab.Controls.Add(value: _merchantInstructions);
            _excludedMerchantsTab.Controls.Add(value: _merchantHelp);
            _excludedMerchantsTab.Controls.Add(value: _merchantGridCard);
            _excludedMerchantsTab.Controls.Add(value: _merchantEditorCard);
            _excludedMerchantsTab.Controls.Add(value: _merchantCommentsCard);
            LoadMerchantChoices();
            RefreshMerchantGrid(selectedId: null);
            _merchantGrid.CurrentCellDirtyStateChanged += CommitMerchantCheckBox;
            _merchantGrid.CellValueChanged += MerchantChoiceChanged;
            _merchantGrid.SelectionChanged += MerchantSelectionChanged;
            MerchantSelectionChanged(sender: this, e: EventArgs.Empty);
        }

        private void LayoutExcludedMerchantsTab()
        {
            TabPage page = _excludedMerchantsTab;
            int commentsTop = page.ClientSize.Height - DisplayUnits(value: 95);
            int editorTop = commentsTop - DisplayUnits(value: 90);
            _merchantInstructions.SetBounds(x: DisplayUnits(value: 20), y: DisplayUnits(value: 16), width: page.ClientSize.Width - DisplayUnits(value: 105), height: DisplayUnits(value: 40));
            _merchantHelp.SetBounds(x: page.ClientSize.Width - DisplayUnits(value: 54), y: DisplayUnits(value: 16), width: DisplayUnits(value: 34), height: DisplayUnits(value: 34));
            _merchantGridCard.SetBounds(x: DisplayUnits(value: 20), y: DisplayUnits(value: 65), width: page.ClientSize.Width - DisplayUnits(value: 40), height: editorTop - DisplayUnits(value: 77));
            _merchantEditorCard.SetBounds(x: DisplayUnits(value: 20), y: editorTop, width: page.ClientSize.Width - DisplayUnits(value: 40), height: DisplayUnits(value: 78));
            _merchantCommentsCard.SetBounds(x: DisplayUnits(value: 12), y: commentsTop, width: page.ClientSize.Width - DisplayUnits(value: 24), height: DisplayUnits(value: 83));
        }

        private void CommitMerchantCheckBox(object? sender, EventArgs e)
        {
            if (_merchantGrid.IsCurrentCellDirty) _merchantGrid.CommitEdit(context: DataGridViewDataErrorContexts.Commit);
        }

        private void MerchantSelectionChanged(object? sender, EventArgs e)
        {
            if (_updatingMerchantGrid) return;
            MerchantExclusion? entry = _merchantGrid.CurrentRow?.Tag as MerchantExclusion;
            _merchantPhrase.Text = entry?.Description ?? string.Empty;
            _editMerchant.Enabled = entry is not null;
            _saveMerchant.Enabled = entry is not null;
            _deleteMerchant.Enabled = entry is not null;
        }

        private void RefreshMerchantGrid(string? selectedId)
        {
            _updatingMerchantGrid = true;
            try
            {
                _merchantExclusions.Sort(comparison: (left, right) => string.Compare(strA: left.Description, strB: right.Description, comparisonType: StringComparison.OrdinalIgnoreCase));
                _merchantGrid.Rows.Clear();
                int selectedRow = -1;
                foreach (MerchantExclusion entry in _merchantExclusions)
                {
                    int index = _merchantGrid.Rows.Add(values: new object[] { entry.Exclude, entry.Description });
                    _merchantGrid.Rows[index].Tag = entry;
                    if (string.Equals(a: entry.Id, b: selectedId, comparisonType: StringComparison.OrdinalIgnoreCase)) selectedRow = index;
                }
                if (selectedRow >= 0) _merchantGrid.CurrentCell = _merchantGrid.Rows[selectedRow].Cells[1];
            }
            finally { _updatingMerchantGrid = false; }
            MerchantSelectionChanged(sender: this, e: EventArgs.Empty);
        }
        #endregion

        #region Add, edit and delete operations
        private string ValidateMerchantPhrase(string phrase, string? excludedId)
        {
            string value = phrase.Trim();
            if (value.Length == 0) throw new InvalidDataException(message: "Enter a nonblank merchant phrase.");
            foreach (MerchantExclusion entry in _merchantExclusions)
                if (!string.Equals(a: entry.Id, b: excludedId, comparisonType: StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(a: entry.Description, b: value, comparisonType: StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(message: "That merchant phrase already exists (ignoring case).");
            return value;
        }

        private MerchantExclusion FindMerchant(string id)
        {
            foreach (MerchantExclusion entry in _merchantExclusions)
                if (string.Equals(a: entry.Id, b: id, comparisonType: StringComparison.OrdinalIgnoreCase)) return entry;
            throw new InvalidDataException(message: "Select an existing merchant phrase.");
        }

        private bool ApplyMerchantMutation(Action change, string message, string? selectedId)
        {
            string? previousSelection = (_merchantGrid.CurrentRow?.Tag as MerchantExclusion)?.Id;
            string previousDraft = _merchantPhrase.Text;
            List<MerchantExclusion> before = new(capacity: _merchantExclusions.Count);
            foreach (MerchantExclusion entry in _merchantExclusions) before.Add(item: CopyMerchant(merchant: entry));
            try
            {
                change();
                ValidateMerchantEntries(entries: _merchantExclusions);
                SaveMerchantChoices();
                RefreshMerchantGrid(selectedId: selectedId);
                _merchantComments.Text = message + "\r\nSaved. Applies to the next conversion for all banks.";
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                _merchantExclusions.Clear();
                _merchantExclusions.AddRange(collection: before);
                RefreshMerchantGrid(selectedId: previousSelection);
                _merchantPhrase.Text = previousDraft;
                _merchantComments.Text = "Merchant list was not changed: " + ex.Message;
                return false;
            }
        }

        private bool AddMerchantPhrase(string phrase)
        {
            string id = "custom:" + Guid.NewGuid().ToString(format: "N");
            return ApplyMerchantMutation(change: () => _merchantExclusions.Add(item: new MerchantExclusion
            {
                Id = id, Description = ValidateMerchantPhrase(phrase: phrase, excludedId: null)
            }), message: "Merchant added.", selectedId: id);
        }

        private bool EditMerchantPhrase(string id, string phrase)
        {
            return ApplyMerchantMutation(change: () => FindMerchant(id: id).Description = ValidateMerchantPhrase(phrase: phrase, excludedId: id),
                message: "Merchant updated.", selectedId: id);
        }

        private bool DeleteMerchantPhrase(string id)
        {
            return ApplyMerchantMutation(change: () => _merchantExclusions.Remove(item: FindMerchant(id: id)), message: "Merchant deleted.", selectedId: null);
        }

        private void EditSelectedMerchant()
        {
            if (_merchantGrid.CurrentRow?.Tag is not MerchantExclusion) return;
            _merchantPhrase.Focus();
            _merchantPhrase.SelectAll();
            _merchantComments.Text = "Edit the phrase, then choose Save to replace the selected entry. Add creates a separate entry.";
        }

        private void SaveSelectedMerchant()
        {
            if (_merchantGrid.CurrentRow?.Tag is MerchantExclusion entry)
                EditMerchantPhrase(id: entry.Id, phrase: _merchantPhrase.Text);
        }

        private void DeleteSelectedMerchant()
        {
            if (_merchantGrid.CurrentRow?.Tag is MerchantExclusion entry) DeleteMerchantPhrase(id: entry.Id);
        }
        #endregion

        #region Load, migrate and persist shared merchant list
        private void LoadMerchantChoices()
        {
            List<MerchantExclusion> defaults = CreateDefaultMerchantExclusions();
            List<MerchantExclusion> loaded = new();
            HashSet<string> known = new(comparer: StringComparer.OrdinalIgnoreCase);
            string? warning = null;
            try
            {
                if (File.Exists(path: MerchantPreferencesPath))
                {
                    string json = File.ReadAllText(path: MerchantPreferencesPath);
                    using JsonDocument document = JsonDocument.Parse(json: json);
                    if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException(message: "The saved merchant list must be an object.");
                    if (document.RootElement.TryGetProperty(propertyName: "Version", value: out _))
                    {
                        MerchantDocument saved = JsonSerializer.Deserialize<MerchantDocument>(json: json)
                            ?? throw new InvalidDataException(message: "The saved merchant list is empty.");
                        if (saved.Version != MerchantStorageVersion || saved.Merchants is null || saved.KnownDefaultIds is null)
                            throw new InvalidDataException(message: "The saved merchant-list format is unsupported.");
                        ValidateMerchantEntries(entries: saved.Merchants);
                        loaded.AddRange(collection: saved.Merchants);
                        foreach (string id in saved.KnownDefaultIds)
                        {
                            if (string.IsNullOrWhiteSpace(value: id) || !known.Add(item: id))
                                throw new InvalidDataException(message: "The saved default-entry history is invalid.");
                        }
                    }
                    else
                    {
                        Dictionary<string, bool> old = JsonSerializer.Deserialize<Dictionary<string, bool>>(json: json)
                            ?? throw new InvalidDataException(message: "The earlier merchant choices are empty.");
                        Dictionary<string, bool> choices = new(dictionary: old, comparer: StringComparer.OrdinalIgnoreCase);
                        foreach (MerchantExclusion entry in defaults)
                        {
                            MerchantExclusion copy = CopyMerchant(merchant: entry);
                            if (choices.TryGetValue(key: copy.Id, value: out bool exclude)) copy.Exclude = exclude;
                            loaded.Add(item: copy);
                        }
                    }
                }
                foreach (MerchantExclusion entry in defaults)
                {
                    bool present = false;
                    foreach (MerchantExclusion existing in loaded)
                        if (string.Equals(a: existing.Id, b: entry.Id, comparisonType: StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a: existing.Description, b: entry.Description, comparisonType: StringComparison.OrdinalIgnoreCase)) { present = true; break; }
                    if (!known.Contains(item: entry.Id) && !present) loaded.Add(item: CopyMerchant(merchant: entry));
                    known.Add(item: entry.Id);
                }
                ValidateMerchantEntries(entries: loaded);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
            {
                warning = "Saved merchant list could not be loaded; default phrases are active. The saved file has not been changed.\r\n" + ex.Message;
                loaded.Clear();
                loaded.AddRange(collection: defaults);
                known.Clear();
                foreach (MerchantExclusion entry in defaults) known.Add(item: entry.Id);
            }
            _merchantExclusions.AddRange(collection: loaded);
            _knownMerchantDefaultIds.UnionWith(other: known);
            _merchantComments.Text = warning ?? "Ready. Exclusions apply to original descriptions in every bank file.\r\n" + MerchantPreferencesPath;
        }

        private void MerchantChoiceChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_updatingMerchantGrid || e.RowIndex < 0 || e.ColumnIndex != 0) return;
            DataGridViewRow row = _merchantGrid.Rows[e.RowIndex];
            if (row.Tag is not MerchantExclusion filter) return;
            bool previous = filter.Exclude;
            filter.Exclude = row.Cells[0].Value is bool check && check;
            try
            {
                SaveMerchantChoices();
                _merchantComments.Text = (filter.Exclude ? "Excluded: " : "Allowed: ") + filter.Description + "\r\nSaved. Applies to all bank conversions; other exclusions still apply.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                filter.Exclude = previous;
                _updatingMerchantGrid = true;
                try { row.Cells[0].Value = previous; }
                finally { _updatingMerchantGrid = false; }
                _merchantComments.Text = "Choice not saved; previous setting remains active.\r\n" + ex.Message;
            }
        }

        private void SaveMerchantChoices()
        {
            MerchantDocument saved = new();
            foreach (MerchantExclusion entry in _merchantExclusions) saved.Merchants.Add(item: CopyMerchant(merchant: entry));
            saved.Merchants.Sort(comparison: (left, right) => string.Compare(strA: left.Description, strB: right.Description, comparisonType: StringComparison.OrdinalIgnoreCase));
            saved.KnownDefaultIds.AddRange(collection: _knownMerchantDefaultIds);
            saved.KnownDefaultIds.Sort(comparer: StringComparer.Ordinal);
            string temporaryPath = MerchantPreferencesPath + "." + Guid.NewGuid().ToString(format: "N") + ".tmp";
            try
            {
                File.WriteAllText(path: temporaryPath, contents: JsonSerializer.Serialize(value: saved, options: new JsonSerializerOptions { WriteIndented = true }));
                File.Move(sourceFileName: temporaryPath, destFileName: MerchantPreferencesPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(path: temporaryPath)) File.Delete(path: temporaryPath);
            }
        }
        #endregion
    }
}


