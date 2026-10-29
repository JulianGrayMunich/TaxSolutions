using System.ComponentModel;
using System.Diagnostics;

namespace TaxSolutions
{
    public partial class Form1
    {
        #region Offline HTML user guide
        private static string GetTaxSolutionsHelpPath()
        {
            return Path.Combine(path1: AppContext.BaseDirectory, path2: "Help", path3: "TaxSolutions_Help.html");
        }

        private void ShowInterfaceHelp(object? sender, EventArgs e) => OpenTaxSolutionsHelp(fragment: string.Empty);

        private static string GetTaxSolutionsHelpUri(string fragment) => new UriBuilder(uri: new Uri(uriString: GetTaxSolutionsHelpPath())) { Fragment = fragment }.Uri.AbsoluteUri;

        private void OpenTaxSolutionsHelp(string fragment)
        {
            string helpPath = GetTaxSolutionsHelpPath();
            try
            {
                if (!File.Exists(path: helpPath))
                    throw new FileNotFoundException(message: "The TaxSolutions help document is missing. Restore the Help folder beside the application. Expected file: " + helpPath, fileName: helpPath);
                Process.Start(startInfo: new ProcessStartInfo { FileName = GetTaxSolutionsHelpUri(fragment: fragment), UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException or UnauthorizedAccessException)
            {
                MessageBox.Show(owner: this, text: "Unable to open TaxSolutions help.\r\n\r\n" + ex.Message + "\r\n\r\nYou can also open the HTML document directly in a web browser.", caption: "TaxSolutions Help", buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Warning);
            }
        }
        #endregion
    }
}


