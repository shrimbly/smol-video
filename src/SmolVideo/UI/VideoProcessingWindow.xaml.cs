using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SmolVideo.Models;
using SmolVideo.Services;

namespace SmolVideo.UI;

public partial class VideoProcessingWindow : Window
{
    private readonly FFmpegService _ffmpegService;
    private readonly EditingOptions _editingOptions;
    private readonly VideoMetadata _videoMetadata;
    private CancellationTokenSource? _cancellationTokenSource;
    private DateTime _startTime;

    public VideoProcessingWindow(FFmpegService ffmpegService, EditingOptions editingOptions, VideoMetadata videoMetadata)
    {
        InitializeComponent();
        
        _ffmpegService = ffmpegService;
        _editingOptions = editingOptions;
        _videoMetadata = videoMetadata;
        
        InitializeUI();
        StartProcessing();
    }

    private void InitializeUI()
    {
        InputFileText.Text = $"Input: {Path.GetFileName(_editingOptions.InputPath)}";
        OutputFileText.Text = $"Output: {Path.GetFileName(_editingOptions.OutputPath)}";
        
        // Build operations summary
        var operations = new List<string>();
        
        if (_editingOptions.HasTrimming)
        {
            operations.Add($"• Trimming: {_editingOptions.TrimStart:hh\\:mm\\:ss} - {_editingOptions.TrimEnd:hh\\:mm\\:ss}");
        }
        
        if (_editingOptions.HasCropping)
        {
            var crop = _editingOptions.Crop;
            operations.Add($"• Cropping: Top:{crop.Top}, Right:{crop.Right}, Bottom:{crop.Bottom}, Left:{crop.Left}");
        }
        
        if (_editingOptions.HasResizing)
        {
            var resize = _editingOptions.Resize;
            operations.Add($"• Resizing: {resize.Width} x {resize.Height}");
        }
        
        OperationsText.Text = operations.Count > 0 ? string.Join("\n", operations) : "• No operations specified";
    }

    private async void StartProcessing()
    {
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _startTime = DateTime.Now;
            
            // Subscribe to progress events
            _ffmpegService.ProgressChanged += OnProgressChanged;
            _ffmpegService.StatusChanged += OnStatusChanged;
            
            ProgressText.Text = "Starting video processing...";
            
            // Start the editing process
            var result = await ProcessVideoEditAsync(_editingOptions, _cancellationTokenSource.Token);
            
            if (result.Success)
            {
                OnProcessingCompleted(result);
            }
            else
            {
                OnProcessingFailed(result);
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText.Text = "Processing cancelled";
            Close();
        }
        catch (Exception ex)
        {
            OnProcessingFailed(ProcessResult.CreateError($"Unexpected error: {ex.Message}"));
        }
        finally
        {
            _ffmpegService.ProgressChanged -= OnProgressChanged;
            _ffmpegService.StatusChanged -= OnStatusChanged;
        }
    }

    private async Task<ProcessResult> ProcessVideoEditAsync(EditingOptions options, CancellationToken cancellationToken)
    {
        string? effectiveFfmpegPath = null;
        
        if (_ffmpegService.IsFFmpegAvailable())
        {
            effectiveFfmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ffmpeg", "ffmpeg.exe");
        }
        else
        {
            // Try to use system FFmpeg if bundled version not available
            var systemAvailable = await _ffmpegService.IsSystemFFmpegAvailableAsync();
            if (systemAvailable)
            {
                effectiveFfmpegPath = "ffmpeg";
            }
            else
            {
                return ProcessResult.CreateError("FFmpeg not found. Please ensure FFmpeg is bundled with the application or available in system PATH.");
            }
        }

        if (!File.Exists(options.InputPath))
        {
            return ProcessResult.CreateError($"Input file not found: {options.InputPath}");
        }

        try
        {
            var startTime = DateTime.Now;
            var inputFileInfo = new FileInfo(options.InputPath);
            var inputFileSize = inputFileInfo.Length;

            // Build FFmpeg command for editing
            var arguments = BuildEditingCommand(options);

            // Start FFmpeg process
            var processStartInfo = new ProcessStartInfo
            {
                FileName = effectiveFfmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(effectiveFfmpegPath) ?? Environment.CurrentDirectory
            };

            using var process = new Process { StartInfo = processStartInfo };
            
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    ParseProgress(e.Data, _videoMetadata.Duration);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var processingTime = DateTime.Now - startTime;
            var exitCode = process.ExitCode;

            if (exitCode == 0 && File.Exists(options.OutputPath))
            {
                var outputFileInfo = new FileInfo(options.OutputPath);
                var outputFileSize = outputFileInfo.Length;

                return ProcessResult.CreateSuccess(
                    options.OutputPath, 
                    processingTime, 
                    inputFileSize, 
                    outputFileSize
                );
            }
            else
            {
                var errorMessage = exitCode == 0 ? "Output file was not created" : $"FFmpeg process failed with exit code {exitCode}";
                return ProcessResult.CreateError(errorMessage, errorBuilder.ToString(), exitCode);
            }
        }
        catch (OperationCanceledException)
        {
            return ProcessResult.CreateError("Operation was cancelled by user");
        }
        catch (Exception ex)
        {
            return ProcessResult.CreateError($"Unexpected error: {ex.Message}", ex.ToString());
        }
    }

