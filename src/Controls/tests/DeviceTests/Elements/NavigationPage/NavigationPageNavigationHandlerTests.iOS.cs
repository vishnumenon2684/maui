using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;
using Xunit;

namespace Microsoft.Maui.DeviceTests
{
	/// <summary>
	/// Reuses every NavigationPageTests fact/theory under NavigationViewHandler instead of the
	/// base class's NavigationRenderer. Same pattern as ShellHandlerSubclasses.Android.cs.
	/// TabbedPage stays on TabbedRenderer here; its handler migration is out of scope.
	/// </summary>
	[Category(TestCategory.NavigationPage)]
	[Collection(ControlsHandlerTestBase.RunInNewWindowCollection)]
	[Trait(RendererHandlerVariant.NavigationViewVariantTraitName, RendererHandlerVariant.NavigationViewHandler)] // See RendererHandlerVariant.cs
	public class NavigationPageNavigationHandlerTests : NavigationPageTests
	{
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public Task PopToRootAsyncCompletesForInactiveTabsWhileModalIsOpen(bool animated) =>
			ValidatePopToRootForInactiveTabsWhileModalIsOpen(animated);

		protected override void RegisterNavigationPageHandlers(IMauiHandlersCollection handlers)
		{
			handlers.AddHandler(typeof(Toolbar), typeof(ToolbarHandler));
			handlers.AddHandler(typeof(NavigationPage), typeof(NavigationViewHandler));
			handlers.AddHandler(typeof(TabbedPage), typeof(TabbedRenderer));
		}
	}
}
