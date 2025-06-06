using System.Windows;

namespace VideoOptimizer.UI;

public partial class ErrorDialog : Window
{
    public ErrorDialog(string errorMessage, string errorDetails)
    {
        InitializeComponent();
        
        ErrorMessageText.Text = errorMessage;
        ErrorDetailsText.Text = string.IsNullOrWhiteSpace(errorDetails) 
            ? "No additional details available." 
            : errorDetails;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CopyDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fullError = $"Error: {ErrorMessageText.Text}\n\nDetails:\n{ErrorDetailsText.Text}";
            Clipboard.SetText(fullError);
            
            // Temporarily change button text to show feedback
            var button = sender as System.Windows.Controls.Button;
            if (button != null)
            {
                var originalContent = button.Content;
                button.Content = "Copied!";
                button.IsEnabled = false;
                
                // Reset after 2 seconds
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, args) =>
                {
                    button.Content = originalContent;
                    button.IsEnabled = true;
                    timer.Stop();
                };
                timer.Start();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
} 