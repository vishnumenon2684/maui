#nullable disable
using System;
using System.ComponentModel;

namespace Microsoft.Maui.Controls.Internals
{
	/// <include file="../../../docs/Microsoft.Maui.Controls.Internals/PageExtensions.xml" path="Type[@FullName='Microsoft.Maui.Controls.Internals.PageExtensions']/Docs/*" />
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static class PageExtensions
	{
		/// <include file="../../../docs/Microsoft.Maui.Controls.Internals/PageExtensions.xml" path="//Member[@MemberName='AncestorToRoot']/Docs/*" />
		public static Page AncestorToRoot(this Page page)
		{
			Element parent = page;
			while (!Application.IsApplicationOrWindowOrNull(parent.RealParent))
				parent = parent.RealParent;

			return parent as Page;
		}
		
#nullable enable

		// TODO .NET9 we should reuse this logic for TabbedPage and FlyoutPage
		// We didn't have Loaded behavior yet when navigated was added to those so they currently all fire way too early in the lifecycle
		// we could pretty easily reuse this now inside TabbbedPage and FlyoutPage to more accurately fire navigation events
		internal static IDisposable? HandleNavigatedEventsForNavigatingTo(this Page? oldPage, Page? newPage)
		{
			if (oldPage is null && newPage is null)
			{
				return null;
			}

			IDisposable? unloaded = null;
			IDisposable? loaded = null;
			if (oldPage is not null && oldPage.HasNavigatedTo)
			{
				oldPage?.SendNavigatingFrom(new NavigatingFromEventArgs());
			}

			//if the old page doesn't have a handler at all
			// There's not a PlatformView for us to watch for unloaded
			if (oldPage?.Handler?.PlatformView is not null)
			{
				unloaded = oldPage?.OnUnloaded(() =>
				{
					if (oldPage is not null && oldPage.HasNavigatedTo)
					{
						oldPage?.SendNavigatedFrom(new NavigatedFromEventArgs(newPage));
					}

					unloaded?.Dispose();
					unloaded = null;
				});
			}
			else
			{
				oldPage?.SendNavigatedFrom(new NavigatedFromEventArgs(newPage));
			}

			if (newPage is not null)
			{
				EventHandler onLoaded = (object? sender, EventArgs args) =>
				{
					if (sender is Page page && !page.HasNavigatedTo)
					{
						(sender as Page)?.SendNavigatedTo(new NavigatedToEventArgs(oldPage));
					}

					loaded?.Dispose();
					loaded = null;
				};
				
				newPage.Loaded += onLoaded;
				loaded = new ActionDisposable(() => newPage.Loaded -= onLoaded);
			}

			return new ActionDisposable(() =>
			{
				unloaded?.Dispose();
				loaded?.Dispose();
				unloaded = null;
				loaded = null;
			});
		}
	}
}
