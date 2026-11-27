using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoreAnimation;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using UIKit;
using Xunit;
using static Microsoft.Maui.DeviceTests.AssertHelpers;

namespace Microsoft.Maui.DeviceTests
{
	[Category(TestCategory.TabbedPage)]
	public partial class TabbedPageTests
	{
		[Fact(DisplayName = "TabbedPage BarBackground gradient persists after iOS 15 appearance update")]
		public async Task BarBackgroundGradientPersistsAfteriOS15AppearanceUpdate()
		{
			if (!OperatingSystem.IsIOSVersionAtLeast(15) && !OperatingSystem.IsMacCatalystVersionAtLeast(15))
				return;

			SetupBuilder();
			var tabbedPage = CreateBasicTabbedPage();
			tabbedPage.BarBackground = new LinearGradientBrush(
				new GradientStopCollection
				{
					new GradientStop(Colors.Red, 0),
					new GradientStop(Colors.Blue, 1)
				},
				new Point(0, 0),
				new Point(1, 1));
			tabbedPage.SelectedTabColor = Colors.Green;

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(tabbedPage), handler =>
			{
				Assert.Single(GetTabBar(tabbedPage).Layer.Sublayers.OfType<CAGradientLayer>());

				tabbedPage.BarTextColor = Colors.Green;

				Assert.Single(GetTabBar(tabbedPage).Layer.Sublayers.OfType<CAGradientLayer>());

				return Task.CompletedTask;
			});
		}

		[Fact]
		public async Task GradientGeometryChangesUpdateNativeAppearance()
		{
			if (!OperatingSystem.IsIOSVersionAtLeast(15) && !OperatingSystem.IsMacCatalystVersionAtLeast(15))
				return;

			SetupBuilder();
			var tabbedPage = CreateBasicTabbedPage();
			var gradient = new LinearGradientBrush(
				new GradientStopCollection
				{
					new GradientStop(Colors.Red, 0),
					new GradientStop(Colors.Blue, 1)
				},
				new Point(0, 0),
				new Point(1, 1));
			tabbedPage.BarBackground = gradient;

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(tabbedPage), handler =>
			{
				var tabBar = GetTabBar(tabbedPage);
				gradient.StartPoint = new Point(0.25, 0.5);
				gradient.EndPoint = new Point(0.75, 0);

				var layer = Assert.Single(tabBar.Layer.Sublayers.OfType<CAGradientLayer>());
				Assert.Equal(0.25, (double)layer.StartPoint.X);
				Assert.Equal(0.5, (double)layer.StartPoint.Y);
				Assert.Equal(0.75, (double)layer.EndPoint.X);
				Assert.Equal(0, (double)layer.EndPoint.Y);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public async Task SolidBarBackgroundColorChangesUpdateNativeAppearance()
		{
			if (!OperatingSystem.IsIOSVersionAtLeast(15) && !OperatingSystem.IsMacCatalystVersionAtLeast(15))
				return;

			SetupBuilder();
			var tabbedPage = CreateBasicTabbedPage();
			var original = new SolidColorBrush(Colors.Red);
			tabbedPage.BarBackground = original;

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(tabbedPage), handler =>
			{
				var tabBar = GetTabBar(tabbedPage);
				Assert.True(ColorComparison.ARGBEquivalent(Colors.Red.ToPlatform(), tabBar.BackgroundColor));

				original.Color = Colors.Blue;
				Assert.True(ColorComparison.ARGBEquivalent(Colors.Blue.ToPlatform(), tabBar.StandardAppearance.BackgroundColor));
				Assert.True(ColorComparison.ARGBEquivalent(Colors.Blue.ToPlatform(), tabBar.BackgroundColor));

				var replacement = new SolidColorBrush(Colors.Green);
				tabbedPage.BarBackground = replacement;
				original.Color = Colors.Yellow;
				Assert.True(ColorComparison.ARGBEquivalent(Colors.Green.ToPlatform(), tabBar.BackgroundColor));

				replacement.Color = Colors.Purple;
				Assert.True(ColorComparison.ARGBEquivalent(Colors.Purple.ToPlatform(), tabBar.BackgroundColor));

				tabbedPage.BarBackground = null;
				replacement.Color = Colors.Red;
				Assert.Null(tabBar.BackgroundColor);
				return Task.CompletedTask;
			});
		}

		[Theory]
		[InlineData(false, false)]
		[InlineData(false, true)]
		[InlineData(true, false)]
		[InlineData(true, true)]
		public async Task UnsupportedBarBackgroundFallsBackToColor(bool customGradient, bool hasBackgroundColor)
		{
			if (!OperatingSystem.IsIOSVersionAtLeast(15) && !OperatingSystem.IsMacCatalystVersionAtLeast(15))
				return;

			SetupBuilder();
			var tabbedPage = CreateBasicTabbedPage();
			tabbedPage.BarBackground = customGradient
				? new UnsupportedGradientBrush
				{
					GradientStops = new GradientStopCollection
					{
						new GradientStop(Colors.Red, 0),
						new GradientStop(Colors.Blue, 1)
					}
				}
				: new ImageBrush(ImageSource.FromFile("background.png"));
			if (hasBackgroundColor)
				tabbedPage.BarBackgroundColor = Colors.Blue;

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(tabbedPage), handler =>
			{
				var tabBar = GetTabBar(tabbedPage);
				var expectedColor = hasBackgroundColor ? Colors.Blue.ToPlatform() : null;
				Assert.True(ColorComparison.ARGBEquivalent(expectedColor, tabBar.StandardAppearance.BackgroundColor));
				Assert.Null(tabBar.BackgroundColor);
				return Task.CompletedTask;
			});
		}

