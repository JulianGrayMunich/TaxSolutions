using System.Text.Json;

namespace TaxSolutions
{
    public partial class Form1
    {
        #region Remembered file selection
        private const string FileSelectionPreferencesFileName = "TaxSolutions.files.json";
        private string _lastSelectedCsvPath = string.Empty;
        private string _lastBrowseFolder = string.Empty;
        private string _combinedFolderOverride = string.Empty;

        private sealed class FileSelectionPreferences
        {
            public string LastSelectedCsvPath { get; set; } = string.Empty;
            public string LastBrowseFolder { get; set; } = string.Empty;
            public string CombinedFolderOverride { get; set; } = string.Empty;
        }

        private static string FileSelectionPreferencesPath => Path.Combine(
            path1: Path.GetDirectoryName(path: typeof(Form1).Assembly.Location) ?? AppContext.BaseDirectory,
            path2: FileSelectionPreferencesFileName);

        private void LoadFileSelectionPreferences()
        {
            try
            {
                if (!File.Exists(path: FileSelectionPreferencesPath)) return;
                FileSelectionPreferences saved = JsonSerializer.Deserialize<FileSelectionPreferences>(json: File.ReadAllText(path: FileSelectionPreferencesPath))
                    ?? throw new InvalidDataException(message: "File-selection preferences are empty.");
                _lastSelectedCsvPath = saved.LastSelectedCsvPath ?? string.Empty;
                _lastBrowseFolder = saved.LastBrowseFolder ?? string.Empty;
                _combinedFolderOverride = saved.CombinedFolderOverride ?? string.Empty;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                for (int i = 0; i < _comments.Count; i++)
                    _comments[i].Text = "The previous Browse folder could not be restored: " + ex.Message;
            }
            finally { RefreshRetainedFolder(); }
        }

        private const string DefaultBrowseFolder = @"C:\";
        private string RetainedBrowseFolder => Directory.Exists(path: _lastBrowseFolder) ? _lastBrowseFolder : DefaultBrowseFolder;

        private void RefreshRetainedFolder()
        {
            _lastBrowseFolder = RetainedBrowseFolder;
            if (Directory.Exists(path: _combinedFolderOverride)) { _masterFolder.Text = _combinedFolderOverride; return; }
            string folder = _lastBrowseFolder.TrimEnd(trimChars: new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            _masterFolder.Text = string.Equals(a: Path.GetFileName(path: folder), b: ConvertedFolderName, comparisonType: StringComparison.OrdinalIgnoreCase)
                ? _lastBrowseFolder : Path.Combine(path1: _lastBrowseFolder, path2: ConvertedFolderName);
        }

        private void SelectMasterFolder(string folder)
        {
            if (!Directory.Exists(path: folder)) throw new DirectoryNotFoundException(message: "The selected combined-report folder is unavailable.");
            _combinedFolderOverride = Path.GetFullPath(path: folder);
            RefreshRetainedFolder();
            _masterComments.Text = "Combined reports folder: " + _masterFolder.Text + "\r\nPlace the bank Income and Expenses files in this folder before generating combined reports.";
            SaveFileSelectionPreferences();
        }

        private void SetBankCsvSelection(TextBox target, string selectedPath, int bankIndex)
        {
            ValidateBankCsvSelection(target: target, path: selectedPath, bankIndex: bankIndex);
            string fullPath = Path.GetFullPath(path: selectedPath);
            target.Tag = fullPath;
            target.Text = Path.GetFileName(path: fullPath);
            RefreshConversionAvailability();
            _comments[bankIndex].AppendText(text: "\r\nSelected CSV: " + fullPath);
            RecordCsvSelection(selectedPath: fullPath);
        }

        private static string SelectedBankCsvPath(TextBox target) => string.IsNullOrWhiteSpace(value: target.Text) ? string.Empty : target.Tag as string ?? string.Empty;

        private static void ClearBankCsvSelection(TextBox target)
        {
            target.Clear();
            target.Tag = null;
        }

        private OpenFileDialog CreateCsvFileDialog()
        {
            return new OpenFileDialog
            {
                Title = "Select CSV File",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true,
                RestoreDirectory = true,
                InitialDirectory = RetainedBrowseFolder,
                FileName = string.Empty
            };
        }

        private void RecordCsvSelection(string selectedPath)
        {
            _lastSelectedCsvPath = Path.GetFullPath(path: selectedPath);
            _lastBrowseFolder = Path.GetDirectoryName(path: _lastSelectedCsvPath)
                ?? throw new InvalidDataException(message: "Unable to determine the selected CSV folder.");
            RefreshRetainedFolder();
            SaveFileSelectionPreferences();
        }

        private void SaveFileSelectionPreferences()
        {
            string temporaryPath = FileSelectionPreferencesPath + "." + Guid.NewGuid().ToString(format: "N") + ".tmp";
            try
            {
                FileSelectionPreferences saved = new() { LastSelectedCsvPath = _lastSelectedCsvPath, LastBrowseFolder = _lastBrowseFolder, CombinedFolderOverride = _combinedFolderOverride };
                File.WriteAllText(path: temporaryPath, contents: JsonSerializer.Serialize(value: saved, options: new JsonSerializerOptions { WriteIndented = true }));
                File.Move(sourceFileName: temporaryPath, destFileName: FileSelectionPreferencesPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(path: temporaryPath)) File.Delete(path: temporaryPath);
            }
        }

        private void BrowseCsvButton_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog dialog = CreateCsvFileDialog();
            if (dialog.ShowDialog(owner: this) != DialogResult.OK) return;
            try
            {
                SetBankCsvSelection(target: CsvFilePathTextBox, selectedPath: dialog.FileName, bankIndex: 1);
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show(owner: this, text: ex.Message, caption: "Halifax File Selection", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Warning);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(owner: this, text: "The selected file is available for this session, but its Browse folder could not be saved.\r\n\r\n" + ex.Message,
                    caption: "File Selection", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Warning);
            }
        }
        #endregion
    }
}






