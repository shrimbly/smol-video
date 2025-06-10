using System;
using System.IO;

namespace SmolVideo.Models;

public class EditingOptions
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public TimeSpan TrimStart { get; set; } = TimeSpan.Zero;
    public TimeSpan TrimEnd { get; set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public CropSettings Crop { get; set; } = new();
    public ResizeSettings Resize { get; set; } = new();
    public int CrfValue { get; set; } = 18; // Default CRF value for high quality
    public bool HasTrimming => TrimStart > TimeSpan.Zero || TrimEnd < Duration;
    public bool HasCropping => Crop.HasCropping;
    public bool HasResizing => Resize.HasResizing;

    public string GetOutputFileName()
    {
        var directory = Path.GetDirectoryName(InputPath) ?? string.Empty;
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(InputPath);
        var suffix = "_edited";
        var outputFileName = $"{nameWithoutExtension}{suffix}.mp4";
        
        return Path.Combine(directory, outputFileName);
    }

    public string GetUniqueOutputPath()
    {
        var basePath = GetOutputFileName();
        var counter = 1;
        var finalPath = basePath;

        while (File.Exists(finalPath))
        {
            var directory = Path.GetDirectoryName(basePath) ?? string.Empty;
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(basePath);
            finalPath = Path.Combine(directory, $"{nameWithoutExtension}_{counter}.mp4");
            counter++;
        }

        return finalPath;
    }
} 