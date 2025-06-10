namespace SmolVideo.Models;

public class CropSettings
{
    public int Top { get; set; } = 0;
    public int Right { get; set; } = 0;
    public int Bottom { get; set; } = 0;
    public int Left { get; set; } = 0;
    
    public bool HasCropping => Top > 0 || Right > 0 || Bottom > 0 || Left > 0;
    
    public int CalculateWidth(int originalWidth)
    {
        return originalWidth - Left - Right;
    }
    
    public int CalculateHeight(int originalHeight)
    {
        return originalHeight - Top - Bottom;
    }
    
    public bool IsValid(int videoWidth, int videoHeight)
    {
        return Left >= 0 && Right >= 0 && Top >= 0 && Bottom >= 0 &&
               Left + Right < videoWidth && Top + Bottom < videoHeight;
    }
    
    public void Reset()
    {
        Top = Right = Bottom = Left = 0;
    }
} 