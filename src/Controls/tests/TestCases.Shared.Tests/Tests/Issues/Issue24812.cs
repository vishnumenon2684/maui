using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues
{
	public class Issue24812 : _IssuesUITest
	{
		public override string Issue => "WebViewHandler OnProgressChanged not called on Android";

		public Issue24812(TestDevice device) : base(device) { }

		[Test]
		[Category(UITestCategories.WebView)]
		public void WebViewHandlerOnProgressChangedShouldBeCalled()
		{
			App.WaitForElement("TitleLabel");
			
			// Wait for auto-load to complete (triggered in OnAppearing)
			// Give it time to load and for progress callbacks to fire
			Task.Delay(8000).Wait();

			// Verify progress was reported
			var progressValue = App.FindElement("ProgressValueLabel").GetText();
			var status = App.FindElement("StatusLabel").GetText();

			// The progress value should have been updated from initial "-1"
			// to something between 0.0 and 1.0, and should end at 1.0 when complete
			Assert.That(progressValue, Does.Not.Contain("-1"), 
				"Progress value should have been updated from initial -1");
			
			Assert.That(progressValue, Does.Contain("1.00") | Does.Contain("1,00"),
				"Progress should reach 1.00 (100%) when page loads completely");

			// Status should indicate callbacks were received
			Assert.That(status, Does.Contain("Callbacks:"),
				"Status should show callback count");
			
			Assert.That(status, Does.Not.Contain("Callbacks: 0"),
				"At least one progress callback should have been received");
		}
	}
}
