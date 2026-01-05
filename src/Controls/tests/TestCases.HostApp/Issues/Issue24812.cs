#if ANDROID
using Android.Webkit;
using Microsoft.Maui.Handlers;

namespace Maui.Controls.Sample.Issues
{
	// Custom WebViewHandler for Android
	public class CustomWebViewHandler : WebViewHandler
	{
		private CustomWebView Element => VirtualView as CustomWebView;

		protected override void ConnectHandler(Android.Webkit.WebView platformView)
		{
			System.Console.WriteLine($"=== Issue24812: CustomWebViewHandler.ConnectHandler START, Element: {Element?.GetHashCode()}, PlatformView: {platformView?.GetHashCode()} ===");
			
			base.ConnectHandler(platformView);
			
			System.Console.WriteLine($"=== Issue24812: CustomWebViewHandler.ConnectHandler AFTER base.ConnectHandler ===");

			if (Element is not null && platformView is not null)
			{
				var customClient = new CustomWebChromeClient(Element);
				platformView.SetWebChromeClient(customClient);
				System.Console.WriteLine($"=== Issue24812: Custom WebChromeClient set, Client ID: {customClient.GetHashCode()} ===");
				
#pragma warning disable CA1416 // Validate platform compatibility - We're checking the WebChromeClient that was set
#if ANDROID26_0_OR_GREATER
				// Verify what client is actually set (WebChromeClient property only available in API 26+)
				var currentClient = platformView.WebChromeClient;
				System.Console.WriteLine($"=== Issue24812: WebChromeClient after SetWebChromeClient: {currentClient?.GetHashCode()}, IsCustom: {currentClient is CustomWebChromeClient} ===");
#endif
#pragma warning restore CA1416
			}
			else
			{
				System.Console.WriteLine($"=== Issue24812: WARNING - Element or PlatformView is null in ConnectHandler ===");
			}

			if (platformView is not null)
			{
				platformView.Settings.JavaScriptEnabled = true;
			}
			
			System.Console.WriteLine($"=== Issue24812: CustomWebViewHandler.ConnectHandler END ===");
		}

		private class CustomWebChromeClient : WebChromeClient
		{
			private readonly CustomWebView _customWebView;

			public CustomWebChromeClient(CustomWebView customWebView)
			{
				_customWebView = customWebView;
				System.Console.WriteLine($"=== Issue24812: CustomWebChromeClient constructor, WebView ID: {customWebView?.GetHashCode()} ===");
			}

			public override void OnProgressChanged(Android.Webkit.WebView view, int newProgress)
			{
				System.Console.WriteLine($"=== Issue24812: CustomWebChromeClient.OnProgressChanged called! Progress: {newProgress} ===");
				var progressValue = Math.Round((double)newProgress / 100, 2);
				_customWebView?.UpdateProgress(progressValue);
			}
		}
	}
}
#endif