		sealed class UnsupportedGradientBrush : GradientBrush
		{
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task LegacyBarBackgroundColorPreservesNativeViewBackground(bool afterGradient)
		{
			if (!OperatingSystem.IsIOSVersionAtLeast(15) && !OperatingSystem.IsMacCatalystVersionAtLeast(15))
				return;

			SetupBuilder();
			var tabbedPage = CreateBasicTabbedPage();
			tabbedPage.BarBackgroundColor = Colors.Blue;
			if (afterGradient)
			{
				tabbedPage.BarBackground = new LinearGradientBrush(
					new GradientStopCollection
					{
						new GradientStop(Colors.Red, 0),
						new GradientStop(Colors.Blue, 1)
					},
					new Point(0, 0),
					new Point(1, 1));
			}

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(tabbedPage), handler =>
			{
				var tabBar = GetTabBar(tabbedPage);
				tabbedPage.BarBackground = null;

				Assert.True(ColorComparison.ARGBEquivalent(Colors.Blue.ToPlatform(), tabBar.StandardAppearance.BackgroundColor));
				Assert.Null(tabBar.BackgroundColor);
				Assert.DoesNotContain(tabBar.Layer.Sublayers ?? Array.Empty<CALayer>(), layer => layer is CAGradientLayer);
				return Task.CompletedTask;
			});
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task BarBackgroundTransitionRemovesGradientAndRestoresEffect(bool useSolidBackground)
		{
			if (!OperatingSystem.IsIOSVersionAtLeast(15) && !OperatingSystem.IsMacCatalystVersionAtLeast(15))
				return;

			SetupBuilder();
			var tabbedPage = CreateBasicTabbedPage();
			tabbedPage.BarBackground = new LinearGradientBrush(
				new GradientStopCollection
				{
					new GradientStop(Colors.Red, 0),
					new GradientStop(Colors.Blue, 1)
				},
				new Point(0, 0),
				new Point(1, 1));

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(tabbedPage), handler =>
			{
				var tabBar = GetTabBar(tabbedPage);
				Assert.Single(tabBar.Layer.Sublayers.OfType<CAGradientLayer>());

				tabbedPage.BarBackground = useSolidBackground ? new SolidColorBrush(Colors.Green) : null;

				using var defaultAppearance = new UITabBarAppearance();
				defaultAppearance.ConfigureWithDefaultBackground();
				Assert.Equal(defaultAppearance.BackgroundEffect?.GetType(), tabBar.StandardAppearance.BackgroundEffect?.GetType());
				Assert.True(ColorComparison.ARGBEquivalent(useSolidBackground ? Colors.Green.ToPlatform() : null, tabBar.BackgroundColor));
				Assert.DoesNotContain(tabBar.Layer.Sublayers, layer => layer is CAGradientLayer);
				return Task.CompletedTask;
			});
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task TabBarBackgroundEffectIsRestoredAfterGradient(bool useSolidBackground)
		{
			if (!OperatingSystem.IsIOSVersionAtLeast(15) && !OperatingSystem.IsMacCatalystVersionAtLeast(15))
				return;

			await InvokeOnMainThreadAsync(() =>
			{
				using var tabBar = new UITabBar();
				using var defaultAppearance = new UITabBarAppearance();
				defaultAppearance.ConfigureWithDefaultBackground();
				UITabBarAppearance appearance = null;

				try
				{
					Paint gradient = new LinearGradientBrush(
						new GradientStopCollection
						{
							new GradientStop(Colors.Red, 0),
							new GradientStop(Colors.Blue, 1)
						},
						new Point(0, 0),
						new Point(1, 1));

					tabBar.UpdateiOS15TabBarAppearance(ref appearance, null, null, null, null, gradient, null, null);
					Assert.Null(appearance.BackgroundEffect);

					Paint background = useSolidBackground ? new SolidPaint(Colors.Green) : null;
					tabBar.UpdateiOS15TabBarAppearance(ref appearance, null, null, null, null, background, null, null);

					Assert.Equal(defaultAppearance.BackgroundEffect?.GetType(), appearance.BackgroundEffect?.GetType());
					Assert.True(ColorComparison.ARGBEquivalent(useSolidBackground ? Colors.Green.ToPlatform() : null, tabBar.BackgroundColor));
					Assert.DoesNotContain(tabBar.Layer.Sublayers ?? Array.Empty<CALayer>(), layer => layer is CAGradientLayer);
				}
				finally
				{
					appearance?.Dispose();
				}
			});
		}