    private string BuildEditingCommand(EditingOptions options)
    {
        var command = $"-i \"{options.InputPath}\"";
        
        // Add trimming parameters
        if (options.HasTrimming)
        {
            command += $" -ss {options.TrimStart:hh\\:mm\\:ss\\.fff}";
            var duration = options.TrimEnd - options.TrimStart;
            command += $" -t {duration:hh\\:mm\\:ss\\.fff}";
        }
        
        // Build video filter chain
        var filters = new List<string>();
        
        // Add cropping filter
        if (options.HasCropping)
        {
            var crop = options.Crop;
            var cropWidth = _videoMetadata.Width - crop.Left - crop.Right;
            var cropHeight = _videoMetadata.Height - crop.Top - crop.Bottom;
            filters.Add($"crop={cropWidth}:{cropHeight}:{crop.Left}:{crop.Top}");
        }
        
        // Add scaling filter
        if (options.HasResizing)
        {
            var resize = options.Resize;
            filters.Add($"scale={resize.Width}:{resize.Height}");
        }
        
        // Apply video filters if any
        if (filters.Count > 0)
        {
            command += $" -vf \"{string.Join(",", filters)}\"";
        }
        
        // Add encoding settings
        command += $" -c:v libx264 -crf {options.CrfValue} -preset medium";
        command += " -c:a aac -b:a 128k";
        command += " -movflags +faststart";
        command += " -y"; // Overwrite output file
        
        command += $" \"{options.OutputPath}\"";
        
        return command;
    }

    private void ParseProgress(string ffmpegOutput, TimeSpan totalDuration)
    {
        // Parse FFmpeg progress from stderr output
        if (ffmpegOutput.Contains("time="))
        {
            var timeMatch = System.Text.RegularExpressions.Regex.Match(ffmpegOutput, @"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2})");
            if (timeMatch.Success)
            {
                var hours = int.Parse(timeMatch.Groups[1].Value);
                var minutes = int.Parse(timeMatch.Groups[2].Value);
                var seconds = int.Parse(timeMatch.Groups[3].Value);
                var centiseconds = int.Parse(timeMatch.Groups[4].Value);
                
                var currentTime = new TimeSpan(0, hours, minutes, seconds, centiseconds * 10);
                var percentage = (int)((currentTime.TotalMilliseconds / totalDuration.TotalMilliseconds) * 100);
                
                Dispatcher.Invoke(() =>
                {
                    MainProgressBar.Value = Math.Min(100, percentage);
                    ProgressText.Text = $"{percentage}% - Processing...";
                    
                    var elapsed = DateTime.Now - _startTime;
                    ElapsedTimeText.Text = elapsed.ToString(@"mm\:ss");
                    
                    // Calculate processing speed
                    if (elapsed.TotalSeconds > 0)
                    {
                        var speed = currentTime.TotalSeconds / elapsed.TotalSeconds;
                        SpeedText.Text = $"{speed:F1}x";
                    }
                });
            }
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

    private void OnStatusChanged(object? sender, string e)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressText.Text = e;
        });
    }

    private void OnProcessingCompleted(ProcessResult result)
    {
        Dispatcher.Invoke(() =>
        {
            MainProgressBar.Value = 100;
            ProgressText.Text = "Processing completed successfully!";
            
            CancelButton.Content = "Close";
            OpenFolderButton.Visibility = Visibility.Visible;
            
            var message = $"Video processing completed successfully!\n\n" +
                         $"Output file: {Path.GetFileName(result.OutputPath)}\n" +
                         $"Processing time: {result.ProcessingTime:mm\\:ss}\n" +
                         $"Original size: {FormatFileSize(result.InputFileSize)}\n" +
                         $"New size: {FormatFileSize(result.OutputFileSize)}\n" +
                         $"Size change: {CalculateSizeChange(result.InputFileSize, result.OutputFileSize)}";
            
            MessageBox.Show(message, "Processing Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private void OnProcessingFailed(ProcessResult result)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressText.Text = "Processing failed";
            CancelButton.Content = "Close";
            
            var message = $"Video processing failed:\n\n{result.Message}";
            MessageBox.Show(message, "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (CancelButton.Content.ToString() == "Cancel")
        {
            _cancellationTokenSource?.Cancel();
            ProgressText.Text = "Cancelling...";
        }
        else
        {
            Close();
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = Path.GetDirectoryName(_editingOptions.OutputPath);
            if (Directory.Exists(directory))
            {
                Process.Start("explorer.exe", directory);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open folder: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        
        return $"{number:n1} {suffixes[counter]}";
    }

    private static string CalculateSizeChange(long originalSize, long newSize)
    {
        if (originalSize == 0) return "N/A";
        
        var changePercent = ((double)(newSize - originalSize) / originalSize) * 100;
        var sign = changePercent >= 0 ? "+" : "";
        return $"{sign}{changePercent:F1}%";
    }
} 