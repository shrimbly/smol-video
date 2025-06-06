# Smol-Video - Build Instructions

## Prerequisites

### Required Software
- **Visual Studio 2022** or **Visual Studio Code** with C# extension
- **.NET 8.0 SDK** - Download from [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- **PowerShell 7+** (for build scripts)
- **Inno Setup** (for creating Windows installer) - Download from [https://jrsoftware.org/isinfo.php](https://jrsoftware.org/isinfo.php)

### FFmpeg Binaries
The application requires FFmpeg binaries. You have two options:

#### Option 1: Download and Place Manually
1. Download FFmpeg from [https://ffmpeg.org/download.html](https://ffmpeg.org/download.html)
2. Extract `ffmpeg.exe` and `ffprobe.exe`
3. Place them in: `src/SmolVideo/Resources/ffmpeg/`

#### Option 2: Auto-download during build
The build script can automatically download FFmpeg binaries for you.

## Building the Application

### Method 1: Using Build Script (Recommended)
```powershell
# Run the automated build script
.\build.ps1
```

This script will:
- Restore NuGet packages
- Download FFmpeg binaries if missing
- Build the application in Release mode
- Create a published, self-contained executable
- Copy necessary files to the output directory

### Method 2: Manual Build
```powershell
# Navigate to the project directory
cd src/SmolVideo

# Restore dependencies
dotnet restore

# Build the project
dotnet build --configuration Release

# Publish self-contained executable
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output bin/Release/net8.0-windows/win-x64/publish
```

## Build Outputs

After building, you'll find the following in `src/SmolVideo/bin/Release/net8.0-windows/win-x64/publish/`:

- `SmolVideo.exe` - Main application executable
- `Resources/ffmpeg/` - FFmpeg binaries
- `LICENSE` - License file
- `README.md` - User documentation
- Various .NET runtime DLLs

## Creating Windows Installer

### Prerequisites for Installer
- **Inno Setup 6.0+** installed
- Completed build (using steps above)

### Build Installer
```powershell
# Option 1: Use the build script with installer flag
.\build.ps1 -CreateInstaller

# Option 2: Manual installer creation
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" install\setup.iss
```

The installer will be created in the `dist/` directory as `SmolVideo-Setup-v1.0.0.exe`.

## Project Structure

```
optimise-video/
├── src/
│   └── SmolVideo/
│       ├── Models/              # Data models
│       ├── Services/            # Business logic
│       ├── UI/                  # WPF user interface
│       ├── Resources/           # Icons, FFmpeg binaries
│       ├── Program.cs           # Application entry point
│       └── SmolVideo.csproj
├── install/                     # Installer configuration
│   ├── setup.iss               # Inno Setup script
│   ├── install.ps1             # Context menu registration
│   └── uninstall.ps1           # Context menu removal
├── build.ps1                   # Build automation script
├── BUILD.md                    # This file
└── README.md                   # User documentation
```

## Development Workflow

### Setting up Development Environment
1. Clone the repository
2. Open `SmolVideo.sln` in Visual Studio
3. Ensure .NET 8.0 SDK is installed
4. Build the solution (F6 in Visual Studio)

### Running in Development
- Set `SmolVideo` as the startup project
- Press F5 to debug or Ctrl+F5 to run without debugging

### Testing
```powershell
# Run unit tests (if available)
dotnet test

# Test the built application
.\src\SmolVideo\bin\Release\net8.0-windows\win-x64\publish\SmolVideo.exe
```

## Troubleshooting

### Common Issues

**Build fails with missing .NET SDK**
- Install .NET 8.0 SDK from Microsoft's website
- Verify installation: `dotnet --version`

**FFmpeg not found errors**
- Ensure `ffmpeg.exe` and `ffprobe.exe` are in `Resources/ffmpeg/`
- Check file permissions and antivirus software

**Installer creation fails**
- Verify Inno Setup is installed
- Check that all required files exist in the publish directory
- Run PowerShell as Administrator if needed

**Context menu not working after install**
- Run the installer as Administrator
- Manually run: `install\install.ps1` as Administrator

### Clean Build
```powershell
# Remove all build artifacts
Remove-Item -Recurse -Force src\SmolVideo\bin, src\SmolVideo\obj
dotnet clean
```

## Distribution

### For End Users
- Distribute the installer: `dist/SmolVideo-Setup-v1.0.0.exe`
- Users run the installer with Administrator privileges
- The installer handles all dependencies and context menu registration

### For Developers
- Share the repository URL
- Follow the build instructions above
- Ensure all prerequisites are installed

## Build Script Parameters

The `build.ps1` script supports several parameters:

```powershell
# Build only (default)
.\build.ps1

# Build and create installer
.\build.ps1 -CreateInstaller

# Clean build (removes artifacts first)
.\build.ps1 -Clean

# Verbose output
.\build.ps1 -Verbose
```

## Version Management

Update version numbers in:
- `src/SmolVideo/SmolVideo.csproj` (AssemblyVersion, FileVersion)
- `install/setup.iss` (MyAppVersion)
- `README.md` (if version is mentioned) 