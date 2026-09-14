using Microsoft.Win32;
using System.IO;
using System.Windows;
using UltimaImageConverter.Core;

namespace UltimaImageConverter.Wpf;

public partial class MainWindow : Window
{
    private List<string> _filesToProcess = new();
    private readonly IImageConverter _converter;
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        _converter = new MagickImageConverter();
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var droppedItems = (string[])e.Data.GetData(DataFormats.FileDrop);
            LoadFiles(droppedItems);
        }
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

        _filesToProcess.Clear();

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                _filesToProcess.AddRange(Directory.GetFiles(path).Where(f => validExtensions.Contains(Path.GetExtension(f))));
            }
            else if (File.Exists(path) && validExtensions.Contains(Path.GetExtension(path)))
            {
                _filesToProcess.Add(path);
            }
        }

        FileCountText.Text = _filesToProcess.Count > 0
            ? $"{_filesToProcess.Count} file(s) ready to convert."
            : "No supported images found.";

        BtnConvert.IsEnabled = _filesToProcess.Count > 0;
        ProgressBar.Value = 0;
        StatusText.Text = "Ready";
    }

    private async void BtnConvert_Click(object sender, RoutedEventArgs e)
    {
        if (_filesToProcess.Count == 0) return;

        // Handle Cancellation (Turning the Convert button into a Cancel button)
        if (_cts is not null)
        {
            _cts.Cancel();
            return;
        }

        _cts = new CancellationTokenSource();

        // Parse the selected format from the ComboBox
        var formatString = ((System.Windows.Controls.ComboBoxItem)CmbFormat.SelectedItem).Content.ToString();
        var targetFormat = Enum.Parse<OutputFormat>(formatString!);
        var options = new ConversionOptions(targetFormat, Quality: (int)SldQuality.Value);

        BtnConvert.Content = "Cancel";
        ProgressBar.Maximum = _filesToProcess.Count;
        ProgressBar.Value = 0;
        int successCount = 0;

        try
        {
            for (int i = 0; i < _filesToProcess.Count; i++)
            {
                if (_cts.Token.IsCancellationRequested) break;

                var file = _filesToProcess[i];
                var outputFilename = Path.ChangeExtension(file, targetFormat.ToString().ToLowerInvariant());

                StatusText.Text = $"Converting: {Path.GetFileName(file)}...";

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
            BtnConvert.IsEnabled = false;
            _filesToProcess.Clear();
        }
    }

    // --- Menu Event Handlers ---

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        BtnBrowse_Click(sender, e);
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Ultimate Image Converter (UIC)\n" +
            "Version 1.0.0\n\n" +
            "Author: Javier Martinez\n" +
            "License: BSD-3-Clause\n\n" +
            "An open-source tool built to quickly batch convert image formats including HEIC, AVIF, WebP, and more.",
            "About uic",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}