		[Fact]
		public async Task BadgePropertiesUpdateNativeTabBarItem()
		{
			SetupBuilder(builder =>
			{
				builder.ConfigureMauiHandlers(handlers =>
				{
					handlers.AddHandler<TabbedPage, Microsoft.Maui.Handlers.TabbedViewHandler>();
				});
			});

			var firstPage = new ContentPage { Title = "First" };
			var secondPage = new ContentPage { Title = "Second" };
			TabbedPage.SetBadgeText(firstPage, "7");
			TabbedPage.SetBadgeColor(firstPage, Colors.Blue);
			TabbedPage.SetBadgeTextColor(firstPage, Colors.Yellow);

			var tabbedPage = CreateBasicTabbedPage(pages: new[] { firstPage, secondPage });

			await CreateHandlerAndAddToWindow<Microsoft.Maui.Handlers.TabbedViewHandler>(tabbedPage, async handler =>
			{
				await OnLoadedAsync(firstPage);
				UITabBarItem GetFirstTabBarItem() => handler.Manager?.TabBar?.Items?.FirstOrDefault();

				await AssertEventually(() => GetFirstTabBarItem()?.BadgeValue == "7");
				var tabBarItem = GetFirstTabBarItem();
				Assert.True(ColorComparison.ARGBEquivalent(Colors.Blue.ToPlatform(), tabBarItem.BadgeColor));
				AssertBadgeTextColor(tabBarItem, Colors.Yellow);

				TabbedPage.SetBadgeText(firstPage, "");
				await AssertEventually(() => GetFirstTabBarItem()?.BadgeValue == "");

				TabbedPage.SetBadgeText(firstPage, "New");
				TabbedPage.SetBadgeColor(firstPage, Colors.Orange);
				TabbedPage.SetBadgeTextColor(firstPage, Colors.Black);
				await AssertEventually(() =>
					GetFirstTabBarItem()?.BadgeValue == "New" &&
					ColorComparison.ARGBEquivalent(Colors.Orange.ToPlatform(), GetFirstTabBarItem().BadgeColor));
				tabBarItem = GetFirstTabBarItem();
				AssertBadgeTextColor(tabBarItem, Colors.Black);

				firstPage.ClearValue(TabbedPage.BadgeColorProperty);
				firstPage.ClearValue(TabbedPage.BadgeTextColorProperty);
				await AssertEventually(() =>
					GetFirstTabBarItem() is { } item &&
					item.BadgeColor is null &&
					item.GetBadgeTextAttributes(UIControlState.Normal)?.ForegroundColor is null);

				TabbedPage.SetBadgeText(firstPage, null);
				await AssertEventually(() =>
					GetFirstTabBarItem() is { } item &&
					item.BadgeValue is null);
			});
		}

