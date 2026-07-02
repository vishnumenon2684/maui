using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Core.UnitTests;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.UnitTests;
using Xunit;

namespace Microsoft.Maui.Controls.Xaml.UnitTests;

public partial class Maui29708 : ContentPage
{
	public Maui29708() => InitializeComponent();

	[Collection("Issue")]
	public class Tests : IDisposable
	{
		public Tests()
		{
			Application.SetCurrentApplication(new MockApplication());
			DispatcherProvider.SetCurrent(new DispatcherProviderStub());
		}

		public void Dispose() => AppInfo.SetCurrent(null);

		[Theory]
		[XamlInflatorData]
		internal void GlobalUsingTypeResolvedInXaml(XamlInflator inflator)
		{
			var page = new Maui29708(inflator);
			Assert.NotNull(page.customLabel);
			Assert.Equal("Hello", page.customLabel.Text);
			Assert.Equal("World", page.customLabel.Subtitle);
		}
	}
}
