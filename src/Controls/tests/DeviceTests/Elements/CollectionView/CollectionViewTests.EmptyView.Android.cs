using System;
using System.Threading.Tasks;
using Android.Content;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Items;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using Xunit;
using AView = Android.Views.View;

namespace Microsoft.Maui.DeviceTests
{
	public partial class CollectionViewTests
	{
		[Theory]
		[InlineData(false, false)]
		[InlineData(false, true)]
		[InlineData(true, false)]
		[InlineData(true, true)]
		public async Task FixedHeightEmptyViewDoesNotRequestParentLayout(bool useTemplate, bool infiniteConstraint)
		{
			await WithEmptyViewLayoutProbe(useTemplate, async (collectionView, handler, host, recyclerView) =>
			{
				double constraint = infiniteConstraint ? double.PositiveInfinity : 500;
				var adapter = Assert.IsType<EmptyViewAdapter>(recyclerView.GetAdapter());

				for (int pass = 0; pass < 3; pass++)
				{
					double measuredHeight = 0;
					await host.DuringNextLayout(() =>
					{
						Assert.True(recyclerView.IsInLayout);
						Assert.NotNull(recyclerView.FindViewHolderForAdapterPosition(0));
						recyclerView.LayoutRequests = 0;

						// Model the constraints supplied by a vertical stack/scroll parent,
						// while bypassing MAUI's measure cache on every pass.
						handler.GetDesiredSize(240, constraint);
						measuredHeight = adapter.RecyclerViewHeight;
						handler.PlatformArrange(new Rect(0, 0, 240, 180));
					});

					// RequestLayoutIfNeeded and the missing-holder path both post work.
					// Inspecting IsLayoutRequested inside OnLayout would miss that work.
					await DrainEmptyViewLayoutCallbacks(host);
					Assert.Equal(0, recyclerView.LayoutRequests);
					Assert.Equal(recyclerView.Context.ToPixels(180), measuredHeight, 1d);
					AssertEmptyViewGeometry(recyclerView, 240, 180);
				}
			});
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task EmptyViewGeometryUpdatesAfterRealResize(bool useTemplate)
		{
			await WithEmptyViewLayoutProbe(useTemplate, async (collectionView, handler, host, recyclerView) =>
			{
				AssertEmptyViewGeometry(recyclerView, 240, 180);

				collectionView.WidthRequest = 280;
				collectionView.HeightRequest = 240;
				recyclerView.LayoutParameters = new FrameLayout.LayoutParams(
					(int)recyclerView.Context.ToPixels(280), (int)recyclerView.Context.ToPixels(240));
				await DrainEmptyViewLayoutCallbacks(host);

				// Allocate the native bounds before notifying the handler. OnSizeChanged
				// remeasures using the old adapter dimensions; it must not mask a missing
				// empty-view layout request when the handler updates those dimensions.
				Assert.Equal(recyclerView.Context.ToPixels(280), recyclerView.Width, 1d);
				Assert.Equal(recyclerView.Context.ToPixels(240), recyclerView.Height, 1d);
				AssertEmptyViewGeometry(recyclerView, 240, 180);

				await host.DuringNextLayout(() =>
				{
					Assert.True(recyclerView.IsInLayout);
					recyclerView.LayoutRequests = 0;
					handler.GetDesiredSize(280, double.PositiveInfinity);
					handler.PlatformArrange(new Rect(0, 0, 280, 240));
				});

				await DrainEmptyViewLayoutCallbacks(host);
				Assert.True(recyclerView.LayoutRequests > 0,
					"Changed empty-view dimensions must request layout after the native layout pass.");
				AssertEmptyViewGeometry(recyclerView, 280, 240);
			});
		}

		async Task WithEmptyViewLayoutProbe(bool useTemplate,
			Func<CollectionView, EmptyViewLayoutProbeHandler, EmptyViewLayoutHost, EmptyViewLayoutRecyclerView, Task> test)
		{
			SetupBuilder();
			await InvokeOnMainThreadAsync(async () =>
			{
				var collectionView = new CollectionView
				{
					WidthRequest = 240,
					HeightRequest = 180,
					ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Horizontal),
					ItemsSource = Array.Empty<string>(),
					EmptyView = useTemplate ? "Empty" : new Label { Text = "Empty" },
					EmptyViewTemplate = useTemplate ? new DataTemplate(() => new Label { Text = "Empty" }) : null
				};
				var handler = CreateHandler<EmptyViewLayoutProbeHandler>(collectionView);
				var recyclerView = Assert.IsType<EmptyViewLayoutRecyclerView>(handler.PlatformView);
				using var host = new EmptyViewLayoutHost(recyclerView.Context);
				host.AddView(recyclerView, new FrameLayout.LayoutParams(
					(int)recyclerView.Context.ToPixels(240), (int)recyclerView.Context.ToPixels(180)));
				handler.PlatformArrange(new Rect(0, 0, 240, 180));

				try
				{
					await host.AttachAndRun(async () =>
					{
						await DrainEmptyViewLayoutCallbacks(host);
						Assert.True(recyclerView.IsAttachedToWindow);
						AssertEmptyViewGeometry(recyclerView, 240, 180);

						// Positive control: observe a holder request posted during native layout,
						// not just requests made synchronously by the test.
						await host.DuringNextLayout(() =>
						{
							Assert.True(recyclerView.IsInLayout);
							recyclerView.LayoutRequests = 0;
							recyclerView.Post(() =>
								recyclerView.FindViewHolderForAdapterPosition(0).ItemView.RequestLayout());
						});
						await DrainEmptyViewLayoutCallbacks(host);
						Assert.True(recyclerView.LayoutRequests > 0);

						await test(collectionView, handler, host, recyclerView);
					});
				}
				finally
				{
					host.RemoveView(recyclerView);
					((IElementHandler)handler).DisconnectHandler();
				}
			});
		}

