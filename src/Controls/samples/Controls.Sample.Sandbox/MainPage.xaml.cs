namespace Maui.Controls.Sample;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        LoadWideImageAsync();
    }

    private async void LoadWideImageAsync()
    {
        try
        {
            StatusLabel.Text = "Status: Loading wide image (1600x130)...";
            
            // Load the wide image from embedded resources
            await using var stream = await FileSystem.OpenAppPackageFileAsync("wide_test_image.png");
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var imageBytes = ms.ToArray();

            // Write to local storage
            var localPath = Path.Combine(FileSystem.Current.AppDataDirectory, "test_wide_image.png");
            await File.WriteAllBytesAsync(localPath, imageBytes);

            // Load the image
            TestImage.Source = ImageSource.FromFile(localPath);

            StatusLabel.Text = "Status: ✅ Image loaded successfully!";
            StatusLabel.TextColor = Colors.Green;
            ImageInfoLabel.Text = $"Image Info: {imageBytes.Length} bytes, Path: {localPath}";

            Console.WriteLine("========== ISSUE 32869 TEST ==========");
            Console.WriteLine($"✅ SUCCESS: Wide image loaded without crash");
            Console.WriteLine($"Image size: {imageBytes.Length} bytes");
            Console.WriteLine($"Image path: {localPath}");
            Console.WriteLine("=====================================");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Status: ❌ ERROR - {ex.GetType().Name}";
            StatusLabel.TextColor = Colors.Red;
            ImageInfoLabel.Text = $"Error: {ex.Message}";

            Console.WriteLine("========== ISSUE 32869 TEST ==========");
            Console.WriteLine($"❌ FAILED: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            Console.WriteLine("=====================================");
        }
    }
}