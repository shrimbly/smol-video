using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SmolVideo.Models;

namespace SmolVideo.Services;

public class VideoMetadataService
{
    private readonly string _ffprobePath;

    public VideoMetadataService()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _ffprobePath = Path.Combine(appDirectory, "Resources", "ffmpeg", "ffprobe.exe");
    }

    public bool IsFFprobeAvailable()
    {
        return File.Exists(_ffprobePath);
    }

    public async Task<bool> IsSystemFFprobeAvailableAsync()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
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

    public async Task<VideoMetadata?> GetVideoMetadataAsync(string videoPath)
    {
        if (!File.Exists(videoPath))
            return null;

        if (!IsFFprobeAvailable())
        {
            // Try to use system FFprobe if bundled version not available
            return await TrySystemFFprobeAsync(videoPath);
        }

        return await ExtractMetadataAsync(_ffprobePath, videoPath);
    }

    private async Task<VideoMetadata?> TrySystemFFprobeAsync(string videoPath)
    {
        // Try to find FFprobe in system PATH
        var systemPaths = new[] { "ffprobe", "ffprobe.exe" };
        
        foreach (var ffprobeName in systemPaths)
        {
            try
            {
                var result = await ExtractMetadataAsync(ffprobeName, videoPath);
                if (result != null)
                    return result;
            }
            catch
            {
                // Continue to next path
            }
        }
        
        return null;
    }

    private async Task<VideoMetadata?> ExtractMetadataAsync(string ffprobePath, string videoPath)
    {
        try
        {
            var arguments = $"-v quiet -print_format json -show_format -show_streams \"{videoPath}\"";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var errorOutput = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                System.Diagnostics.Debug.WriteLine($"FFprobe failed with exit code {process.ExitCode}");
                System.Diagnostics.Debug.WriteLine($"Error: {errorOutput}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                System.Diagnostics.Debug.WriteLine("FFprobe returned empty output");
                return null;
            }

            return ParseMetadata(videoPath, output);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception in ExtractMetadataAsync: {ex.Message}");
            return null;
        }
    }

    private VideoMetadata? ParseMetadata(string videoPath, string jsonOutput)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonOutput);
            var root = document.RootElement;

            var metadata = new VideoMetadata
            {
                FilePath = videoPath,
                FileSize = new FileInfo(videoPath).Length
            };

            // Parse format information
            if (root.TryGetProperty("format", out var format))
            {
                if (format.TryGetProperty("duration", out var duration) && 
                    double.TryParse(duration.GetString(), out var durationSeconds))
                {
                    metadata.Duration = TimeSpan.FromSeconds(durationSeconds);
                }

                if (format.TryGetProperty("format_name", out var formatName))
                {
                    metadata.Format = formatName.GetString() ?? "";
                }

                if (format.TryGetProperty("bit_rate", out var bitRate))
                {
                    metadata.Bitrate = bitRate.GetString() ?? "";
                }
            }

            // Parse video stream information
            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (!stream.TryGetProperty("codec_type", out var codecType))
                        continue;

                    var type = codecType.GetString();
                    
                    if (type == "video")
                    {
                        if (stream.TryGetProperty("width", out var width))
                            metadata.Width = width.GetInt32();

                        if (stream.TryGetProperty("height", out var height))
                            metadata.Height = height.GetInt32();

                        if (stream.TryGetProperty("codec_name", out var videoCodec))
                            metadata.VideoCodec = videoCodec.GetString() ?? "";

                        if (stream.TryGetProperty("r_frame_rate", out var frameRate))
                        {
                            var frameRateStr = frameRate.GetString() ?? "";
                            if (frameRateStr.Contains('/'))
                            {
                                var parts = frameRateStr.Split('/');
                                if (parts.Length == 2 && 
                                    double.TryParse(parts[0], out var numerator) && 
                                    double.TryParse(parts[1], out var denominator) && 
                                    denominator != 0)
                                {
                                    metadata.FrameRate = numerator / denominator;
                                }
                            }
                        }
                    }
                    else if (type == "audio")
                    {
                        if (stream.TryGetProperty("codec_name", out var audioCodec))
                            metadata.AudioCodec = audioCodec.GetString() ?? "";
                    }
                }
            }

            return metadata;
        }
        catch
        {
            return null;
        }
    }
} 