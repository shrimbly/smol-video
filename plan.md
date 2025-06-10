# SmolVideo - Video Editing Features Development Plan

## Overview
This document outlines the development plan for extending SmolVideo to include basic video editing capabilities: trimming, cropping, and resizing. The application currently functions as a video optimization tool accessible through Windows context menu. We will extend it to allow independent operation with a video editing interface.

## Current Application Analysis

### Existing Architecture
- **Technology**: WPF (.NET) Windows application
- **Core Service**: `FFmpegService` - handles FFmpeg command execution and progress tracking
- **Current Models**: 
  - `OptimizationOptions` - configuration for video optimization
  - `ProcessResult` - result tracking for FFmpeg operations
- **Current UI**: `ProgressWindow` - shows optimization progress with cancel/open folder functionality
- **Entry Point**: Command-line driven, requires video file path as argument

### Current Limitations
1. Application only works when launched from context menu with file argument
2. No video preview capabilities
3. No timeline or scrubbing interface
4. Single-purpose optimization functionality
5. No manual file selection interface

## Proposed Features

### 1. Independent Application Launch
**Goal**: Allow application to run without command-line arguments
- **Current Behavior**: Shows installer dialog when no arguments provided
- **New Behavior**: Launch video editing interface when no arguments provided
- **Implementation**: Modify `Program.cs` main method to show editing window instead of installer

### 2. Video File Selection
**Goal**: Manual video file selection through standard Windows file dialog
- **Interface**: File picker button in main editing window
- **Validation**: Reuse existing `IsVideoFile()` method
- **Integration**: Set selected file as input for editing operations

### 3. Video Preview & Playback
**Goal**: Real-time video preview with playback controls
- **Technology**: WPF MediaElement or Windows Media Player control
- **Features**:
  - Play/pause functionality
  - Seek to specific timestamps
  - Volume control
  - Current time display
- **Integration**: Sync with timeline for trimming operations

### 4. Timeline Interface
**Goal**: Visual timeline with draggable trim handles
- **Components**:
  - Timeline track showing video duration
  - Start trim handle (left side)
  - End trim handle (right side)
  - Current playhead position
- **Interaction**:
  - Drag handles to set trim start/end points
  - Click on timeline to seek video
  - Visual feedback showing selected region
- **Data Binding**: Connect to trim start/end time properties

### 5. Trimming Functionality
**Goal**: Remove unwanted portions from beginning/end of video
- **Interface**: Draggable timeline handles
- **FFmpeg Command**: Use `-ss` (start time) and `-t` (duration) parameters
- **Validation**: Ensure start < end, minimum duration requirements
- **Preview**: Real-time update of trim region visualization

### 6. Cropping Functionality
**Goal**: Remove unwanted portions from video edges
- **Interface**: Input fields for top, right, bottom, left crop values
- **Preview**: Overlay rectangle on video preview showing crop area
- **FFmpeg Command**: Use `crop` filter with calculated width/height/x/y
- **Validation**: Ensure crop values don't exceed video dimensions
- **Auto-calculation**: Calculate resulting video dimensions

### 7. Resizing Functionality
**Goal**: Change video resolution while maintaining aspect ratio
- **Interface**: Width input field with auto-calculated height display
- **Logic**: Calculate height based on original aspect ratio
- **FFmpeg Command**: Use `scale` filter with calculated dimensions
- **Validation**: Minimum/maximum resolution limits
- **Non-divisible Support**: Use FFmpeg's scale filter options for non-even dimensions

## Technical Implementation Plan

### Phase 1: Application Structure Refactoring
1. **New Main Window**: `VideoEditorWindow.xaml` - main editing interface
2. **Modified Program.cs**: Launch editor window when no arguments provided
3. **Preserve Context Menu**: Keep existing optimization workflow intact
4. **New Models**: Create editing-specific data models

### Phase 2: Core UI Components
1. **File Selection Panel**:
   - File picker button
   - Selected file display
   - File information (duration, resolution, size)

2. **Video Preview Panel**:
   - MediaElement for video display
   - Playback controls (play/pause, seek)
   - Current time and duration display
   - Volume control

3. **Timeline Component**:
   - Custom UserControl for timeline visualization
   - Draggable handles for trim points
   - Progress indicator for current playback position
   - Time markers and grid

### Phase 3: Editing Controls
1. **Trim Controls**:
   - Start/end time input fields
   - Visual timeline with handles
   - Reset to full duration button

2. **Crop Controls**:
   - Input fields for top, right, bottom, left values
   - Crop preview overlay on video
   - Reset to original size button
   - Dimension validation

3. **Resize Controls**:
   - Width input field
   - Auto-calculated height display
   - Aspect ratio lock toggle
   - Common resolution presets

### Phase 4: FFmpeg Integration
1. **New Models**:
   ```csharp
   public class EditingOptions
   {
       public string InputPath { get; set; }
       public string OutputPath { get; set; }
       public TimeSpan TrimStart { get; set; }
       public TimeSpan TrimEnd { get; set; }
       public CropSettings Crop { get; set; }
       public ResizeSettings Resize { get; set; }
   }

   public class CropSettings
   {
       public int Top { get; set; }
       public int Right { get; set; }
       public int Bottom { get; set; }
       public int Left { get; set; }
   }

   public class ResizeSettings
   {
       public int Width { get; set; }
       public int Height { get; set; }
       public bool MaintainAspectRatio { get; set; } = true;
   }
   ```

