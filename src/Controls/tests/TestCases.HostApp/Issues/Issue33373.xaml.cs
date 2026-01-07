namespace Maui.Controls.Sample.Issues;

[Issue(IssueTracker.Github, 33373, "SemanticProperties.Description announced twice when set on focusable container cell", PlatformAffected.Windows)]
public partial class Issue33373 : ContentPage
{
	public Issue33373()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Console.WriteLine("=== Issue 33373 Test Page Appearing ===");
		Console.WriteLine("Testing: SemanticProperties.Description duplicate announcements on Windows");
		Console.WriteLine("Test cells are ready. Focus on each cell using Tab key.");
		Console.WriteLine("Narrator should announce the description once, not the description AND the label text together.");
		Console.WriteLine("=== Ready for testing ===");
	}
}
