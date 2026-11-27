using System;
using System.Collections.Generic;
using System.Text;
#if IOS || MACCATALYST
using PlatformView = UIKit.UIView;
#elif MONOANDROID
using PlatformView = Android.Views.View;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.FrameworkElement;
#endif

namespace Microsoft.Maui.Controls
{
	public partial class VisualElement
	{
		IDisposable? _loadedUnloadedToken;
#if IOS || MACCATALYST
		bool _retainingTabContent;
#endif
		partial void HandlePlatformUnloadedLoaded()
		{
#if IOS || MACCATALYST
			bool wasRetainingTabContent = _retainingTabContent;
#endif
			_loadedUnloadedToken?.Dispose();
			_loadedUnloadedToken = null;

#if IOS || MACCATALYST
			if (GetRetainedTab(wasRetainingTabContent) is TabbedPage retainedTab)
			{
				// UIKit detaches inactive tabs without removing their content. Wait for either
				// reattachment or the tab container unloading; the child won't move again on teardown.
				var weakElement = new WeakReference<VisualElement>(this);
				bool disposed = false;
				EventHandler unloaded = (_, _) =>
				{
					retainedTab.Dispatcher.Dispatch(() =>
					{
						if (!disposed && weakElement.TryGetTarget(out var element))
							element.HandlePlatformUnloadedLoaded();
					});
				};
				retainedTab.Unloaded += unloaded;
				// The selected page still receives native detach notifications on container teardown.
				var selectedTab = GetSelectedTabContent();
				bool watchSelectedLoaded = selectedTab?.Handler?.PlatformView is not PlatformView selectedView ||
					selectedView.Window is null;
				if (selectedTab is not null)
				{
					selectedTab.Unloaded += unloaded;
					if (watchSelectedLoaded)
						selectedTab.Loaded += unloaded;
				}
				System.ComponentModel.PropertyChangedEventHandler selectionChanged = (_, e) =>
				{
					if (e.PropertyName != nameof(TabbedPage.CurrentPage))
						return;

					// Rewire after UIKit finishes replacing the selected native tab or navigation page.
					unloaded(null, EventArgs.Empty);
				};
				retainedTab.PropertyChanged += selectionChanged;
				var navigationAncestors = new List<NavigationPage>();
				for (var parent = Parent; parent is not null && parent != retainedTab; parent = parent.Parent)
				{
					if (parent is NavigationPage navigation)
					{
						navigationAncestors.Add(navigation);
						navigation.PropertyChanged += selectionChanged;
					}
				}
				var loadedToken = this.OnLoaded(HandlePlatformUnloadedLoaded);
				_loadedUnloadedToken = new ActionDisposable(() =>
				{
					disposed = true;
					_retainingTabContent = false;
					loadedToken.Dispose();
					retainedTab.PropertyChanged -= selectionChanged;
					foreach (var navigation in navigationAncestors)
						navigation.PropertyChanged -= selectionChanged;
					retainedTab.Unloaded -= unloaded;
					if (selectedTab is not null)
					{
						selectedTab.Unloaded -= unloaded;
						if (watchSelectedLoaded)
							selectedTab.Loaded -= unloaded;
					}
				});
				_retainingTabContent = true;
				return;

				Page? GetSelectedTabContent()
				{
					var page = retainedTab.CurrentPage;
					while (page is NavigationPage navigation)
						page = navigation.CurrentPage;
					for (Element? parent = this; parent is not null; parent = parent.Parent)
					{
						if (parent == page)
							return null;
					}
					return page;
				}
			}
#endif

			// Window and this VisualElement both have a handler to work with
			if (Window?.Handler?.PlatformView is not null &&
				Handler?.PlatformView is PlatformView view)
			{
				if (view.IsLoaded())
				{
					SendLoaded(false);

					// If SendLoaded caused the unloaded tokens to wire up
					_loadedUnloadedToken?.Dispose();
					_loadedUnloadedToken = null;
					_loadedUnloadedToken = this.OnUnloaded(OnPlatformUnloaded);
				}
				else
				{
					SendUnloaded(false);

					// If SendUnloaded caused the unloaded tokens to wire up
					_loadedUnloadedToken?.Dispose();
					_loadedUnloadedToken = null;
					if (Handler is not null)
					{
						_loadedUnloadedToken = this.OnLoaded(SendLoaded);
					}
				}
			}
			// This VisualElement has a handler with MauiContext but no MAUI Window.
			// This happens when the view is hosted inside a native container via ToPlatform().
			// We still need to fire Loaded/Unloaded based on the platform view's attach state.
			else if (Window is null &&
				Handler?.MauiContext is not null &&
				Handler?.PlatformView is PlatformView nativeHostedView)
			{
				if (nativeHostedView.IsLoaded())
				{
					SendLoaded(false);

					// If SendLoaded caused the unloaded tokens to wire up
					_loadedUnloadedToken?.Dispose();
					_loadedUnloadedToken = null;
					_loadedUnloadedToken = this.OnUnloaded(OnPlatformUnloaded);
				}
				else
				{
#if IOS || MACCATALYST
					SendUnloaded(false);
#endif
					// Not yet attached to a native parent; wait for the platform loaded event
					_loadedUnloadedToken?.Dispose();
					_loadedUnloadedToken = null;
					if (Handler is not null)
					{
						_loadedUnloadedToken = this.OnLoaded(SendLoaded);
					}
				}
			}
			else
			{
				// My handler is still set but the window handler isn't set.
				// This means I'm starting to detach from the platform window
				// So we wait for the platform detach events to fire before calling 
				// OnUnloaded
				if (Handler?.PlatformView is PlatformView detachingView &&
					detachingView.IsLoaded())
				{
					_loadedUnloadedToken = this.OnUnloaded(SendUnloaded);
				}
				else
				{
					SendUnloaded();
				}
			}
		}

		void OnPlatformUnloaded()
		{
#if IOS || MACCATALYST
			if (GetRetainedTab() is not null)
			{
				HandlePlatformUnloadedLoaded();
				return;
			}
#endif
			SendUnloaded();
		}

#if IOS || MACCATALYST
		TabbedPage? GetRetainedTab(bool wasRetainingTabContent = false)
		{
			if (!_isLoadedFired || Window is null || Handler?.PlatformView is not PlatformView view ||
				view.Window is not null)
				return null;

			Element child = this;
			for (var parent = Parent; parent is not null; child = parent, parent = parent.Parent)
			{
				// A page removed from a navigation stack is not retained tab content.
				if (parent is NavigationPage navigation && navigation.CurrentPage != child)
					return null;

				if (parent is TabbedPage tabs)
				{
					if ((!wasRetainingTabContent && tabs.CurrentPage == child) || !tabs.Children.Contains(child as Page) ||
						tabs.Handler?.PlatformView is not PlatformView tabView || tabView.Window is null)
						return null;

					// Logical ancestry alone isn't enough: independently removed native content
					// must still unload, even if its managed parent has not been updated yet.
					if (child.Handler?.PlatformView is PlatformView tabContent &&
						(view == tabContent || view.IsDescendantOfView(tabContent)))
						return tabs;

					return null;
				}
			}

			return null;
		}
#endif
	}
}
