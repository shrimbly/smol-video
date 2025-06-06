# Video Optimizer - Registry Installer
# This script registers the "Optimise video" context menu for video files

param(
    [string]$InstallPath = "C:\Program Files\VideoOptimizer"
)

# Check if running as administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "This script requires administrator privileges. Please run as administrator." -ForegroundColor Red
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "Video Optimizer - Context Menu Installer" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""

# Define video file extensions
$videoExtensions = @(
    ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v",
    ".mpg", ".mpeg", ".3gp", ".asf", ".rm", ".rmvb", ".ts", ".mts"
)

# Application executable path
$appPath = Join-Path $InstallPath "VideoOptimizer.exe"

# Verify application exists
if (-not (Test-Path $appPath)) {
    Write-Host "Error: VideoOptimizer.exe not found at: $appPath" -ForegroundColor Red
    Write-Host "Please ensure the application is properly installed." -ForegroundColor Red
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "Installing context menu entries for video files..." -ForegroundColor Yellow
Write-Host "Application path: $appPath" -ForegroundColor Gray
Write-Host ""

$successCount = 0
$errorCount = 0

# Register context menu for each video extension
foreach ($extension in $videoExtensions) {
    try {
        Write-Host "Registering $extension..." -NoNewline
        
        # Registry path for this extension
        $regPath = "Registry::HKEY_CLASSES_ROOT\SystemFileAssociations\$extension\shell\OptimiseVideo"
        $commandPath = "$regPath\command"
        
        # Create the registry entries
        if (-not (Test-Path $regPath)) {
            New-Item -Path $regPath -Force | Out-Null
        }
        
        if (-not (Test-Path $commandPath)) {
            New-Item -Path $commandPath -Force | Out-Null
        }
        
        # Set the display name
        Set-ItemProperty -Path $regPath -Name "(Default)" -Value "Optimise video" -Force
        
        # Set the icon (using the application's icon)
        Set-ItemProperty -Path $regPath -Name "Icon" -Value "`"$appPath`",0" -Force
        
        # Set the command
        Set-ItemProperty -Path $commandPath -Name "(Default)" -Value "`"$appPath`" `"%1`"" -Force
        
        Write-Host " OK" -ForegroundColor Green
        $successCount++
    }
    catch {
        Write-Host " FAILED" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        $errorCount++
    }
}

Write-Host ""
Write-Host "Installation Summary:" -ForegroundColor Cyan
Write-Host "  Successful: $successCount" -ForegroundColor Green
Write-Host "  Failed: $errorCount" -ForegroundColor Red
Write-Host ""

if ($errorCount -eq 0) {
    Write-Host "Context menu installation completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now right-click on video files and select 'Optimise video'." -ForegroundColor White
} else {
    Write-Host "Installation completed with errors. Some video types may not have the context menu." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 