# Video Optimizer

A professional Windows application that adds a context menu option to optimize video files using FFmpeg. Simply right-click on any video file and select "Optimise video" to compress it with high quality settings.

## Features

- **Context Menu Integration**: Right-click on video files in Windows Explorer to optimize them
- **High Quality Compression**: Uses FFmpeg with CRF 18 for excellent quality-to-size ratio
- **Real-time Progress**: Professional progress window with live updates
- **Multiple Formats**: Supports MP4, AVI, MKV, MOV, WMV, FLV, WEBM, M4V, and more
- **Smart Output Naming**: Automatically handles file naming conflicts
- **Error Handling**: Detailed error dialogs with actionable information
- **Self-contained**: Includes FFmpeg - no additional dependencies required

## Installation

### Option 1: Professional Installer (Recommended)
1. Download the latest `VideoOptimizer-Setup-v1.0.0.exe` from releases
2. Run the installer as administrator
3. Follow the installation wizard
4. The context menu will be automatically registered

### Option 2: Manual Installation
1. Extract the application files to `C:\Program Files\VideoOptimizer\`
2. Copy FFmpeg binaries to `Resources\ffmpeg\` folder
3. Run `install\install.ps1` as administrator to register context menu

## Usage

1. **Right-click** on any video file in Windows Explorer
2. Select **"Optimise video"** from the context menu
3. Watch the progress window as your video is optimized
4. The optimized video will be saved in the same folder

### Optimization Settings

- **Video Codec**: H.264 (libx264)
- **Quality**: CRF 18 (near-lossless quality)
- **Audio Codec**: AAC at 128kbps
- **Output Format**: MP4 with fast start for web streaming

## Supported File Types

- **MP4** - MPEG-4 Video
- **AVI** - Audio Video Interleave
- **MKV** - Matroska Video
- **MOV** - QuickTime Movie
- **WMV** - Windows Media Video
- **FLV** - Flash Video
- **WEBM** - WebM Video
- **M4V** - iTunes Video
- **MPG/MPEG** - MPEG Video
- **3GP** - 3GPP Mobile Video

## Build Instructions

### Prerequisites
- Visual Studio 2022 or .NET 8.0 SDK
- Windows 10/11 (x64)
- PowerShell 5.1 or later
- Inno Setup (for installer creation)

### Building the Application
```powershell
# Clone the repository
git clone https://github.com/videooptimizer/videooptimizer.git
cd videooptimizer

# Build the application
dotnet publish src/VideoOptimizer/VideoOptimizer.csproj -c Release -r win-x64 --self-contained true

# Download FFmpeg (place ffmpeg.exe and ffprobe.exe in src/VideoOptimizer/Resources/ffmpeg/)

# Create installer (requires Inno Setup)
iscc install/setup.iss
```

### FFmpeg Integration
The application requires FFmpeg binaries. You can:
1. Download from [FFmpeg.org](https://ffmpeg.org/download.html)
2. Place `ffmpeg.exe` and `ffprobe.exe` in `src/VideoOptimizer/Resources/ffmpeg/`
3. These will be included in the published application

## Uninstallation

### Using Windows Settings
1. Go to Settings > Apps
2. Find "Video Optimizer" in the list
3. Click "Uninstall"

### Manual Uninstallation
1. Run `install\uninstall.ps1` as administrator
2. Delete the application folder

## Troubleshooting

### Context Menu Not Appearing
- Ensure you ran the installer as administrator
- Try manually running `install\install.ps1` as administrator
- Check that the application path in registry is correct

### FFmpeg Errors
- Verify FFmpeg binaries are in the `Resources\ffmpeg\` folder
- Check if the video file is corrupted or in an unsupported format
- Ensure sufficient disk space for the output file

### Permission Errors
- Run the application as administrator
- Check that you have write permissions to the video file's directory
- Ensure the input file is not currently being used by another application

## Technical Details

### System Requirements
- **OS**: Windows 10 version 1809 or later (x64)
- **RAM**: 512 MB minimum, 2 GB recommended
- **Disk Space**: 50 MB for application + space for video processing
- **Processor**: x64 compatible processor

### Architecture
- **Frontend**: WPF (Windows Presentation Foundation)
- **Backend**: .NET 8.0 with self-contained deployment
- **Video Processing**: FFmpeg integration with progress parsing
- **Registry Integration**: PowerShell scripts for context menu management

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Support

If you encounter any issues or have suggestions:
- Open an issue on GitHub
- Check the troubleshooting section above
- Ensure you're using the latest version

## Acknowledgments

- FFmpeg team for the excellent video processing library
- Microsoft for the .NET platform and WPF framework
- Inno Setup for the professional installer framework 