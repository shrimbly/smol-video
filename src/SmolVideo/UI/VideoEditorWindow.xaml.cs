using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using SmolVideo.Models;
using SmolVideo.Services;

namespace SmolVideo.UI;

public partial class VideoEditorWindow : Window
{
    private readonly VideoMetadataService _metadataService;
    private readonly FFmpegService _ffmpegService;
    private VideoMetadata? _currentVideo;
    private EditingOptions _editingOptions;
    private DispatcherTimer _playbackTimer;
    private bool _isPlaying = false;
    private bool _isDraggingStartHandle = false;
    private bool _isDraggingEndHandle = false;
    private double _timelineWidth = 0;
    private bool _isInitialized = false;

    public VideoEditorWindow()
    {
        InitializeComponent();
        
        _metadataService = new VideoMetadataService();
        _ffmpegService = new FFmpegService();
        _editingOptions = new EditingOptions();
        
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _playbackTimer.Tick += PlaybackTimer_Tick;
        
        InitializeTimeline();
        _isInitialized = true;
    }

    private void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Video File",
            Filter = "Video Files (*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.m4v)|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.m4v|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadVideoFile(openFileDialog.FileName);
        }
    }

    private async void LoadVideoFile(string filePath)
    {
        try
        {
            StatusText.Text = "Loading video...";
            
            // Load video metadata
            _currentVideo = await _metadataService.GetVideoMetadataAsync(filePath);
            
            if (_currentVideo == null)
            {
                var bundledAvailable = _metadataService.IsFFprobeAvailable();
                var systemAvailable = await _metadataService.IsSystemFFprobeAvailableAsync();
                
                var message = "Failed to load video metadata. This could be due to:\n\n" +
                             "• FFprobe/FFmpeg not installed or not found in system PATH\n" +
                             "• Unsupported video format or codec\n" +
                             "• Corrupted video file\n\n" +
                             "Please ensure you have FFmpeg installed or try a different video file.\n\n" +
                             $"Bundled FFprobe available: {bundledAvailable}\n" +
                             $"System FFprobe available: {systemAvailable}";
                
                MessageBox.Show(message, "Error Loading Video", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Update UI
            SelectedFileText.Text = System.IO.Path.GetFileName(filePath);
            UpdateVideoInformation();
            
            // Load video in player
            VideoPlayer.Source = new Uri(filePath);
            VideoPlayer.LoadedBehavior = MediaState.Manual;
            VideoPlayer.UnloadedBehavior = MediaState.Manual;
            
            // Initialize editing options
            _editingOptions.InputPath = filePath;
            _editingOptions.Duration = _currentVideo.Duration;
            _editingOptions.TrimStart = TimeSpan.Zero;
            _editingOptions.TrimEnd = _currentVideo.Duration;
            
            // Reset editing controls
            ResetAllControls();
            
            // Enable controls
            PlayPauseButton.IsEnabled = true;
            StopButton.IsEnabled = true;
            ProcessButton.IsEnabled = true;
            
            NoVideoPlaceholder.Visibility = Visibility.Collapsed;
            StatusText.Text = "Video loaded successfully";
            
            // Force timeline update after layout is complete
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshTimelineLayout();
            }), DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading video: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Failed to load video";
        }
    }

    private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (_currentVideo != null)
        {
            DurationText.Text = _currentVideo.DurationFormatted;
            
            // Update timeline after media is loaded
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshTimelineLayout();
                
                // Set initial playhead position
                VideoPlayer.Position = TimeSpan.Zero;
                UpdatePlayhead();
            }), DispatcherPriority.Background);
        }
    }

    private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        MessageBox.Show($"Failed to load video: {e.ErrorException?.Message}", "Error", 
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            VideoPlayer.Pause();
            _playbackTimer.Stop();
            PlayPauseButton.Content = "▶";
            _isPlaying = false;
        }
        else
        {
            VideoPlayer.Play();
            _playbackTimer.Start();
            PlayPauseButton.Content = "⏸";
            _isPlaying = true;
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Stop();
        _playbackTimer.Stop();
        PlayPauseButton.Content = "▶";
        _isPlaying = false;
        UpdatePlayhead();
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
        {
            CurrentTimeText.Text = VideoPlayer.Position.ToString(@"hh\:mm\:ss");
            
            // Check if we need to refresh timeline width
            var currentCanvasWidth = TimelineCanvas.ActualWidth;
            if (Math.Abs(currentCanvasWidth - _timelineWidth) > 1.0) // If width changed significantly
            {
                System.Diagnostics.Debug.WriteLine($"Timeline width mismatch detected: Canvas={currentCanvasWidth:F1}, Stored={_timelineWidth:F1}");
                _timelineWidth = currentCanvasWidth;
            }
            
            UpdatePlayhead();
        }
    }

    private void InitializeTimeline()
    {
        TimelineCanvas.SizeChanged += (s, e) =>
        {
            var newWidth = e.NewSize.Width;
            System.Diagnostics.Debug.WriteLine($"Timeline SizeChanged: Old={_timelineWidth:F1}, New={newWidth:F1}");
            
            if (newWidth > 0 && Math.Abs(newWidth - _timelineWidth) > 1.0)
            {
                _timelineWidth = newWidth;
                System.Diagnostics.Debug.WriteLine($"Timeline width updated in SizeChanged to: {_timelineWidth:F1}");
                
                if (_currentVideo != null)
                {
                    UpdateTimelineLayout();
                }
            }
        };
        
        // Force initial layout update
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (TimelineCanvas.ActualWidth > 0)
            {
                _timelineWidth = TimelineCanvas.ActualWidth;
                if (_currentVideo != null)
                {
                    UpdateTimelineLayout();
                }
            }
        }), DispatcherPriority.Loaded);
    }

    private void RefreshTimelineLayout()
    {
        // Ensure we have the latest canvas dimensions
        TimelineCanvas.UpdateLayout();
        var actualWidth = TimelineCanvas.ActualWidth;
        
        System.Diagnostics.Debug.WriteLine($"RefreshTimelineLayout: Canvas.ActualWidth={actualWidth:F1}, Current _timelineWidth={_timelineWidth:F1}");
        
        if (actualWidth > 0)
        {
            _timelineWidth = actualWidth;
            System.Diagnostics.Debug.WriteLine($"Timeline width updated to: {_timelineWidth:F1}");
            UpdateTimelineLayout();
        }
        else
        {
            // Try to force measure and arrange
            TimelineCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            TimelineCanvas.Arrange(new Rect(TimelineCanvas.DesiredSize));
            actualWidth = TimelineCanvas.ActualWidth;
            
            System.Diagnostics.Debug.WriteLine($"After force layout: Canvas.ActualWidth={actualWidth:F1}");
            
            if (actualWidth > 0)
            {
                _timelineWidth = actualWidth;
                UpdateTimelineLayout();
            }
        }
    }

    private void UpdateTimelineLayout()
    {
        if (_currentVideo == null || _timelineWidth <= 0 || _currentVideo.Duration.TotalMilliseconds <= 0) return;

        // Update timeline track background to fill the entire canvas width
        TimelineTrack.Width = _timelineWidth;
        TimelineBackground.Width = _timelineWidth;
        
        // Calculate positions as proportions of the timeline width
        var totalDuration = _currentVideo.Duration.TotalMilliseconds;
        var startRatio = _editingOptions.TrimStart.TotalMilliseconds / totalDuration;
        var endRatio = _editingOptions.TrimEnd.TotalMilliseconds / totalDuration;
        
        var startPos = startRatio * _timelineWidth;
        var endPos = endRatio * _timelineWidth;
        
        // Ensure positions are valid and within bounds
        startPos = Math.Max(0, Math.Min(startPos, _timelineWidth));
        endPos = Math.Max(startPos, Math.Min(endPos, _timelineWidth));
        
        // Update handles (center them on their positions) - now using 20px wide handles
        Canvas.SetLeft(StartHandle, Math.Max(0, startPos - 10));
        Canvas.SetLeft(EndHandle, Math.Max(0, Math.Min(_timelineWidth - 20, endPos - 10)));
        
        // Update selected region
        var regionWidth = Math.Max(0, endPos - startPos);
        SelectedRegion.Width = regionWidth;
        Canvas.SetLeft(SelectedRegion, startPos);
        
        UpdateTimeMarkers();
        UpdatePlayhead();
    }

    private void UpdateTimeMarkers()
    {
        if (_currentVideo == null || _timelineWidth <= 0) return;

        // Clear existing markers
        TimeMarkers.Children.Clear();

        var duration = _currentVideo.Duration.TotalSeconds;
        var pixelsPerSecond = _timelineWidth / duration;
        
        // Determine marker interval based on timeline width and duration
        var markerInterval = 1.0; // Start with 1 second
        var labelInterval = 5.0;  // Default label interval
        
        if (pixelsPerSecond < 5)
        {
            markerInterval = 60.0;      // 1 minute markers
            labelInterval = 60.0;       // Label every minute
        }
        else if (pixelsPerSecond < 15)
        {
            markerInterval = 30.0;      // 30 second markers
            labelInterval = 30.0;       // Label every 30 seconds
        }
        else if (pixelsPerSecond < 30)
        {
            markerInterval = 10.0;      // 10 second markers
            labelInterval = 30.0;       // Label every 30 seconds
        }
        else if (pixelsPerSecond < 60)
        {
            markerInterval = 5.0;       // 5 second markers
            labelInterval = 15.0;       // Label every 15 seconds
        }
        else
        {
            markerInterval = 1.0;       // 1 second markers
            labelInterval = 5.0;        // Label every 5 seconds
        }

        // Add markers
        for (var time = 0.0; time <= duration; time += markerInterval)
        {
            var x = (time / duration) * _timelineWidth;
            
            // Determine if this is a major or minor marker
            var isMajorMarker = time % labelInterval == 0;
            
            // Create marker line
            var marker = new Line
            {
                X1 = 0,
                X2 = 0,
                Y1 = 0,
                Y2 = isMajorMarker ? 12 : 6, // Taller for major markers
                Stroke = new SolidColorBrush(Color.FromRgb(189, 195, 199)), // #BDC3C7 - more visible
                StrokeThickness = isMajorMarker ? 1.5 : 1,
                Opacity = isMajorMarker ? 0.9 : 0.6
            };
            
            Canvas.SetLeft(marker, x);
            TimeMarkers.Children.Add(marker);
            
            // Add time label for major markers
            if (isMajorMarker)
            {
                var timeSpan = TimeSpan.FromSeconds(time);
                var label = new TextBlock
                {
                    Text = timeSpan.ToString(timeSpan.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(236, 240, 241)), // #ECF0F1 - lighter for better visibility
                    FontFamily = new FontFamily("Segoe UI"),
                    FontWeight = FontWeights.Medium,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                
                Canvas.SetLeft(label, x - 20); // Center the label better
                Canvas.SetTop(label, -18);
                TimeMarkers.Children.Add(label);
            }
        }
    }

    private void ScrubToPosition(TimeSpan position)
    {
        if (_currentVideo == null || VideoPlayer.Source == null) return;

        // Clamp position to video bounds with millisecond precision
        var clampedPosition = TimeSpan.FromMilliseconds(
            Math.Max(0, Math.Min(position.TotalMilliseconds, _currentVideo.Duration.TotalMilliseconds)));

        try
        {
            // Stop playback and timer during scrubbing
            if (_isPlaying)
            {
                VideoPlayer.Pause();
                _playbackTimer.Stop();
                _isPlaying = false;
                PlayPauseButton.Content = "▶";
            }

            // Enhanced frame-accurate scrubbing for millisecond precision
            PerformFrameAccurateScrub(clampedPosition);
            
            // Update UI with millisecond precision
            CurrentTimeText.Text = clampedPosition.ToString(@"hh\:mm\:ss\.fff");
            
            System.Diagnostics.Debug.WriteLine($"Scrubbing to: {clampedPosition.TotalMilliseconds:F1}ms");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during scrubbing: {ex.Message}");
        }
    }

    private void PerformFrameAccurateScrub(TimeSpan targetPosition)
    {
        // Multi-step approach for frame-accurate seeking
        
        // Step 1: Stop and clear any buffered frames
        VideoPlayer.Stop();
        
        // Step 2: Seek to a slightly earlier position to ensure we can step forward
        var seekBackMs = Math.Max(0, targetPosition.TotalMilliseconds - 100);
        var earlyPosition = TimeSpan.FromMilliseconds(seekBackMs);
        VideoPlayer.Position = earlyPosition;
        
        // Step 3: Use a timer sequence for precise frame stepping
        var stepTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(5) // Very short interval for responsiveness
        };
        
        stepTimer.Tick += (s, e) =>
        {
            stepTimer.Stop();
            
            // Start playing from the early position
            VideoPlayer.Play();
            
            // Step 4: Let it play briefly to buffer frames, then seek to exact position
            var precisionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20) // Allow some frames to buffer
            };
            
            precisionTimer.Tick += (s2, e2) =>
            {
                precisionTimer.Stop();
                
                // Seek to exact target position while playing
                VideoPlayer.Position = targetPosition;
                
                // Step 5: Very brief play to render the exact frame
                var renderTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16) // ~1 frame at 60fps
                };
                
                renderTimer.Tick += (s3, e3) =>
                {
                    renderTimer.Stop();
                    
                    // Final pause to display the frame
                    VideoPlayer.Pause();
                    
                    // Verify and correct position if needed (with tight tolerance)
                    var actualPosition = VideoPlayer.Position;
                    var tolerance = 33; // ~1 frame tolerance at 30fps
                    
                    if (Math.Abs((actualPosition - targetPosition).TotalMilliseconds) > tolerance)
                    {
                        // Try one more precise seek
                        VideoPlayer.Position = targetPosition;
                        System.Diagnostics.Debug.WriteLine($"Position corrected: Target={targetPosition.TotalMilliseconds:F1}ms, Was={actualPosition.TotalMilliseconds:F1}ms");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Frame-accurate scrub: Target={targetPosition.TotalMilliseconds:F1}ms, Final={VideoPlayer.Position.TotalMilliseconds:F1}ms, Diff={Math.Abs((VideoPlayer.Position - targetPosition).TotalMilliseconds):F1}ms");
                };
                
                renderTimer.Start();
            };
            
            precisionTimer.Start();
        };
        
        stepTimer.Start();
    }

    private void UpdatePlayhead()
    {
        if (_currentVideo == null || _timelineWidth <= 0 || _currentVideo.Duration.TotalMilliseconds <= 0) return;

        // Get current position, ensuring it doesn't exceed video duration
        var currentPosition = VideoPlayer.Position.TotalMilliseconds;
        var videoDuration = _currentVideo.Duration.TotalMilliseconds;
        
        // Clamp position to video duration bounds
        currentPosition = Math.Max(0, Math.Min(currentPosition, videoDuration));
        
        // Calculate playhead position as a proportion of the timeline width
        var positionRatio = currentPosition / videoDuration;
        var currentPos = positionRatio * _timelineWidth;
        
        // Ensure position is valid and within timeline bounds
        if (double.IsNaN(currentPos) || double.IsInfinity(currentPos))
            currentPos = 0;
            
        // Final bounds check - ensure playhead stays within timeline
        currentPos = Math.Max(0, Math.Min(currentPos, _timelineWidth - 2)); // -2 to keep line visible
        
        // Update playhead visual position - position both the container and line
        Canvas.SetLeft(PlayheadContainer, currentPos - 2); // Center the 4px wide container
        Canvas.SetLeft(Playhead, currentPos);
        // X1 and X2 should remain 0 for a vertical line - don't change them
        Playhead.X1 = 0;
        Playhead.X2 = 0;
        
        // Update current time display with high precision
        var timeSpan = TimeSpan.FromMilliseconds(currentPosition);
        CurrentTimeText.Text = timeSpan.ToString(@"hh\:mm\:ss\.fff");
    }

    private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentVideo == null || _timelineWidth <= 0 || _currentVideo.Duration.TotalMilliseconds <= 0) return;

        var position = e.GetPosition(TimelineCanvas);
        
        // High-precision time calculation with sub-pixel accuracy
        var timePosition = (position.X / _timelineWidth) * _currentVideo.Duration.TotalMilliseconds;
        
        // Ensure we don't get NaN or invalid values
        if (double.IsNaN(timePosition) || double.IsInfinity(timePosition) || timePosition < 0)
            return;
            
        // Maintain millisecond precision without rounding
        var seekTime = TimeSpan.FromMilliseconds(Math.Min(timePosition, _currentVideo.Duration.TotalMilliseconds));
        
        System.Diagnostics.Debug.WriteLine($"Timeline click: X={position.X:F2}, TimelineWidth={_timelineWidth:F2}, Time={seekTime.TotalMilliseconds:F1}ms");
        
        // Use simplified direct seeking for timeline clicks (more reliable for single clicks)
        PerformDirectSeek(seekTime);
        UpdatePlayhead();
    }

    private void PerformDirectSeek(TimeSpan targetPosition)
    {
        try
        {
            // Stop any current playback
            if (_isPlaying)
            {
                VideoPlayer.Pause();
                _playbackTimer.Stop();
                _isPlaying = false;
                PlayPauseButton.Content = "▶";
            }

            // Simple, single play/pause cycle approach
            VideoPlayer.Position = targetPosition;
            
            // Wait a moment for the seek to register
            var seekTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            
            seekTimer.Tick += (s, e) =>
            {
                seekTimer.Stop();
                
                // Single play/pause cycle - no retries, no nested timers
                VideoPlayer.Play();
                
                var pauseTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200) // Longer single duration
                };
                
                pauseTimer.Tick += (s2, e2) =>
                {
                    pauseTimer.Stop();
                    VideoPlayer.Pause();
                    
                    // Update UI with current position (whatever it ended up at)
                    var currentPos = VideoPlayer.Position;
                    CurrentTimeText.Text = currentPos.ToString(@"hh\:mm\:ss\.fff");
                    
                    System.Diagnostics.Debug.WriteLine($"Direct seek complete: Target={targetPosition.TotalMilliseconds:F1}ms, Final={currentPos.TotalMilliseconds:F1}ms");
                };
                
                pauseTimer.Start();
            };
            
            seekTimer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in direct seek: {ex.Message}");
        }
    }

    private void StartHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingStartHandle = true;
        ((Border)sender).CaptureMouse();
        e.Handled = true;
    }

    private void EndHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingEndHandle = true;
        ((Border)sender).CaptureMouse();
        e.Handled = true;
    }

    private void Handle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingStartHandle = false;
        _isDraggingEndHandle = false;
        ((Border)sender).ReleaseMouseCapture();
    }

    private void StartHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingStartHandle || _currentVideo == null || _timelineWidth <= 0) return;

        var position = e.GetPosition(TimelineCanvas);
        
        // High-precision calculation for millisecond-level trim accuracy
        var timeMs = (position.X / _timelineWidth) * _currentVideo.Duration.TotalMilliseconds;
        
        // Ensure we don't get NaN or invalid values
        if (double.IsNaN(timeMs) || double.IsInfinity(timeMs))
            return;
            
        var newTime = TimeSpan.FromMilliseconds(Math.Max(0, Math.Min(timeMs, _editingOptions.TrimEnd.TotalMilliseconds - 100))); // Reduced minimum gap to 100ms
        
        _editingOptions.TrimStart = newTime;
        TrimStartTextBox.Text = newTime.ToString(@"hh\:mm\:ss\.fff"); // Show milliseconds
        
        // Frame-accurate video scrubbing - seek to the handle position
        ScrubToPosition(newTime);
        
        UpdateTimelineLayout();
    }

    private void EndHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingEndHandle || _currentVideo == null || _timelineWidth <= 0) return;

        var position = e.GetPosition(TimelineCanvas);
        
        // High-precision calculation for millisecond-level trim accuracy
        var timeMs = (position.X / _timelineWidth) * _currentVideo.Duration.TotalMilliseconds;
        
        // Ensure we don't get NaN or invalid values
        if (double.IsNaN(timeMs) || double.IsInfinity(timeMs))
            return;
            
        var newTime = TimeSpan.FromMilliseconds(Math.Min(_currentVideo.Duration.TotalMilliseconds, Math.Max(timeMs, _editingOptions.TrimStart.TotalMilliseconds + 100))); // Reduced minimum gap to 100ms
        
        _editingOptions.TrimEnd = newTime;
        TrimEndTextBox.Text = newTime.ToString(@"hh\:mm\:ss\.fff"); // Show milliseconds
        
        // Frame-accurate video scrubbing - seek to the handle position
        ScrubToPosition(newTime);
        
        UpdateTimelineLayout();
    }

    private void TrimTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || _currentVideo == null) return;

        var textBox = (TextBox)sender;
        if (TimeSpan.TryParse(textBox.Text, out var time))
        {
            if (textBox == TrimStartTextBox)
            {
                _editingOptions.TrimStart = TimeSpan.FromMilliseconds(Math.Max(0, Math.Min(time.TotalMilliseconds, _editingOptions.TrimEnd.TotalMilliseconds - 1000)));
            }
            else if (textBox == TrimEndTextBox)
            {
                _editingOptions.TrimEnd = TimeSpan.FromMilliseconds(Math.Min(_currentVideo.Duration.TotalMilliseconds, Math.Max(time.TotalMilliseconds, _editingOptions.TrimStart.TotalMilliseconds + 1000)));
            }
            
            UpdateTimelineLayout();
        }
    }

    private void CropTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || _currentVideo == null) return;

        if (int.TryParse(CropTopTextBox.Text, out var top) &&
            int.TryParse(CropRightTextBox.Text, out var right) &&
            int.TryParse(CropBottomTextBox.Text, out var bottom) &&
            int.TryParse(CropLeftTextBox.Text, out var left))
        {
            _editingOptions.Crop.Top = Math.Max(0, top);
            _editingOptions.Crop.Right = Math.Max(0, right);
            _editingOptions.Crop.Bottom = Math.Max(0, bottom);
            _editingOptions.Crop.Left = Math.Max(0, left);
            
            UpdateCropPreview();
            UpdateCropDimensions();
        }
    }

    private void UpdateCropPreview()
    {
        if (!_isInitialized || _currentVideo == null || !_editingOptions.Crop.HasCropping)
        {
            CropRectangle.Visibility = Visibility.Collapsed;
            return;
        }

        // Show crop rectangle overlay
        var videoWidth = VideoPlayer.ActualWidth;
        var videoHeight = VideoPlayer.ActualHeight;
        
        if (videoWidth > 0 && videoHeight > 0)
        {
            var scaleX = videoWidth / _currentVideo.Width;
            var scaleY = videoHeight / _currentVideo.Height;
            
            var cropLeft = _editingOptions.Crop.Left * scaleX;
            var cropTop = _editingOptions.Crop.Top * scaleY;
            var cropWidth = (_currentVideo.Width - _editingOptions.Crop.Left - _editingOptions.Crop.Right) * scaleX;
            var cropHeight = (_currentVideo.Height - _editingOptions.Crop.Top - _editingOptions.Crop.Bottom) * scaleY;
            
            Canvas.SetLeft(CropRectangle, cropLeft);
            Canvas.SetTop(CropRectangle, cropTop);
            CropRectangle.Width = cropWidth;
            CropRectangle.Height = cropHeight;
            CropRectangle.Visibility = Visibility.Visible;
        }
    }

    private void UpdateCropDimensions()
    {
        if (!_isInitialized || _currentVideo == null) return;

        var newWidth = _editingOptions.Crop.CalculateWidth(_currentVideo.Width);
        var newHeight = _editingOptions.Crop.CalculateHeight(_currentVideo.Height);
        CropDimensionsText.Text = $"{newWidth} x {newHeight}";
    }

    private void ResizeWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || _currentVideo == null) return;

        if (int.TryParse(ResizeWidthTextBox.Text, out var width) && width > 0)
        {
            _editingOptions.Resize.Width = width;
            
            if (_editingOptions.Resize.MaintainAspectRatio)
            {
                _editingOptions.Resize.CalculateHeight(_currentVideo.Width, _currentVideo.Height, width);
                ResizeHeightTextBox.Text = _editingOptions.Resize.Height.ToString();
            }
        }
    }

    private void MaintainAspectRatioCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _currentVideo == null) return;
        
        _editingOptions.Resize.MaintainAspectRatio = MaintainAspectRatioCheckBox.IsChecked == true;
        
        if (_editingOptions.Resize.MaintainAspectRatio && _currentVideo != null && _editingOptions.Resize.Width > 0)
        {
            _editingOptions.Resize.CalculateHeight(_currentVideo.Width, _currentVideo.Height, _editingOptions.Resize.Width);
            ResizeHeightTextBox.Text = _editingOptions.Resize.Height.ToString();
        }
    }

    private void ResetTrimButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _currentVideo == null) return;

        _editingOptions.TrimStart = TimeSpan.Zero;
        _editingOptions.TrimEnd = _currentVideo.Duration;
        TrimStartTextBox.Text = "00:00:00";
        TrimEndTextBox.Text = _currentVideo.DurationFormatted;
        UpdateTimelineLayout();
    }

    private void ResetCropButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        
        _editingOptions.Crop.Reset();
        CropTopTextBox.Text = "0";
        CropRightTextBox.Text = "0";
        CropBottomTextBox.Text = "0";
        CropLeftTextBox.Text = "0";
        UpdateCropPreview();
        UpdateCropDimensions();
    }

    private void ResetResizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        
        _editingOptions.Resize.Reset();
        ResizeWidthTextBox.Text = "";
        ResizeHeightTextBox.Text = "";
    }

    private void CrfSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized)
            return;

        var crfValue = (int)Math.Round(e.NewValue);
        _editingOptions.CrfValue = crfValue;
        
        // Update the display text with quality description
        var qualityDescription = GetQualityDescription(crfValue);
        CrfValueText.Text = $"{crfValue} ({qualityDescription})";
        
        // Update the color based on quality level
        var brush = GetQualityBrush(crfValue);
        CrfValueText.Foreground = brush;
    }

    private string GetQualityDescription(int crfValue)
    {
        return crfValue switch
        {
            <= 15 => "Visually Lossless",
            <= 18 => "High Quality",
            <= 23 => "Good Quality",
            <= 28 => "Medium Quality",
            <= 35 => "Low Quality",
            _ => "Very Low Quality"
        };
    }

    private Brush GetQualityBrush(int crfValue)
    {
        return crfValue switch
        {
            <= 15 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E44AD")), // Purple - Visually Lossless
            <= 18 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")), // Green - High Quality
            <= 23 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")), // Blue - Good Quality
            <= 28 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")), // Orange - Medium Quality
            <= 35 => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E67E22")), // Dark Orange - Low Quality
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"))      // Red - Very Low Quality
        };
    }

    private void UpdateVideoInformation()
    {
        if (_currentVideo == null) return;

        VideoInfoText.Text = $"Duration: {_currentVideo.DurationFormatted}\n" +
                           $"Resolution: {_currentVideo.ResolutionText}\n" +
                           $"Size: {_currentVideo.FileSizeFormatted}\n" +
                           $"Format: {_currentVideo.Format}\n" +
                           $"Video Codec: {_currentVideo.VideoCodec}\n" +
                           $"Audio Codec: {_currentVideo.AudioCodec}";
    }

    private void ResetAllControls()
    {
        TrimStartTextBox.Text = "00:00:00";
        TrimEndTextBox.Text = _currentVideo?.DurationFormatted ?? "00:00:00";
        CropTopTextBox.Text = "0";
        CropRightTextBox.Text = "0";
        CropBottomTextBox.Text = "0";
        CropLeftTextBox.Text = "0";
        ResizeWidthTextBox.Text = "";
        ResizeHeightTextBox.Text = "";
        MaintainAspectRatioCheckBox.IsChecked = true;
        CrfSlider.Value = 18; // Reset CRF to default
        
        UpdateCropPreview();
        UpdateCropDimensions();
    }

    private async void ProcessButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentVideo == null) return;

        try
        {
            // Validate editing options
            if (!_editingOptions.HasTrimming && !_editingOptions.HasCropping && !_editingOptions.HasResizing)
            {
                MessageBox.Show("No editing operations specified. Please configure trimming, cropping, or resizing.", 
                    "No Changes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Set output path
            _editingOptions.OutputPath = _editingOptions.GetUniqueOutputPath();

            // Show processing window
            var progressWindow = new VideoProcessingWindow(_ffmpegService, _editingOptions, _currentVideo);
            progressWindow.Owner = this;
            progressWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting video processing: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        VideoPlayer.Close();
        _playbackTimer.Stop();
        base.OnClosed(e);
    }
} 