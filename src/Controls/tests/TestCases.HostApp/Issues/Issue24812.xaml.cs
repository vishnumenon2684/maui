using System;
using Microsoft.Maui.Controls;

namespace Maui.Controls.Sample.Issues
{
	[Issue(IssueTracker.Github, 24812, "WebViewHandler OnProgressChanged not called on Android", PlatformAffected.Android)]
	public partial class Issue24812 : ContentPage
	{
		private int _progressCallbackCount = 0;
		private double _lastProgressValue = -1;

		public Issue24812()
		{
			InitializeComponent();
			Console.WriteLine("=== Issue24812: Page initialized ===");
		}

		protected override void OnAppearing()
		{
			base.OnAppearing();
			Console.WriteLine("=== Issue24812: OnAppearing ===");
			
			// Auto-load after a short delay
			Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
			{
				LoadWebView();
			});
		}

		private void OnLoadPage(object sender, EventArgs e)
		{
			Console.WriteLine("=== Issue24812: LoadButton clicked ===");
			LoadWebView();
		}

		private void LoadWebView()
		{
			_progressCallbackCount = 0;
			_lastProgressValue = -1;
			UpdateStatusLabel("Loading...");
			
			// Load a URL that takes some time to load so we can observe progress
			TestWebView.Source = "https://www.microsoft.com";
			Console.WriteLine("=== Issue24812: WebView.Source set to https://www.microsoft.com ===");
		}

		private void OnWebViewProgressChanged(object sender, WebViewProgressChangedEventArgs e)
		{
			_progressCallbackCount++;
			_lastProgressValue = e.Progress;
			
			Console.WriteLine($"=== Issue24812: ProgressChanged callback #{_progressCallbackCount}, Progress: {e.Progress:F2} ===");
			
			Dispatcher.Dispatch(() =>
			{
				ProgressLabel.Text = $"Progress: {e.Progress:P0}";
				ProgressValueLabel.Text = $"Progress Value: {e.Progress:F2}";
				
				if (e.Progress >= 1.0)
				{
					UpdateStatusLabel($"Complete! Callbacks: {_progressCallbackCount}");
				}
				else
				{
					UpdateStatusLabel($"Loading... Callbacks: {_progressCallbackCount}");
				}
			});
		}

		private void UpdateStatusLabel(string status)
		{
			Dispatcher.Dispatch(() =>
			{
				StatusLabel.Text = $"Status: {status}";
				Console.WriteLine($"=== Issue24812: Status updated: {status} ===");
			});
		}
	}

	// Custom WebView control
	public class CustomWebView : WebView
	{
		public CustomWebView()
		{
			Console.WriteLine($"=== Issue24812: CustomWebView constructor called, ID: {this.GetHashCode()} ===");
		}

		public event EventHandler<WebViewProgressChangedEventArgs> ProgressChanged;

		public void UpdateProgress(double progress)
		{
			Console.WriteLine($"=== Issue24812: CustomWebView.UpdateProgress called: {progress:F2} ===");
			ProgressChanged?.Invoke(this, new WebViewProgressChangedEventArgs(progress));
		}
	}

	// Event args for progress changed
	public class WebViewProgressChangedEventArgs : EventArgs
	{
		public WebViewProgressChangedEventArgs(double progress)
		{
			Progress = progress;
		}

		public double Progress { get; }
	}
}
