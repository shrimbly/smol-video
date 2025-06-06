# Video Optimizer - Build Script
# Automates building, publishing, and creating installer

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipFFmpeg,
    [switch]$CreateInstaller,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "Video Optimizer Build Script" -ForegroundColor Green
Write-Host "============================" -ForegroundColor Green
Write-Host ""

# Set paths
$RootDir = $PSScriptRoot
$ProjectDir = Join-Path $RootDir "src\VideoOptimizer"
$PublishDir = Join-Path $ProjectDir "bin\$Configuration\net8.0-windows\$Runtime\publish"
$ResourcesDir = Join-Path $PublishDir "Resources"
$FFmpegDir = Join-Path $ResourcesDir "ffmpeg"
$DistDir = Join-Path $RootDir "dist"

# Create dist directory if it doesn't exist
if (-not (Test-Path $DistDir)) {
    New-Item -Path $DistDir -ItemType Directory -Force | Out-Null
}

# Clean previous builds if requested
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    
    $CleanPaths = @(
        (Join-Path $ProjectDir "bin"),
        (Join-Path $ProjectDir "obj"),
        $DistDir
    )
    
    foreach ($path in $CleanPaths) {
        if (Test-Path $path) {
            Remove-Item -Path $path -Recurse -Force
            Write-Host "  Cleaned: $path" -ForegroundColor Gray
        }
    }
    Write-Host "Clean completed." -ForegroundColor Green
    Write-Host ""
}

# Check .NET SDK
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "  .NET SDK Version: $dotnetVersion" -ForegroundColor Gray
} catch {
    Write-Host "  Error: .NET SDK not found. Please install .NET 8.0 SDK." -ForegroundColor Red
    exit 1
}

# Restore packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $ProjectDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Error: Package restore failed." -ForegroundColor Red
    exit 1
}
Write-Host "  Packages restored successfully." -ForegroundColor Green

# Build and publish
Write-Host "Building and publishing application..." -ForegroundColor Yellow
$PublishCommand = @(
    "publish", $ProjectDir,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "--verbosity", "minimal"
)

dotnet @PublishCommand
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Error: Build failed." -ForegroundColor Red
    exit 1
}
Write-Host "  Build completed successfully." -ForegroundColor Green

# Download FFmpeg if not skipping
if (-not $SkipFFmpeg) {
    Write-Host "Setting up FFmpeg..." -ForegroundColor Yellow
    
    # Create FFmpeg directory
    if (-not (Test-Path $FFmpegDir)) {
        New-Item -Path $FFmpegDir -ItemType Directory -Force | Out-Null
    }
    
    $FFmpegExe = Join-Path $FFmpegDir "ffmpeg.exe"
    $FFprobeExe = Join-Path $FFmpegDir "ffprobe.exe"
    
    if (-not (Test-Path $FFmpegExe) -or -not (Test-Path $FFprobeExe)) {
        Write-Host "  Downloading FFmpeg binaries..." -ForegroundColor Yellow
        
        try {
            # Download FFmpeg from GitHub releases (reliable source)
            $FFmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
            $FFmpegZip = Join-Path $RootDir "ffmpeg-download.zip"
            $FFmpegTemp = Join-Path $RootDir "ffmpeg-temp"
            
            Write-Host "    Downloading from GitHub..." -ForegroundColor Gray
            Invoke-WebRequest -Uri $FFmpegUrl -OutFile $FFmpegZip -UseBasicParsing
            
            Write-Host "    Extracting archive..." -ForegroundColor Gray
            Expand-Archive -Path $FFmpegZip -DestinationPath $FFmpegTemp -Force
            
            # Find the ffmpeg binaries in the extracted folder
            $ExtractedBinDir = Get-ChildItem $FFmpegTemp -Recurse -Directory -Name "bin" | Select-Object -First 1
            $FFmpegSource = Join-Path $FFmpegTemp $ExtractedBinDir
            
            if (Test-Path (Join-Path $FFmpegSource "ffmpeg.exe")) {
                Copy-Item (Join-Path $FFmpegSource "ffmpeg.exe") $FFmpegExe -Force
                Copy-Item (Join-Path $FFmpegSource "ffprobe.exe") $FFprobeExe -Force
                Write-Host "  FFmpeg binaries downloaded and installed successfully." -ForegroundColor Green
            } else {
                throw "FFmpeg binaries not found in downloaded archive"
            }
            
            # Cleanup
            Remove-Item $FFmpegZip -Force -ErrorAction SilentlyContinue
            Remove-Item $FFmpegTemp -Recurse -Force -ErrorAction SilentlyContinue
            
        } catch {
            Write-Host "  Warning: Automatic FFmpeg download failed: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host "  Please download FFmpeg manually:" -ForegroundColor Yellow
            Write-Host "    1. Download from: https://github.com/BtbN/FFmpeg-Builds/releases/latest" -ForegroundColor Gray
            Write-Host "    2. Extract ffmpeg.exe and ffprobe.exe to: $FFmpegDir" -ForegroundColor Gray
            
            if (-not $CreateInstaller) {
                exit 1
            }
        }
    } else {
        Write-Host "  FFmpeg binaries found." -ForegroundColor Green
    }
}