		static void AssertEmptyViewGeometry(RecyclerView recyclerView, double width, double height)
		{
			var itemView = Assert.IsAssignableFrom<IMauiRecyclerViewEmptyView>(
				recyclerView.FindViewHolderForAdapterPosition(0)?.ItemView);
			var nativeView = Assert.IsAssignableFrom<AView>(itemView);
			Assert.Equal(recyclerView.Context.ToPixels(width), nativeView.Width, 1d);
			Assert.Equal(recyclerView.Context.ToPixels(height), nativeView.Height, 1d);
		}

		static async Task DrainEmptyViewLayoutCallbacks(AView view)
		{
			for (int frame = 0; frame < 2; frame++)
			{
				var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				using var callback = new Java.Lang.Runnable(() => view.Post(() => completion.TrySetResult(true)));
				view.PostOnAnimation(callback);
				await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
			}
		}

		class EmptyViewLayoutProbeHandler : CollectionViewHandler
		{
			public EmptyViewLayoutProbeHandler()
			{
			}

			protected override RecyclerView CreatePlatformView() =>
				new EmptyViewLayoutRecyclerView(Context, GetItemsLayout, CreateAdapter);
		}

		class EmptyViewLayoutRecyclerView : MauiRecyclerView<ReorderableItemsView,
			GroupableItemsViewAdapter<ReorderableItemsView, IGroupableItemsViewSource>, IGroupableItemsViewSource>
		{
			public int LayoutRequests { get; set; }

			public EmptyViewLayoutRecyclerView(Context context, Func<IItemsLayout> getItemsLayout,
				Func<GroupableItemsViewAdapter<ReorderableItemsView, IGroupableItemsViewSource>> createAdapter)
				: base(context, getItemsLayout, createAdapter)
			{
			}

			public override void RequestLayout()
			{
				LayoutRequests++;
				base.RequestLayout();
			}
		}

		class EmptyViewLayoutHost : FrameLayout
		{
			Action _duringLayout;

			public EmptyViewLayoutHost(Context context) : base(context)
			{
			}

			public async Task DuringNextLayout(Action action)
			{
				var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				_duringLayout = () =>
				{
					try
					{
						action();
						completion.TrySetResult(true);
					}
					catch (Exception exception)
					{
						completion.TrySetException(exception);
					}
				};
				RequestLayout();
				try
				{
					await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
				}
				finally
				{
					_duringLayout = null;
				}
			}

			protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
			{
				base.OnLayout(changed, left, top, right, bottom);
				var action = _duringLayout;
				_duringLayout = null;
				action?.Invoke();
			}
		}
	}
}
