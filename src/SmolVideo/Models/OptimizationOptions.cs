using System.IO;

namespace SmolVideo.Models;

public class OptimizationOptions
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public int CrfValue { get; set; } = 18;
    public string VideoCodec { get; set; } = "libx264";
    public string AudioCodec { get; set; } = "aac";
    public string AudioBitrate { get; set; } = "128k";
    public string Preset { get; set; } = "medium";
    public bool AddFastStart { get; set; } = true;
    public bool OverwriteExisting { get; set; } = false;

    public string GetOutputFileName()
    {
        var directory = Path.GetDirectoryName(InputPath) ?? string.Empty;
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(InputPath);
        var originalExtension = Path.GetExtension(InputPath).ToLowerInvariant();
        
        // If input is already MP4, add "_optimized" suffix
        var suffix = originalExtension == ".mp4" ? "_optimized" : string.Empty;
        var outputFileName = $"{nameWithoutExtension}{suffix}.mp4";
        
        return Path.Combine(directory, outputFileName);
    }

    public string GetUniqueOutputPath()
    {
        var basePath = GetOutputFileName();
        var counter = 1;
        var finalPath = basePath;

        while (File.Exists(finalPath) && !OverwriteExisting)
        {
            var directory = Path.GetDirectoryName(basePath) ?? string.Empty;
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(basePath);
            finalPath = Path.Combine(directory, $"{nameWithoutExtension}_{counter}.mp4");
            counter++;
        }

        return finalPath;
    }
} 