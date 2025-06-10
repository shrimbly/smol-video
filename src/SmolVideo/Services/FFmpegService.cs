using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using SmolVideo.Models;

namespace SmolVideo.Services;

public class FFmpegService
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private Process? _currentProcess;

    public event EventHandler<ProgressEventArgs>? ProgressChanged;
    public event EventHandler<string>? StatusChanged;

    public FFmpegService()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _ffmpegPath = Path.Combine(appDirectory, "Resources", "ffmpeg", "ffmpeg.exe");
        _ffprobePath = Path.Combine(appDirectory, "Resources", "ffmpeg", "ffprobe.exe");
    }

    public bool IsFFmpegAvailable()
    {
        return File.Exists(_ffmpegPath) && File.Exists(_ffprobePath);
    }

    public async Task<bool> IsSystemFFmpegAvailableAsync()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> GetSystemFFmpegPathAsync()
    {
        var systemPaths = new[] { "ffmpeg", "ffmpeg.exe" };
        
        foreach (var ffmpegName in systemPaths)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegName,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    return ffmpegName;
                }
            }
            catch
            {
                // Continue to next path
            }
        }
        
        return null;
    }

    public async Task<ProcessResult> OptimizeVideoAsync(OptimizationOptions options, CancellationToken cancellationToken = default)
    {
        string? effectiveFfmpegPath = null;
        
        if (IsFFmpegAvailable())
        {
            effectiveFfmpegPath = _ffmpegPath;
        }
        else
        {
            // Try to use system FFmpeg if bundled version not available
            effectiveFfmpegPath = await GetSystemFFmpegPathAsync();
            if (effectiveFfmpegPath == null)
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
            // Set status for hardware acceleration
            if (options.UseHardwareAcceleration)
            {
                StatusChanged?.Invoke(this, "Using hardware acceleration (auto-detect)");
            }
            else
            {
                StatusChanged?.Invoke(this, "Using software encoding only");
            }
            
            // Get video duration for progress calculation
            var duration = await GetVideoDurationAsync(options.InputPath);
            
            // Prepare output path
            options.OutputPath = options.GetUniqueOutputPath();
            
            // Get input file size
            var inputFileInfo = new FileInfo(options.InputPath);
            var inputFileSize = inputFileInfo.Length;

            var startTime = DateTime.Now;
            StatusChanged?.Invoke(this, "Starting video optimization...");

            // Build FFmpeg command
            var arguments = BuildFFmpegCommand(options);

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

            _currentProcess = new Process { StartInfo = processStartInfo };
            
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            _currentProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            _currentProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    ParseProgress(e.Data, duration);
                }
            };

            _currentProcess.Start();
            _currentProcess.BeginOutputReadLine();
            _currentProcess.BeginErrorReadLine();

            await _currentProcess.WaitForExitAsync(cancellationToken);

            var processingTime = DateTime.Now - startTime;
            var exitCode = _currentProcess.ExitCode;

            if (exitCode == 0 && File.Exists(options.OutputPath))
            {
                var outputFileInfo = new FileInfo(options.OutputPath);
                var outputFileSize = outputFileInfo.Length;

                StatusChanged?.Invoke(this, "Optimization completed successfully!");
                
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
            _currentProcess?.Kill(true);
            return ProcessResult.CreateError("Operation was cancelled by user");
        }
        catch (Exception ex)
        {
            return ProcessResult.CreateError($"Unexpected error: {ex.Message}", ex.ToString());
        }
        finally
        {
            _currentProcess?.Dispose();
            _currentProcess = null;
        }
    }

    public void CancelOperation()
    {
        if (_currentProcess != null && !_currentProcess.HasExited)
        {
            try
            {
                _currentProcess.Kill(true);
            }
            catch (Exception ex)
            {
                // Log error but don't throw
                System.Diagnostics.Debug.WriteLine($"Error cancelling process: {ex.Message}");
            }
        }
    }

    private string BuildFFmpegCommand(OptimizationOptions options)
    {
        var command = "";
        
        // Add hardware acceleration if enabled
        if (options.UseHardwareAcceleration)
        {
            command += "-hwaccel auto ";
        }
        
        command += $"-i \"{options.InputPath}\"";
        
        // Video codec settings - use libx264 with hardware acceleration
        command += $" -c:v {options.VideoCodec}";
        
        // Add web safety options
        command += $" -profile:v {options.VideoProfile}";
        command += $" -pix_fmt {options.PixelFormat}";
        
        // Quality settings
        command += $" -crf {options.CrfValue}";
        command += $" -preset {options.Preset}";
        
        // Audio settings
        command += $" -c:a {options.AudioCodec}";
        command += $" -b:a {options.AudioBitrate}";
        
        // Web safety: Add faststart for web compatibility
        if (options.AddFastStart)
        {
            command += " -movflags +faststart";
        }

        if (options.OverwriteExisting)
        {
            command += " -y";
        }

        command += $" \"{options.OutputPath}\"";

        return command;
    }

    private async Task<string?> GetSystemFFprobePathAsync()
    {
        var systemPaths = new[] { "ffprobe", "ffprobe.exe" };
        
        foreach (var ffprobeName in systemPaths)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffprobeName,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    return ffprobeName;
                }
            }
            catch
            {
                // Continue to next path
            }
        }
        
        return null;
    }

    private async Task<TimeSpan> GetVideoDurationAsync(string videoPath)
    {
        string? effectiveFfprobePath = null;
        
        if (File.Exists(_ffprobePath))
        {
            effectiveFfprobePath = _ffprobePath;
        }
        else
        {
            // Try to use system FFprobe if bundled version not available
            effectiveFfprobePath = await GetSystemFFprobePathAsync();
            if (effectiveFfprobePath == null)
            {
                System.Diagnostics.Debug.WriteLine("FFprobe not found in bundled location or system PATH");
                return TimeSpan.Zero;
            }
        }

        try
        {
            var arguments = $"-v quiet -show_entries format=duration -of csv=p=0 \"{videoPath}\"";
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = effectiveFfprobePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (double.TryParse(output.Trim(), out var seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting video duration: {ex.Message}");
        }

        return TimeSpan.Zero;
    }

    private void ParseProgress(string ffmpegOutput, TimeSpan totalDuration)
    {
        try
        {
            // Parse time progress from FFmpeg output
            var timeMatch = Regex.Match(ffmpegOutput, @"time=(\d+):(\d+):(\d+\.\d+)");
            if (timeMatch.Success)
            {
                var hours = int.Parse(timeMatch.Groups[1].Value);
                var minutes = int.Parse(timeMatch.Groups[2].Value);
                var seconds = double.Parse(timeMatch.Groups[3].Value);
                
                var currentTime = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
                
                if (totalDuration.TotalSeconds > 0)
                {
                    var progressPercentage = (int)((currentTime.TotalSeconds / totalDuration.TotalSeconds) * 100);
                    progressPercentage = Math.Min(progressPercentage, 100);
                    
                    var remainingTime = totalDuration - currentTime;
                    var status = $"Processing... {progressPercentage}% - ETA: {remainingTime:mm\\:ss}";
                    
                    ProgressChanged?.Invoke(this, new ProgressEventArgs(progressPercentage, status));
                    StatusChanged?.Invoke(this, status);
                }
            }

            // Parse speed information
            var speedMatch = Regex.Match(ffmpegOutput, @"speed=\s*(\d+\.?\d*)x");
            if (speedMatch.Success)
            {
                var speed = double.Parse(speedMatch.Groups[1].Value);
                var speedStatus = $"Processing speed: {speed:F1}x";
                StatusChanged?.Invoke(this, speedStatus);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing FFmpeg progress: {ex.Message}");
        }
    }
}

public class ProgressEventArgs : EventArgs
{
    public int Percentage { get; }
    public string Status { get; }

    public ProgressEventArgs(int percentage, string status)
    {
        Percentage = percentage;
        Status = status;
    }
} 