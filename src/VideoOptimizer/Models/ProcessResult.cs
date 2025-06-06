namespace VideoOptimizer.Models;

public class ProcessResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public TimeSpan ProcessingTime { get; set; }
    public long InputFileSize { get; set; }
    public long OutputFileSize { get; set; }
    public int ExitCode { get; set; }

    public static ProcessResult CreateSuccess(string outputPath, TimeSpan processingTime, long inputSize, long outputSize)
    {
        return new ProcessResult
        {
            Success = true,
            Message = "Video optimization completed successfully",
            OutputPath = outputPath,
            ProcessingTime = processingTime,
            InputFileSize = inputSize,
            OutputFileSize = outputSize,
            ExitCode = 0
        };
    }

    public static ProcessResult CreateError(string message, string errorDetails = "", int exitCode = -1)
    {
        return new ProcessResult
        {
            Success = false,
            Message = message,
            ErrorDetails = errorDetails,
            ExitCode = exitCode
        };
    }

    public double GetCompressionRatio()
    {
        if (InputFileSize == 0) return 0;
        return (double)OutputFileSize / InputFileSize;
    }

    public string GetCompressionPercentage()
    {
        var ratio = GetCompressionRatio();
        var percentage = (1 - ratio) * 100;
        return $"{percentage:F1}%";
    }

    public string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:F2} {sizes[order]}";
    }
} 