# Copy installation scripts
Write-Host "Copying installation scripts..." -ForegroundColor Yellow
$InstallScripts = @("install.ps1", "uninstall.ps1")
foreach ($script in $InstallScripts) {
    $SourcePath = Join-Path $RootDir "install\$script"
    $DestPath = Join-Path $PublishDir $script
    if (Test-Path $SourcePath) {
        Copy-Item -Path $SourcePath -Destination $DestPath -Force
        Write-Host "  Copied: $script" -ForegroundColor Gray
    }
}

# Display build information
Write-Host ""
Write-Host "Build Summary:" -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
Write-Host "  Runtime: $Runtime" -ForegroundColor Gray
Write-Host "  Output Directory: $PublishDir" -ForegroundColor Gray

$ExePath = Join-Path $PublishDir "VideoOptimizer.exe"
if (Test-Path $ExePath) {
    $FileInfo = Get-Item $ExePath
    $FileSize = [math]::Round($FileInfo.Length / 1MB, 2)
    Write-Host "  Executable Size: $FileSize MB" -ForegroundColor Gray
}

# Create installer if requested
if ($CreateInstaller) {
    Write-Host ""
    Write-Host "Creating installer..." -ForegroundColor Yellow
    
    $InnoSetupScript = Join-Path $RootDir "install\setup.iss"
    
    # Check if Inno Setup is installed
    $InnoSetupPath = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    
    if ($InnoSetupPath) {
        Write-Host "  Using Inno Setup: $InnoSetupPath" -ForegroundColor Gray
        
        & $InnoSetupPath $InnoSetupScript
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  Installer created successfully." -ForegroundColor Green
            
            $InstallerPath = Join-Path $DistDir "VideoOptimizer-Setup-v1.0.0.exe"
            if (Test-Path $InstallerPath) {
                $InstallerInfo = Get-Item $InstallerPath
                $InstallerSize = [math]::Round($InstallerInfo.Length / 1MB, 2)
                Write-Host "  Installer Size: $InstallerSize MB" -ForegroundColor Gray
                Write-Host "  Installer Location: $InstallerPath" -ForegroundColor Gray
            }
        } else {
            Write-Host "  Warning: Installer creation failed." -ForegroundColor Yellow
        }
    } else {
        Write-Host "  Warning: Inno Setup not found. Skipping installer creation." -ForegroundColor Yellow
        Write-Host "  Install Inno Setup from: https://jrsoftware.org/isinfo.php" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Test the application: $ExePath" -ForegroundColor Gray
Write-Host "  2. Install context menu: Run install\install.ps1 as administrator" -ForegroundColor Gray
if ($CreateInstaller -and (Test-Path (Join-Path $DistDir "VideoOptimizer-Setup-v1.0.0.exe"))) {
    Write-Host "  3. Distribute installer: $DistDir\VideoOptimizer-Setup-v1.0.0.exe" -ForegroundColor Gray
}