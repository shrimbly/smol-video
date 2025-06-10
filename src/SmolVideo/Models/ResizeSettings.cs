namespace SmolVideo.Models;

public class ResizeSettings
{
    public int Width { get; set; } = 0;
    public int Height { get; set; } = 0;
    public bool MaintainAspectRatio { get; set; } = true;
    
    public bool HasResizing => Width > 0 && Height > 0;
    
    public void CalculateHeight(int originalWidth, int originalHeight, int newWidth)
    {
        if (MaintainAspectRatio && originalWidth > 0 && originalHeight > 0)
        {
            Height = (int)Math.Round((double)newWidth * originalHeight / originalWidth);
            // Ensure even dimensions for video encoding
            if (Height % 2 != 0)
                Height += 1;
        }
    }
    
    public void SetDimensions(int width, int height)
    {
        Width = width;
        Height = height;
    }
    
    public bool IsValid()
    {
        return Width > 0 && Height > 0 && Width <= 7680 && Height <= 4320; // Max 8K resolution
    }
    
    public void Reset()
    {
        Width = Height = 0;
    }
} 