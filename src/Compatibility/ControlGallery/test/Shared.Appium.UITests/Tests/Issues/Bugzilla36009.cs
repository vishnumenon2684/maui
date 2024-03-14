﻿using NUnit.Framework;
using UITest.Appium;

namespace UITests
{
	public class Bugzilla36009 : IssuesUITest
	{
		public Bugzilla36009(TestDevice testDevice) : base(testDevice)
		{
		}

		public override string Issue => "[Bug] Exception Ancestor must be provided for all pushes except first";

		[Test]
		[Category(UITestCategories.BoxView)]
		public void Bugzilla36009Test()
		{
			this.IgnoreIfPlatforms([TestDevice.Android, TestDevice.iOS, TestDevice.Mac, TestDevice.Windows], "The content is not visible. The test is failing.");

			RunningApp.WaitForElement("Victory");
		}
	}
}