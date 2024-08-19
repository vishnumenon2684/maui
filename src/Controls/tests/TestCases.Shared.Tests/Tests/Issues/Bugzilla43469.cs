using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Bugzilla43469 : _IssuesUITest
{

	public Bugzilla43469(TestDevice testDevice) : base(testDevice)
	{
	}

	public override string Issue => "Calling DisplayAlert twice in WinRT causes a crash";

	[Test]
	[Category(UITestCategories.DisplayAlert)]
	[FailsOnIOS]
	public async Task Bugzilla43469Test()
	{
		App.WaitForElement("kButton");
		App.Tap("kButton");
		App.WaitForElement("First");
		App.GetAlert()?.DismissAlert();
		App.WaitForElement("Second");
		App.GetAlert()?.DismissAlert();
		App.WaitForElement("Three");
		App.GetAlert()?.DismissAlert();

		await Task.Delay(100);
		App.GetAlert()?.DismissAlert();
		await Task.Delay(100);
		App.GetAlert()?.DismissAlert();
		await Task.Delay(100);
		App.GetAlert()?.DismissAlert();
		await Task.Delay(100);
		App.WaitForElement("kButton");
	}
}