using System.IO;
using System.Windows;
using SmolVideo.Models;
using SmolVideo.Services;
using SmolVideo.UI;
using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Principal;

namespace SmolVideo;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Initialize WPF application
            var app = new Application();
            
            // Handle command line arguments
            if (args.Length == 0)
            {
                // Launch video editor when no arguments provided
                var editorWindow = new VideoEditorWindow();
                app.Run(editorWindow);
                return;
            }

            // Check for special arguments
            if (args.Length == 1 && args[0].Equals("--install", StringComparison.OrdinalIgnoreCase))
            {
                PerformInstallation();
                return;
            }
            
            if (args.Length == 1 && args[0].Equals("--setup", StringComparison.OrdinalIgnoreCase))
            {
                ShowInstallerDialog();
                return;
            }

            var videoPath = args[0];
            
            // Validate input file
            if (!File.Exists(videoPath))
            {
                ShowErrorDialog("File Not Found", $"The specified video file does not exist:\n{videoPath}");
                return;
            }

            // Check if it's a video file (basic extension check)
            if (!IsVideoFile(videoPath))
            {
                ShowErrorDialog("Invalid File Type", 
                    "The selected file does not appear to be a video file.\n\n" +
                    "Supported formats: MP4, AVI, MKV, MOV, WMV, FLV, WEBM, M4V");
                return;
            }

            // Initialize services
            var ffmpegService = new FFmpegService();
            
            // Check if FFmpeg is available
            if (!ffmpegService.IsFFmpegAvailable())
            {
                // Check if system FFmpeg is available as fallback
                var systemAvailable = Task.Run(async () => await ffmpegService.IsSystemFFmpegAvailableAsync()).Result;
                if (!systemAvailable)
                {
                    ShowErrorDialog("FFmpeg Not Found", 
                        "FFmpeg is required but was not found in the application directory or system PATH.\n\n" +
                        "Please either:\n" +
                        "• Ensure FFmpeg is properly bundled with the application, or\n" +
                        "• Install FFmpeg and add it to your system PATH");
                    return;
                }
            }

            // Create optimization options
            var options = new OptimizationOptions
            {
                InputPath = videoPath
            };

            // Show progress window and start optimization
            var progressWindow = new ProgressWindow(ffmpegService, options);
            app.Run(progressWindow);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Unexpected Error", 
                $"An unexpected error occurred:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
        }
    }

    private static bool IsVideoFile(string filePath)
    {
        var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v",
            ".mpg", ".mpeg", ".3gp", ".asf", ".rm", ".rmvb", ".ts", ".mts"
        };

        var extension = Path.GetExtension(filePath);
        return videoExtensions.Contains(extension);
    }

    private static void ShowInstallerDialog()
    {
        var currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
        var currentDir = Path.GetDirectoryName(currentExe) ?? "";
        var targetDir = @"C:\Program Files\SmolVideo";
        
        var message = "Smol-Video - Self Installer\n\n" +
                     "This will install Smol-Video with the following features:\n" +
                     "• Copy application to Program Files\n" +
                     "• Install FFmpeg dependencies\n" +
                     "• Register right-click context menu for video files\n\n" +
                     $"Current location: {currentDir}\n" +
                     $"Install location: {targetDir}\n\n" +
                     "Would you like to install Smol-Video?\n\n" +
                     "Note: Administrator privileges are required.";

        var result = MessageBox.Show(message, "Smol-Video - Install", 
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            if (IsRunningAsAdmin())
            {
                PerformInstallation();
            }
            else
            {
                // Restart as administrator
                RestartAsAdmin("--install");
            }
        }
    }

    private static void PerformInstallation()
    {
        try
        {
            var currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var currentDir = Path.GetDirectoryName(currentExe) ?? "";
            var targetDir = @"C:\Program Files\SmolVideo";

            // Create target directory
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, true);
            }
            Directory.CreateDirectory(targetDir);

            // Copy all files from current directory
            CopyDirectory(currentDir, targetDir);

            // Register context menu
            RegisterContextMenu(targetDir);

            var successMessage = "Smol-Video has been installed successfully!\n\n" +
                               "Installation completed:\n" +
                               $"• Application installed to: {targetDir}\n" +
                               "• FFmpeg dependencies included\n" +
                               "• Context menu registered for video files\n\n" +
                               "You can now right-click on video files and select 'Optimise video'.";

            MessageBox.Show(successMessage, "Installation Complete", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Installation failed:\n\n{ex.Message}\n\n" +
                             "Please ensure you have administrator privileges and try again.";
            
            MessageBox.Show(errorMessage, "Installation Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }

        // Copy all subdirectories recursively
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectory(subDir, targetSubDir);
        }
    }

    private static void RegisterContextMenu(string installPath)
    {
        var appPath = Path.Combine(installPath, "SmolVideo.exe");
        var videoExtensions = new[] {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v",
            ".mpg", ".mpeg", ".3gp", ".asf", ".rm", ".rmvb", ".ts", ".mts"
        };

        foreach (var extension in videoExtensions)
        {
            try
            {
                // Get the ProgID for this extension
                using var extKey = Registry.ClassesRoot.OpenSubKey(extension);
                var progId = extKey?.GetValue("") as string;

                if (string.IsNullOrEmpty(progId))
                {
                    progId = extension.Substring(1) + "file";
                }

                // Create shell command key
                var shellKeyPath = $@"{progId}\shell\SmolVideo";
                using var shellKey = Registry.ClassesRoot.CreateSubKey(shellKeyPath);
                shellKey.SetValue("", "Optimise video");
                shellKey.SetValue("Icon", appPath);

                // Create command key
                var commandKeyPath = $@"{progId}\shell\SmolVideo\command";
                using var commandKey = Registry.ClassesRoot.CreateSubKey(commandKeyPath);
                commandKey.SetValue("", $"\"{appPath}\" \"%1\"");
            }
            catch (Exception ex)
            {
                // Log error but continue with other extensions
                System.Diagnostics.Debug.WriteLine($"Failed to register context menu for {extension}: {ex.Message}");
            }
        }
    }

    private static bool IsRunningAsAdmin()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RestartAsAdmin(string arguments)
    {
        try
        {
            // For single-file applications, use Environment.ProcessPath instead of Assembly.Location
            var currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            
            if (string.IsNullOrEmpty(currentExe))
            {
                MessageBox.Show("Could not determine the current executable path.", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            var processInfo = new ProcessStartInfo
            {
                FileName = currentExe,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas" // This triggers the UAC prompt
            };

            Process.Start(processInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to restart as administrator:\n\n{ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void ShowUsageDialog()
    {
        var message = "Smol-Video\n\n" +
                     "This application is designed to be used from the Windows context menu.\n\n" +
                     "To use:\n" +
                     "1. Right-click on a video file in Windows Explorer\n" +
                     "2. Select 'Optimise video' from the context menu\n\n" +
                     "The video will be optimized using FFmpeg with CRF 18 quality settings.";

        MessageBox.Show(message, "Smol-Video", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static void ShowErrorDialog(string title, string message)
    {
        MessageBox.Show(message, $"Smol-Video - {title}", MessageBoxButton.OK, MessageBoxImage.Error);
    }
} 