		[Fact]
		public async Task BadgeTextUpdatePreservesNativeBadgeAppearance()
		{
			SetupBuilder(builder =>
			{
				builder.ConfigureMauiHandlers(handlers =>
				{
					handlers.AddHandler<TabbedPage, Microsoft.Maui.Handlers.TabbedViewHandler>();
				});
			});

			var firstPage = new ContentPage { Title = "First" };
			TabbedPage.SetBadgeText(firstPage, "1");
			var tabbedPage = CreateBasicTabbedPage(pages: new[] { firstPage });

			await CreateHandlerAndAddToWindow<Microsoft.Maui.Handlers.TabbedViewHandler>(tabbedPage, async handler =>
			{
				await OnLoadedAsync(firstPage);
				var tabBarItem = handler.Manager?.TabBar?.Items?.FirstOrDefault();
				Assert.NotNull(tabBarItem);

				var attributes = new UIStringAttributes { ForegroundColor = Colors.Green.ToPlatform() };
				tabBarItem.BadgeColor = Colors.Purple.ToPlatform();
				tabBarItem.SetBadgeTextAttributes(attributes, UIControlState.Normal);

				TabbedPage.SetBadgeText(firstPage, "2");

				await AssertEventually(() => tabBarItem.BadgeValue == "2");
				Assert.True(ColorComparison.ARGBEquivalent(Colors.Purple.ToPlatform(), tabBarItem.BadgeColor));
				Assert.True(ColorComparison.ARGBEquivalent(
					Colors.Green.ToPlatform(),
					tabBarItem.GetBadgeTextAttributes(UIControlState.Normal)?.ForegroundColor));
			});
		}

		static void AssertBadgeTextColor(UITabBarItem tabBarItem, Color expected)
		{
			Assert.True(ColorComparison.ARGBEquivalent(
				expected.ToPlatform(),
				tabBarItem.GetBadgeTextAttributes(UIControlState.Normal)?.ForegroundColor));
			Assert.True(ColorComparison.ARGBEquivalent(
				expected.ToPlatform(),
				tabBarItem.GetBadgeTextAttributes(UIControlState.Selected)?.ForegroundColor));
			Assert.True(ColorComparison.ARGBEquivalent(
				expected.ToPlatform(),
				tabBarItem.GetBadgeTextAttributes(UIControlState.Disabled)?.ForegroundColor));
			Assert.True(ColorComparison.ARGBEquivalent(
				expected.ToPlatform(),
				tabBarItem.GetBadgeTextAttributes(UIControlState.Focused)?.ForegroundColor));
		}

		UITabBar GetTabBar(TabbedPage tabbedPage)
		{
			var handler = Assert.IsAssignableFrom<IPlatformViewHandler>(tabbedPage.Handler);
			var controller = Assert.IsAssignableFrom<UITabBarController>(handler.ViewController);
			return controller.TabBar;
		}

