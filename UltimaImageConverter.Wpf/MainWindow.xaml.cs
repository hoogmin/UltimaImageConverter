using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using UltimaImageConverter.Core;

namespace UltimaImageConverter.Wpf;

// Our immutable data model for the UI
public record ImageFile(string FullPath, string FileName);

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ImageFile> _filesToProcess = new();
    private readonly IImageConverter _converter;
    private CancellationTokenSource? _cts;
    private string? _customOutputDirectory = null;

    public MainWindow()
    {
        InitializeComponent();
        _converter = new MagickImageConverter();

        // Bind the list to the UI
        LstFiles.ItemsSource = _filesToProcess;

        // Auto-update the UI whenever the list changes
        _filesToProcess.CollectionChanged += (s, e) => UpdateUIState();
    }

    private void UpdateUIState(bool preserveStatus = false)
    {
        bool hasFiles = _filesToProcess.Count > 0;
        FileCountText.Visibility = hasFiles ? Visibility.Collapsed : Visibility.Visible;
        BtnConvert.IsEnabled = hasFiles && _cts == null;

        if (!preserveStatus)
        {
            StatusText.Text = hasFiles ? $"{_filesToProcess.Count} file(s) ready to convert." : "Ready";
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var droppedItems = (string[])e.Data.GetData(DataFormats.FileDrop);
            LoadFiles(droppedItems);
        }
    }

    private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Output Directory"
        };

        if (dialog.ShowDialog() == true)
        {
            _customOutputDirectory = dialog.FolderName;
            TxtOutputDir.Text = _customOutputDirectory;
            TxtOutputDir.Foreground = Brushes.White;
            BtnResetOutput.IsEnabled = true;
        }
    }

    private void BtnResetOutput_Click(object sender, RoutedEventArgs e)
    {
        _customOutputDirectory = null;
        TxtOutputDir.Text = "Same as input directory";

        // Revert to the original gray color
        TxtOutputDir.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
        BtnResetOutput.IsEnabled = false;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Supported Images|*.heic;*.heif;*.avif;*.webp;*.jpg;*.jpeg;*.png;*.tiff;*.bmp"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadFiles(openFileDialog.FileNames);
        }
    }

    private void LoadFiles(string[] paths)
    {
        var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".heic", ".heif", ".avif", ".webp", ".jpg", ".jpeg", ".png", ".tiff", ".bmp"
        };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path).Where(f => validExtensions.Contains(Path.GetExtension(f)));
                foreach (var f in files)
                {
                    // Prevent duplicates
                    if (!_filesToProcess.Any(x => x.FullPath.Equals(f, StringComparison.OrdinalIgnoreCase)))
                        _filesToProcess.Add(new ImageFile(f, Path.GetFileName(f)));
                }
            }
            else if (File.Exists(path) && validExtensions.Contains(Path.GetExtension(path)))
            {
                if (!_filesToProcess.Any(x => x.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    _filesToProcess.Add(new ImageFile(path, Path.GetFileName(path)));
            }
        }
    }

    // --- Deletion Logic ---

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _filesToProcess.Clear();
        ProgressBar.Value = 0;
    }

    private void MenuRemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        RemoveSelectedItems();
    }

    private void LstFiles_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            RemoveSelectedItems();
        }
    }

    private void RemoveSelectedItems()
    {
        // Copy to list first to avoid modifying the collection while iterating
        var selected = LstFiles.SelectedItems.Cast<ImageFile>().ToList();
        foreach (var item in selected)
        {
            _filesToProcess.Remove(item);
        }
    }

    // --- Conversion Logic ---

    private async void BtnConvert_Click(object sender, RoutedEventArgs e)
    {
        if (_filesToProcess.Count == 0) return;

        if (_cts is not null)
        {
            _cts.Cancel();
            return;
        }

        _cts = new CancellationTokenSource();
        var formatString = ((System.Windows.Controls.ComboBoxItem)CmbFormat.SelectedItem).Content.ToString();
        var targetFormat = Enum.Parse<OutputFormat>(formatString!);
        var options = new ConversionOptions(targetFormat, Quality: (int)SldQuality.Value);

        BtnConvert.Content = "Cancel";
        ProgressBar.Maximum = _filesToProcess.Count;
        ProgressBar.Value = 0;
        int successCount = 0;

        try
        {
            // Lock the list UI while converting
            LstFiles.IsEnabled = false;
            BtnBrowse.IsEnabled = false;
            BtnClear.IsEnabled = false;
            BtnBrowseOutput.IsEnabled = false;
            BtnResetOutput.IsEnabled = false;

            for (int i = 0; i < _filesToProcess.Count; i++)
            {
                if (_cts.Token.IsCancellationRequested) break;

                // Access the FullPath from our record
                var file = _filesToProcess[i].FullPath;
                var targetDir = _customOutputDirectory ?? Path.GetDirectoryName(file)!;

                // Construct the final output path
                var newFileName = Path.ChangeExtension(Path.GetFileName(file), targetFormat.ToString().ToLowerInvariant());
                var outputFilename = Path.Combine(targetDir, newFileName);

                StatusText.Text = $"Converting: {_filesToProcess[i].FileName}...";

                var success = await _converter.ConvertAsync(file, outputFilename, options, _cts.Token);
                if (success) successCount += 1;

                ProgressBar.Value = i + 1;
            }

            StatusText.Text = _cts.Token.IsCancellationRequested
                ? $"Aborted. Converted {successCount}/{_filesToProcess.Count} files."
                : $"Done! Converted {successCount}/{_filesToProcess.Count} files.";
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            BtnConvert.Content = "Convert";

            // Re-enable UI
            LstFiles.IsEnabled = true;
            BtnBrowse.IsEnabled = true;
            BtnClear.IsEnabled = true;
            BtnBrowseOutput.IsEnabled = true;
            BtnResetOutput.IsEnabled = _customOutputDirectory is not null;

            // Re-evaluate button states, but leave the Done/Aborted message visible
            UpdateUIState(preserveStatus: true);
        }
    }

    // --- Menu Actions ---

    private void MenuOpen_Click(object sender, RoutedEventArgs e) => BtnBrowse_Click(sender, e);
    private void MenuExit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Ultima Image Converter (UIC)\nVersion 1.0.0\n\nAuthor: Javier Martinez\nLicense: BSD-3-Clause",
            "About uic", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}