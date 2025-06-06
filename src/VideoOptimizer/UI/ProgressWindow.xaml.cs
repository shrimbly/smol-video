using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using VideoOptimizer.Models;
using VideoOptimizer.Services;

namespace VideoOptimizer.UI;

public partial class ProgressWindow : Window
{
    private readonly FFmpegService _ffmpegService;
    private readonly OptimizationOptions _options;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly DispatcherTimer _elapsedTimer;
    private DateTime _startTime;
    private bool _isCompleted;

    public ProcessResult? Result { get; private set; }

    public ProgressWindow(FFmpegService ffmpegService, OptimizationOptions options)
    {
        InitializeComponent();
        
        _ffmpegService = ffmpegService;
        _options = options;
        _cancellationTokenSource = new CancellationTokenSource();
        _isCompleted = false;

        // Setup UI
        FileNameText.Text = $"Input: {Path.GetFileName(options.InputPath)}";
        OutputPathText.Text = $"Output: {Path.GetFileName(options.GetOutputFileName())}";

        // Setup timer for elapsed time display
        _elapsedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _elapsedTimer.Tick += ElapsedTimer_Tick;

        // Subscribe to FFmpeg events
        _ffmpegService.ProgressChanged += OnProgressChanged;
        _ffmpegService.StatusChanged += OnStatusChanged;

        // Start optimization when window loads
        Loaded += ProgressWindow_Loaded;
        Closing += ProgressWindow_Closing;
    }

    private async void ProgressWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _startTime = DateTime.Now;
        _elapsedTimer.Start();

        try
        {
            Result = await _ffmpegService.OptimizeVideoAsync(_options, _cancellationTokenSource.Token);
            
            if (Result.Success)
            {
                OnOptimizationCompleted();
            }
            else
            {
                OnOptimizationFailed(Result);
            }
        }
        catch (OperationCanceledException)
        {
            Result = ProcessResult.CreateError("Operation was cancelled by user");
            OnOptimizationCancelled();
        }
        catch (Exception ex)
        {
            Result = ProcessResult.CreateError($"Unexpected error: {ex.Message}", ex.ToString());
            OnOptimizationFailed(Result);
        }
    }

    private void OnProgressChanged(object? sender, ProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            MainProgressBar.Value = e.Percentage;
            ProgressText.Text = e.Status;
        });
    }

    private void OnStatusChanged(object? sender, string status)
    {
        Dispatcher.Invoke(() =>
        {
            // Update speed if status contains speed information
            if (status.Contains("speed:") || status.Contains("Processing speed:"))
            {
                var speedPart = status.Split("speed:", StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (!string.IsNullOrEmpty(speedPart))
                {
                    SpeedText.Text = speedPart.Trim();
                }
            }
            
            // Update main status if it's a general status update
            if (!status.Contains("speed:") && !status.Contains("Processing speed:"))
            {
                ProgressText.Text = status;
            }
        });
    }

    private void OnOptimizationCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            _isCompleted = true;
            _elapsedTimer.Stop();

            MainProgressBar.Value = 100;
            ProgressText.Text = "Optimization completed successfully!";

            // Show completion information
            if (Result != null)
            {
                var inputSize = Result.FormatFileSize(Result.InputFileSize);
                var outputSize = Result.FormatFileSize(Result.OutputFileSize);
                var compression = Result.GetCompressionPercentage();
                
                ProgressText.Text = $"Completed! Size: {inputSize} → {outputSize} ({compression} reduction)";
            }

            // Update buttons
            CancelButton.Content = "Close";
            OpenFolderButton.Visibility = Visibility.Visible;
        });
    }

    private void OnOptimizationFailed(ProcessResult result)
    {
        Dispatcher.Invoke(() =>
        {
            _isCompleted = true;
            _elapsedTimer.Stop();

            ProgressText.Text = $"Error: {result.Message}";
            CancelButton.Content = "Close";

            // Show error dialog with details
            var errorWindow = new ErrorDialog(result.Message, result.ErrorDetails);
            errorWindow.Owner = this;
            errorWindow.ShowDialog();
        });
    }

    private void OnOptimizationCancelled()
    {
        Dispatcher.Invoke(() =>
        {
            _isCompleted = true;
            _elapsedTimer.Stop();
            ProgressText.Text = "Operation cancelled";
            CancelButton.Content = "Close";
        });
    }

    private void ElapsedTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _startTime;
        ElapsedTimeText.Text = elapsed.ToString(@"mm\:ss");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCompleted)
        {
            DialogResult = Result?.Success == true;
            Close();
        }
        else
        {
            // Cancel the operation
            CancelButton.IsEnabled = false;
            CancelButton.Content = "Cancelling...";
            
            _cancellationTokenSource.Cancel();
            _ffmpegService.CancelOperation();
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (Result?.Success == true && !string.IsNullOrEmpty(Result.OutputPath))
        {
            try
            {
                var directory = Path.GetDirectoryName(Result.OutputPath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    Process.Start("explorer.exe", $"/select,\"{Result.OutputPath}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open folder: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ProgressWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Cleanup
        _elapsedTimer.Stop();
        _ffmpegService.ProgressChanged -= OnProgressChanged;
        _ffmpegService.StatusChanged -= OnStatusChanged;
        
        if (!_isCompleted)
        {
            _cancellationTokenSource.Cancel();
            _ffmpegService.CancelOperation();
        }
        
        _cancellationTokenSource.Dispose();
    }
} 