		async Task ValidateTabBarIconColor(
			TabbedPage tabbedPage,
			string tabText,
			Color iconColor,
			bool hasColor)
		{
			if (hasColor)
			{
				await AssertionExtensions.AssertTabItemIconContainsColor(
					GetTabBar(tabbedPage),
					tabText, iconColor, MauiContext);
			}
			else
			{
				await AssertionExtensions.AssertTabItemIconDoesNotContainColor(
					GetTabBar(tabbedPage),
					tabText, iconColor, MauiContext);
			}
		}

		async Task ValidateTabBarTextColor(
			TabbedPage tabbedPage,
			string tabText,
			Color iconColor,
			bool hasColor)
		{
			if (hasColor)
			{
				await AssertionExtensions.AssertTabItemTextContainsColor(
					GetTabBar(tabbedPage),
					tabText, iconColor, MauiContext);
			}
			else
			{
				await AssertionExtensions.AssertTabItemTextDoesNotContainColor(
					GetTabBar(tabbedPage),
					tabText, iconColor, MauiContext);
			}
		}

		[Theory("Handler: Tab switch fires Appearing/Disappearing on NavigationPage content")]
		[ClassData(typeof(TabbedPagePivots))]
		public async Task Handler_TabSwitchFiresAppearingDisappearing(bool bottomTabs, bool isSmoothScrollEnabled)
		{
			EnsureHandlerCreated(builder =>
			{
				builder.ConfigureMauiHandlers(handlers =>
				{
					handlers.AddHandler(typeof(VerticalStackLayout), typeof(LayoutHandler));
					handlers.AddHandler(typeof(Toolbar), typeof(ToolbarHandler));
					handlers.AddHandler(typeof(Button), typeof(ButtonHandler));
					handlers.AddHandler<Page, PageHandler>();
					handlers.AddHandler<Label, LabelHandler>();
					RegisterTabbedPageHandler(handlers);
					handlers.AddHandler(typeof(NavigationPage), typeof(NavigationViewHandler));
				});
			});

			var page1Content = new ContentPage { Title = "Tab1 Content", Content = new Label { Text = "Tab 1" } };
			var page2Content = new ContentPage { Title = "Tab2 Content", Content = new Label { Text = "Tab 2" } };
			var navPage1 = new NavigationPage(page1Content) { Title = "Tab 1" };
			var navPage2 = new NavigationPage(page2Content) { Title = "Tab 2" };

			bool page1Appeared = false;
			bool page1Disappeared = false;
			bool page2Appeared = false;
			bool page2Disappeared = false;

			page1Content.Appearing += (_, _) => page1Appeared = true;
			page1Content.Disappearing += (_, _) => page1Disappeared = true;
			page2Content.Appearing += (_, _) => page2Appeared = true;
			page2Content.Disappearing += (_, _) => page2Disappeared = true;

			var tabbedPage = CreateBasicTabbedPage(bottomTabs, isSmoothScrollEnabled, new Page[] { navPage1, navPage2 });

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(tabbedPage), async (handler) =>
			{
				// Tab 1 is initially selected — its content should have appeared
				await OnNavigatedToAsync(page1Content);
				Assert.True(page1Appeared, "Tab1 content should have appeared on initial load");

				// Switch to Tab 2
				page1Disappeared = false;
				page2Appeared = false;
				tabbedPage.CurrentPage = navPage2;
				await OnNavigatedToAsync(page2Content);
				await AssertEventually(() => page1Disappeared && page2Appeared);

				Assert.True(page1Disappeared, "Tab1 content should have disappeared after switching to Tab2");
				Assert.True(page2Appeared, "Tab2 content should have appeared after switching to Tab2");

				// Switch back to Tab 1
				page1Appeared = false;
				page2Disappeared = false;
				tabbedPage.CurrentPage = navPage1;
				await OnNavigatedToAsync(page1Content);
				await AssertEventually(() => page2Disappeared && page1Appeared);

				Assert.True(page2Disappeared, "Tab2 content should have disappeared after switching back to Tab1");
				Assert.True(page1Appeared, "Tab1 content should have appeared after switching back to Tab1");
			});
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task TabSwitchDoesNotDisconnectContentThatDisconnectsOnUnloaded(bool firstTabWrappedInNavigationPage)
		{
			SetupBuilder(builder => builder.ConfigureMauiHandlers(handlers =>
				handlers.AddHandler<DisconnectOnUnloadedView, DisconnectOnUnloadedHandler>()));

			var disconnectingView = new DisconnectOnUnloadedView();
			var firstTabContentPage = new ContentPage { Title = "Tab 1", Content = disconnectingView };
			Page firstTab = firstTabWrappedInNavigationPage
				? new NavigationPage(firstTabContentPage) { Title = "Tab 1" }
				: firstTabContentPage;
			var secondTab = new ContentPage { Title = "Tab 2", Content = new Label { Text = "Tab 2 Content" } };
			var tabbedPage = CreateBasicTabbedPage(pages: new[] { firstTab, secondTab });
			var expectedCurrentContent = firstTabWrappedInNavigationPage
				? ((NavigationPage)firstTab).CurrentPage
				: firstTab;

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(tabbedPage), async handler =>
			{
				await OnNavigatedToAsync(expectedCurrentContent);
				await OnLoadedAsync(disconnectingView);
				Assert.NotNull(disconnectingView.Handler);
				Assert.Equal(0, disconnectingView.UnloadedCount);
				var contentHandler = disconnectingView.Handler;
				var nativeContent = (UIView)contentHandler.PlatformView;

				for (var cycle = 0; cycle < 3; cycle++)
				{
					tabbedPage.CurrentPage = secondTab;
					await OnNavigatedToAsync(secondTab);
					await AssertEventually(() => nativeContent.Window is null);
					await Task.Delay(100);
					Assert.Equal(0, disconnectingView.UnloadedCount);
					Assert.Same(contentHandler, disconnectingView.Handler);
					Assert.Same(disconnectingView, contentHandler.VirtualView);

					var lateLoadedCount = 0;
					EventHandler lateLoaded = (_, _) => lateLoadedCount++;
					disconnectingView.Loaded += lateLoaded;
					Assert.Equal(1, lateLoadedCount);
					disconnectingView.Loaded -= lateLoaded;

					tabbedPage.CurrentPage = firstTab;
					await OnNavigatedToAsync(expectedCurrentContent);
					await OnLoadedAsync(disconnectingView);
					Assert.Same(contentHandler, disconnectingView.Handler);
					Assert.Equal(0, disconnectingView.UnloadedCount);
					Assert.Equal(1, disconnectingView.LoadedCount);
				}
			});
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task InactiveNavigationTabPushAndPopUnloadContent(bool animated)
		{
			SetupBuilder(builder => builder.ConfigureMauiHandlers(handlers =>
				handlers.AddHandler<DisconnectOnUnloadedView, DisconnectOnUnloadedHandler>()));
			var firstContent = new DisconnectOnUnloadedView();
			var firstPage = new ContentPage { Title = "First", Content = firstContent };
			var navigation = new NavigationPage(firstPage) { Title = "Navigation" };
			var otherTab = new ContentPage { Title = "Other" };
			var tabs = CreateBasicTabbedPage(pages: new Page[] { navigation, otherTab });

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(tabs), async handler =>
			{
				await OnLoadedAsync(firstContent);
				var firstNativeContent = (UIView)firstContent.Handler.PlatformView;
				tabs.CurrentPage = otherTab;
				await OnNavigatedToAsync(otherTab);
				await AssertEventually(() => firstNativeContent.Window is null);
				await Task.Delay(100);
				Assert.Equal(0, firstContent.UnloadedCount);

				var secondContent = new DisconnectOnUnloadedView();
				var secondPage = new ContentPage { Title = "Pushed", Content = secondContent };
				var push = navigation.PushAsync(secondPage, animated);
				await AssertEventually(() => firstContent.UnloadedCount == 1,
					message: "Inactive navigation push must unload the previous content");
				Assert.Same(otherTab, tabs.CurrentPage);
				Assert.Contains(firstPage, navigation.Navigation.NavigationStack);
				Assert.Same(navigation, firstPage.Parent);
				Assert.Null(firstContent.Handler);
				Assert.Equal(0, secondContent.LoadedCount);
				var firstReference = new WeakReference(firstContent);
				var replacementContent = new DisconnectOnUnloadedView();
				firstPage.Content = replacementContent;
				firstContent = null;
				await AssertionExtensions.WaitForGC(firstReference);
				GC.KeepAlive(navigation);

				tabs.CurrentPage = navigation;
				await push;
				await OnLoadedAsync(secondContent);
				var secondNativeContent = (UIView)secondContent.Handler.PlatformView;
				tabs.CurrentPage = otherTab;
				await OnNavigatedToAsync(otherTab);
				await AssertEventually(() => secondNativeContent.Window is null);
				await Task.Delay(100);
				Assert.Equal(0, secondContent.UnloadedCount);

				var pop = navigation.PopAsync(animated);
				await Task.Delay(100);
				// The renderer defers its logical pop until UIKit completes the transition.
				if (navigation.CurrentPage == firstPage)
				{
					await AssertEventually(() => secondContent.UnloadedCount == 1,
						message: "Inactive navigation pop must unload the removed content");
				}
				else
				{
					Assert.Same(secondPage, navigation.CurrentPage);
					Assert.Equal(0, secondContent.UnloadedCount);
				}
				Assert.Same(otherTab, tabs.CurrentPage);
				tabs.CurrentPage = navigation;
				Assert.Same(secondPage, await pop);
				await AssertEventually(() => secondContent.UnloadedCount == 1,
					message: "Completed navigation pop must unload the removed content");
				Assert.Null(secondContent.Handler);
				var reference = new WeakReference(secondContent);
				secondPage.Content = null;
				secondContent = null;
				await AssertionExtensions.WaitForGC(reference);
				GC.KeepAlive(navigation);

				await OnLoadedAsync(replacementContent);
				Assert.Equal(1, replacementContent.LoadedCount);
				tabs.CurrentPage = otherTab;
				await OnNavigatedToAsync(otherTab);
				await Task.Delay(100);
				Assert.Equal(0, replacementContent.UnloadedCount);
				firstPage.Content = null;
				await AssertEventually(() => replacementContent.UnloadedCount == 1);
				Assert.Null(replacementContent.Handler);
			});
		}

