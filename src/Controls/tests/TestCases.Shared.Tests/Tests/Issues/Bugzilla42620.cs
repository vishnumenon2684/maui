using NUnit.Framework;
using UITest.Appium;
using UITest.Core;

namespace Microsoft.Maui.TestCases.Tests.Issues;

public class Bugzilla42620 : _IssuesUITest
{

	public Bugzilla42620(TestDevice testDevice) : base(testDevice)
	{
	}

	public override string Issue => " Grid.Children.AddHorizontal does not span all rows";

	// TODO from Xamarin.UITest migration: needs to be converted to use AutomationIds instead of referencing controls directly
	// [FailsOnAndroid]
	// [FailsOnIOS]
	// [Test]
	// [Category(UITestCategories.Layout)]
	// public void GridChildrenAddHorizontalDoesNotSpanAllRows()
	// {
	// 	Issue42620Test("RCRHVHVHVHVHV");

	// 	Issue42620Test("HHHV");
	// 	Issue42620Test("VVVH");

	// 	Issue42620Test("RV");
	// 	Issue42620Test("RH");
	// 	Issue42620Test("CV");
	// 	Issue42620Test("CH");

	// 	Issue42620Test("RVRRV");
	// 	Issue42620Test("CHCCH");

	// 	Issue42620Test("HHV");
	// 	Issue42620Test("HHH");
	// 	Issue42620Test("HVV");
	// 	Issue42620Test("HVH");
	// 	//Issue42620Test("VHV");
	// 	Issue42620Test("VHH");
	// 	Issue42620Test("VVV");
	// 	Issue42620Test("VVH");
	// }

	// public void Issue42620Test(string command)
	// {
	// 	App.WaitForElement(q => q.Marked(Ids.Clear));

	// 	_totalWidth = 0;
	// 	_totalHeight = 0;
	// 	_rowDef = 0;
	// 	_colDef = 0;

	// 	foreach (var c in command)
	// 	{
	// 		if (c == 'H')
	// 			AddHorizontal();
	// 		if (c == 'V')
	// 			AddVertical();
	// 		if (c == 'R')
	// 			AddRowDef();
	// 		if (c == 'C')
	// 			AddColumnDef();
	// 	}

	// 	Execute();
	// }

	// public void Execute()
	// {
	// 	App.EnterText("batch", string.Join("", _buttons));
	// 	App.DismissKeyboard();
	// 	App.Tap(Ids.Batch);

	// 	foreach (var result in _result)
	// 		App.WaitForElement(q => q.Marked($"{result}"));

	// 	_buttons.Clear();
	// 	_result.Clear();
	// }

	// public void AddHorizontal()
	// {
	// 	// new block gets new id
	// 	var id = _id++;

	// 	// adding column only increases height if no rows exist
	// 	if (_totalHeight == 0)
	// 		_totalHeight = 1;

	// 	// adding column always increased width by 1
	// 	_totalWidth++;

	// 	// column spans rows 0 to the last row
	// 	var row = 0;
	// 	var height = _totalHeight;

	// 	// column is always added at the end with a width of 1
	// 	var column = _totalWidth - 1;
	// 	var width = 1;

	// 	Tap(Ids.AddHorizontal);
	// 	WaitForElement($"{id}: {column}x{row} {width}x{height}");
	// }

	// public void AddVertical()
	// {
	// 	// new block gets new id
	// 	var id = _id++;

	// 	// adding row only increases width if no columns exist
	// 	if (_totalWidth == 0)
	// 		_totalWidth = 1;

	// 	// adding row always increased height by 1
	// 	_totalHeight++;

	// 	// row spans columns 0 to the last column
	// 	var column = 0;
	// 	var width = _totalWidth;

	// 	// row is always added at the end with a height of 1
	// 	var row = _totalHeight - 1;
	// 	var height = 1;

	// 	Tap(Ids.AddVertical);
	// 	WaitForElement($"{id}: {column}x{row} {width}x{height}");
	// }

	// public void AddRowDef()
	// {
	// 	Tap(Ids.AddRow);
	// 	_rowDef++;
	// 	_totalHeight = Math.Max(_rowDef, _totalHeight);
	// }

	// public void AddColumnDef()
	// {
	// 	Tap(Ids.AddColumn);
	// 	_colDef++;
	// 	_totalWidth = Math.Max(_colDef, _totalWidth);
	// }
}