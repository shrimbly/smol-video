# Smol-Video - Registry Uninstaller
# This script removes the "Optimise video" context menu from video files

# Check if running as administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "This script requires administrator privileges. Please run as administrator." -ForegroundColor Red
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host "Smol-Video - Context Menu Uninstaller" -ForegroundColor Red
Write-Host "===========================================" -ForegroundColor Red
Write-Host ""

# Define video file extensions
$videoExtensions = @(
    ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v",
    ".mpg", ".mpeg", ".3gp", ".asf", ".rm", ".rmvb", ".ts", ".mts"
)

Write-Host "Removing context menu entries for video files..." -ForegroundColor Yellow
Write-Host ""

$successCount = 0
$notFoundCount = 0
$errorCount = 0

# Remove context menu for each video extension
foreach ($extension in $videoExtensions) {
    try {
        Write-Host "Removing $extension..." -NoNewline
        
        # Registry path for this extension
        $regPath = "Registry::HKEY_CLASSES_ROOT\SystemFileAssociations\$extension\shell\OptimiseVideo"
        
        # Check if registry entry exists
        if (Test-Path $regPath) {
            # Remove the entire OptimiseVideo key and its subkeys
            Remove-Item -Path $regPath -Recurse -Force
            Write-Host " OK" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host " NOT FOUND" -ForegroundColor Yellow
            $notFoundCount++
        }
    }
    catch {
        Write-Host " FAILED" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        $errorCount++
    }
}

Write-Host ""
Write-Host "Uninstallation Summary:" -ForegroundColor Cyan
Write-Host "  Removed: $successCount" -ForegroundColor Green
Write-Host "  Not Found: $notFoundCount" -ForegroundColor Yellow
Write-Host "  Failed: $errorCount" -ForegroundColor Red
Write-Host ""

if ($errorCount -eq 0) {
    if ($successCount -gt 0) {
        Write-Host "Context menu uninstallation completed successfully!" -ForegroundColor Green
        Write-Host "The 'Optimise video' option has been removed from video file context menus." -ForegroundColor White
    } else {
        Write-Host "No context menu entries were found to remove." -ForegroundColor Yellow
        Write-Host "The context menu may have already been uninstalled." -ForegroundColor White
    }
} else {
    Write-Host "Uninstallation completed with errors. Some entries may still remain." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 