2. **Enhanced FFmpegService**:
   - New method: `ProcessVideoEditAsync(EditingOptions options)`
   - Command builder for editing operations
   - Combined filter chain for multiple operations
   - Progress tracking for editing operations

3. **FFmpeg Command Structure**:
   ```bash
   ffmpeg -i input.mp4 -ss 00:00:10 -t 00:01:30 -vf "crop=w:h:x:y,scale=1920:1080" output.mp4
   ```

### Phase 5: User Experience Enhancements
1. **Real-time Preview**: Update preview when editing parameters change
2. **Undo/Redo**: Track editing state for undo functionality
3. **Presets**: Common editing presets (social media formats, etc.)
4. **Batch Operations**: Queue multiple videos for processing
5. **Export Options**: Different quality/format settings

## File Structure Changes

### New Files to Create:
```
src/SmolVideo/
├── UI/
│   ├── VideoEditorWindow.xaml
│   ├── VideoEditorWindow.xaml.cs
│   ├── Controls/
│   │   ├── VideoPreview.xaml
│   │   ├── VideoPreview.xaml.cs
│   │   ├── Timeline.xaml
│   │   ├── Timeline.xaml.cs
│   │   ├── TrimControls.xaml
│   │   ├── TrimControls.xaml.cs
│   │   ├── CropControls.xaml
│   │   ├── CropControls.xaml.cs
│   │   ├── ResizeControls.xaml
│   │   └── ResizeControls.xaml.cs
├── Models/
│   ├── EditingOptions.cs
│   ├── CropSettings.cs
│   ├── ResizeSettings.cs
│   └── VideoMetadata.cs
├── Services/
│   └── VideoMetadataService.cs
└── Converters/
    ├── TimeSpanToStringConverter.cs
    ├── BooleanToVisibilityConverter.cs
    └── DurationToPixelsConverter.cs
```

### Files to Modify:
- `Program.cs` - Add editing window launch logic
- `FFmpegService.cs` - Add editing operation support
- `SmolVideo.csproj` - Add any new dependencies

## Development Phases

### Phase 1 (Foundation) - Week 1
- Modify Program.cs for independent launch
- Create basic VideoEditorWindow with file selection
- Implement file picker and validation
- Basic layout and navigation structure

### Phase 2 (Video Preview) - Week 2
- Implement MediaElement video preview
- Add basic playback controls
- Integrate with selected video file
- Handle video loading and error states

### Phase 3 (Timeline) - Week 3
- Create custom Timeline control
- Implement draggable trim handles
- Connect timeline to video preview seeking
- Visual feedback for trim region

### Phase 4 (Editing Controls) - Week 4
- Implement trim functionality with FFmpeg integration
- Create crop controls with preview overlay
- Add resize controls with aspect ratio calculation
- Input validation and error handling

### Phase 5 (Integration & Polish) - Week 5
- Connect all editing features to FFmpeg processing
- Progress tracking for editing operations
- Export functionality with quality options
- Testing and bug fixes

### Phase 6 (Future Enhancements) - Week 6+
- Add "Edit Video" context menu option
- Batch processing capabilities
- Additional editing features (filters, effects)
- Performance optimizations

## Technical Considerations

### Performance
- Use MediaElement's position property for efficient seeking
- Implement lazy loading for video metadata
- Optimize timeline rendering for long videos
- Consider background processing for preview updates

### User Experience
- Provide visual feedback during all operations
- Implement proper error handling and user messages
- Ensure responsive UI during FFmpeg operations
- Add tooltips and help text for editing controls

### Compatibility
- Maintain compatibility with existing optimization workflow
- Ensure FFmpeg commands work across different video formats
- Handle edge cases (very short videos, unusual aspect ratios)
- Test with various video codecs and containers

### Quality Assurance
- Unit tests for FFmpeg command generation
- Integration tests for editing workflows
- Performance testing with large video files
- User acceptance testing for UI/UX

## Success Criteria

1. **Independent Launch**: Application launches successfully without command-line arguments
2. **Video Selection**: Users can select and load video files through file picker
3. **Preview Functionality**: Videos play correctly with full playback controls
4. **Trimming**: Users can visually trim videos using timeline handles
5. **Cropping**: Users can crop videos with real-time preview
6. **Resizing**: Users can resize videos with automatic aspect ratio calculation
7. **Export**: All editing operations produce correct FFmpeg commands and output files
8. **Context Menu Preservation**: Existing optimization workflow remains unaffected

## Risk Mitigation

### Technical Risks
- **MediaElement Limitations**: Fallback to alternative video preview solutions
- **FFmpeg Command Complexity**: Incremental testing of command building
- **Performance Issues**: Profiling and optimization strategies
- **Compatibility Problems**: Extensive testing with various video formats

### User Experience Risks
- **Complex Interface**: Iterative UI design with user feedback
- **Learning Curve**: Comprehensive tooltips and help documentation
- **Error Handling**: Graceful degradation and clear error messages

This plan provides a structured approach to implementing comprehensive video editing features while maintaining the existing application's functionality and extending its capabilities for independent use. 