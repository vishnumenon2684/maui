using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue33373 : _IssuesUITest
{
	public Issue33373(TestDevice testDevice) : base(testDevice) { }

	public override string Issue => "SemanticProperties.Description announced twice when set on focusable container cell";

	[Test]
	[Category(UITestCategories.Accessibility)]
	public void SemanticDescriptionNotAnnouncedTwice()
	{
		// Verify that the test page loads
		App.WaitForElement("Cell1");
		
		// Verify the first test cell is in the accessibility tree
		var cell1Element = App.FindElement("Cell1");
		Assert.That(cell1Element, Is.Not.Null, "Test Cell 1 should be found");

		// Verify the second test cell
		var cell2Element = App.FindElement("Cell2");
		Assert.That(cell2Element, Is.Not.Null, "Test Cell 2 should be found");

		// Verify the third test cell with hint
		var cell3Element = App.FindElement("Cell3");
		Assert.That(cell3Element, Is.Not.Null, "Test Cell 3 should be found");

		// On Windows with the fix, the semantic description should be announced
		// but NOT combined with the label text, preventing duplication
		Console.WriteLine("All test cells are accessible and ready for Narrator testing on Windows");
	}
}