		[Theory]
		[InlineData(false, 0)]
		[InlineData(true, 0)]
		[InlineData(false, 1)]
		[InlineData(true, 1)]
		[InlineData(false, 2)]
		[InlineData(true, 2)]
		[InlineData(false, 3)]
		[InlineData(true, 3)]
		public async Task InactiveTabContentUnloadsOnRemoval(bool wrappedInNavigationPage, int removal)
		{
			SetupBuilder(builder => builder.ConfigureMauiHandlers(handlers =>
				handlers.AddHandler<DisconnectOnUnloadedView, DisconnectOnUnloadedHandler>()));
			var content = new DisconnectOnUnloadedView();
			var contentPage = new ContentPage { Title = "First", Content = content };
			Page firstTab = wrappedInNavigationPage
				? new NavigationPage(contentPage) { Title = "First" }
				: contentPage;
			var secondContent = new ContentPage { Title = "Second" };
			var thirdContent = new ContentPage { Title = "Third" };
			Page secondTab = wrappedInNavigationPage
				? new NavigationPage(secondContent) { Title = "Second" }
				: secondContent;
			Page thirdTab = wrappedInNavigationPage
				? new NavigationPage(thirdContent) { Title = "Third" }
				: thirdContent;
			var tabs = CreateBasicTabbedPage(pages: new[] { firstTab, secondTab, thirdTab });
			var window = new Window(tabs);

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(window, async handler =>
			{
				await OnLoadedAsync(content);
				var nativeContent = (UIView)content.Handler.PlatformView;
				tabs.CurrentPage = secondTab;
				await OnNavigatedToAsync(secondContent);
				await AssertEventually(() => nativeContent.Window is null);
				await Task.Delay(100);
				Assert.Equal(0, content.UnloadedCount);
				tabs.CurrentPage = thirdTab;
				await OnNavigatedToAsync(thirdContent);
				await Task.Delay(100);
				Assert.Equal(0, content.UnloadedCount);

				switch (removal)
				{
					case 0:
						contentPage.Content = null;
						break;
					case 1:
						tabs.Children.Remove(firstTab);
						break;
					case 2:
						window.Page = new ContentPage();
						break;
					case 3:
						((UIView)tabs.Handler.PlatformView).RemoveFromSuperview();
						break;
				}

				await AssertEventually(() => content.UnloadedCount == 1);
				if (removal != 3)
					Assert.Null(content.Window);
				Assert.Null(content.Handler);
			});
		}

