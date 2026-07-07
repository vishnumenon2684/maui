using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Issue34257 : _IssuesUITest
{
	public Issue34257(TestDevice device)
		: base(device)
	{
	}

	public override string Issue => "CollectionView vertical grid item spacing updates all rows and columns";

	[Test]
	[Category(UITestCategories.CollectionView)]
	public void UpdatingHorizontalSpacingShouldResizeBothColumns()
	{
		var collection = App.WaitForElement("TestCollectionView").GetRect();
		var firstColumnBefore = App.WaitForElement("FirstColumnTopItem").GetRect();
		var offsetBefore = firstColumnBefore.X - collection.X;

		App.Tap("ApplyHorizontalSpacingButton");
		App.WaitForElement("StatusLabel", "Spacing=0,80");

		var collectionAfter = App.WaitForElement("TestCollectionView").GetRect();
		var firstColumnAfter = App.WaitForElement("FirstColumnTopItem").GetRect();
		var offsetAfter = firstColumnAfter.X - collectionAfter.X;

		Assert.That(offsetAfter, Is.GreaterThan(offsetBefore),
			"First column should have spacing to its left when horizontal spacing is applied");
	}

	[Test]
	[Category(UITestCategories.CollectionView)]
	public void UpdatingVerticalSpacingShouldResizeBothRows()
	{
		var collection = App.WaitForElement("TestCollectionView").GetRect();
		var firstRowBefore = App.WaitForElement("FirstColumnTopItem").GetRect();
		var offsetBefore = firstRowBefore.Y - collection.Y;

		App.Tap("ApplyVerticalSpacingButton");
		App.WaitForElement("StatusLabel", "Spacing=40,0");

		var collectionAfter = App.WaitForElement("TestCollectionView").GetRect();
		var firstRowAfter = App.WaitForElement("FirstColumnTopItem").GetRect();
		var offsetAfter = firstRowAfter.Y - collectionAfter.Y;

		Assert.That(offsetAfter, Is.GreaterThan(offsetBefore),
			"First row should have spacing above it when vertical spacing is applied");
	}
}