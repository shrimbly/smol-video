using System;

namespace SmolVideo.Models;

public class VideoMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public int Width { get; set; } = 0;
    public int Height { get; set; } = 0;
    public string Format { get; set; } = string.Empty;
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public double FrameRate { get; set; } = 0;
    public long FileSize { get; set; } = 0;
    public string Bitrate { get; set; } = string.Empty;
    
    public string DurationFormatted => Duration.ToString(@"hh\:mm\:ss\.fff");
    public string FileSizeFormatted => FormatFileSize(FileSize);
    public string ResolutionText => $"{Width} x {Height}";
    public double AspectRatio => Height > 0 ? (double)Width / Height : 0;
    
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
} 