		[Fact]
		public async Task NativeHostedContentStillLoadsAndUnloads()
		{
			SetupBuilder(builder => builder.ConfigureMauiHandlers(handlers =>
				handlers.AddHandler<DisconnectOnUnloadedView, DisconnectOnUnloadedHandler>()));
			var content = new DisconnectOnUnloadedView();

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(new ContentPage()), async handler =>
			{
				var nativeContent = content.ToPlatform(MauiContext);
				Assert.Null(content.Window);
				handler.PlatformView.AddSubview(nativeContent);
				await OnLoadedAsync(content);
				Assert.Equal(1, content.LoadedCount);
				Assert.Equal(0, content.UnloadedCount);

				nativeContent.RemoveFromSuperview();
				await AssertEventually(() => content.UnloadedCount == 1);
				Assert.Null(content.Handler);
			});
		}

		[Fact]
		public async Task RemovedInactiveTabContentDoesNotLeak()
		{
			SetupBuilder(builder => builder.ConfigureMauiHandlers(handlers =>
				handlers.AddHandler<DisconnectOnUnloadedView, DisconnectOnUnloadedHandler>()));
			var firstTab = new ContentPage { Title = "First" };
			var secondTab = new ContentPage { Title = "Second" };
			var tabs = CreateBasicTabbedPage(pages: new[] { firstTab, secondTab });
			var reference = new WeakReference(null);

			await CreateHandlerAndAddToWindow<WindowHandlerStub>(new Window(tabs), async handler =>
			{
				var content = new DisconnectOnUnloadedView();
				firstTab.Content = content;
				await OnLoadedAsync(content);
				reference.Target = content;
				tabs.CurrentPage = secondTab;
				await OnNavigatedToAsync(secondTab);
				await Task.Delay(100);
				Assert.Equal(0, content.UnloadedCount);

				firstTab.Content = null;
				await AssertEventually(() => content.UnloadedCount == 1);
				content = null;
				await AssertionExtensions.WaitForGC(reference);
				GC.KeepAlive(tabs);
			});
		}

		sealed class DisconnectOnUnloadedHandler : ContentViewHandler
		{
			UITapGestureRecognizer _gesture;

			protected override void ConnectHandler(Microsoft.Maui.Platform.ContentView platformView)
			{
				base.ConnectHandler(platformView);
				// Match custom controls whose native gesture target retains their handler.
				_gesture = new UITapGestureRecognizer(() => GC.KeepAlive(VirtualView));
				platformView.AddGestureRecognizer(_gesture);
			}

			protected override void DisconnectHandler(Microsoft.Maui.Platform.ContentView platformView)
			{
				platformView.RemoveGestureRecognizer(_gesture);
				_gesture.Dispose();
				_gesture = null;
				base.DisconnectHandler(platformView);
			}
		}

		sealed class DisconnectOnUnloadedView : Microsoft.Maui.Controls.ContentView
		{
			public int UnloadedCount { get; private set; }
			public int LoadedCount { get; private set; }

			public DisconnectOnUnloadedView()
			{
				Loaded += (_, _) => LoadedCount++;
				Unloaded += OnUnloaded;
			}

			void OnUnloaded(object sender, EventArgs e)
			{
				UnloadedCount++;
				Handler?.DisconnectHandler();
			}
		}
	}
}
