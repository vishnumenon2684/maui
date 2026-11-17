using System;
using System.Threading.Tasks;
using Android.Content;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Items;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
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

				for (int pass = 0; pass < 3; pass++)
				{
					await host.DuringNextLayout(() =>
					{
						Assert.True(recyclerView.IsInLayout);
						Assert.NotNull(recyclerView.FindViewHolderForAdapterPosition(0));
						recyclerView.LayoutRequests = 0;

						// Bypass MAUI's measure cache with the constraints supplied by a
						// vertical stack/scroll parent. Only the final geometry matters;
						// the adapter may retain the raw speculative measure constraint.
						handler.GetDesiredSize(240, constraint);
						handler.PlatformArrange(new Rect(0, 0, 240, 180));
					});

					await DrainEmptyViewLayoutCallbacks(host);
					Assert.Equal(0, recyclerView.LayoutRequests);
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
				collectionView.WidthRequest = 280;
				collectionView.HeightRequest = 240;
				recyclerView.LayoutParameters = new FrameLayout.LayoutParams(
					(int)recyclerView.Context.ToPixels(280), (int)recyclerView.Context.ToPixels(240));
				await DrainEmptyViewLayoutCallbacks(host);

				// Allocate native bounds first. OnSizeChanged must not mask a missing
				// empty-view update when the handler subsequently publishes the size.
				Assert.Equal(recyclerView.Context.ToPixels(280), recyclerView.Width, 1d);
				Assert.Equal(recyclerView.Context.ToPixels(240), recyclerView.Height, 1d);
				AssertEmptyViewGeometry(recyclerView, 240, 180);

				await host.DuringNextLayout(() =>
				{
					Assert.True(recyclerView.IsInLayout);
					handler.GetDesiredSize(280, double.PositiveInfinity);
					handler.PlatformArrange(new Rect(0, 0, 280, 240));
				});

				await DrainEmptyViewLayoutCallbacks(host);
				AssertEmptyViewGeometry(recyclerView, 280, 240);
			});
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task SpeculativelyMeasuredEmptyViewReturnsToFinalArrangeSize(bool useTemplate)
		{
			await WithEmptyViewLayoutProbe(useTemplate, async (collectionView, handler, host, recyclerView) =>
			{
				await host.DuringNextLayout(() =>
				{
					handler.GetDesiredSize(240, 500);

					// Unlike the stable-layout regression, a native child really has
					// consumed the speculative constraint. The parent stays 240x180.
					var emptyView = GetEmptyViewLayoutItem(recyclerView);
					emptyView.Measure(emptyView.MakeMeasureSpecExact(240), emptyView.MakeMeasureSpecExact(500));
					emptyView.Layout(0, 0, emptyView.MeasuredWidth, emptyView.MeasuredHeight);
					Assert.Equal(recyclerView.Context.ToPixels(500), emptyView.MeasuredHeight, 1d);
					Assert.Equal(recyclerView.Context.ToPixels(500), emptyView.Height, 1d);

					handler.PlatformArrange(new Rect(0, 0, 240, 180));
				});

				await DrainEmptyViewLayoutCallbacks(host);
				AssertEmptyViewGeometry(recyclerView, 240, 180);

				// A correction must settle, not introduce a request every frame.
				await host.DuringNextLayout(() =>
				{
					recyclerView.LayoutRequests = 0;
					handler.PlatformArrange(new Rect(0, 0, 240, 180));
				});
				await DrainEmptyViewLayoutCallbacks(host);
				Assert.Equal(0, recyclerView.LayoutRequests);
				AssertEmptyViewGeometry(recyclerView, 240, 180);
			});
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task EmptyViewContentChangesAtSameOuterSizeAreMeasured(bool useTemplate)
		{
			await WithEmptyViewLayoutProbe(useTemplate, async (collectionView, handler, host, recyclerView) =>
			{
				var item = Assert.IsAssignableFrom<ItemContentView>(GetEmptyViewLayoutItem(recyclerView));
				var label = Assert.IsType<Label>(item.View);
				var nativeLabel = Assert.IsAssignableFrom<TextView>(label.Handler.PlatformView);
				Assert.Equal(1, nativeLabel.LineCount);

				// Verify both growth and shrinkage of actual native text layout,
				// not just a mapped Text property or unchanged holder dimensions.
				foreach (string updatedText in new[] { "First line\nSecond line\nThird line", "Short again" })
				{
					label.Text = updatedText;
					handler.PlatformArrange(new Rect(0, 0, 240, 180));
					await DrainEmptyViewLayoutCallbacks(host);

					Assert.Equal(updatedText, nativeLabel.Text);
					Assert.Equal(updatedText.Split('\n').Length, nativeLabel.LineCount);
					AssertEmptyViewGeometry(recyclerView, 240, 180);
				}
			});
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task EmptyViewGeometrySurvivesAdapterChanges(bool useTemplate)
		{
			await WithEmptyViewLayoutProbe(useTemplate, async (collectionView, handler, host, recyclerView) =>
			{
				recyclerView.SetItemAnimator(null);
				collectionView.ItemTemplate = new DataTemplate(() =>
				{
					var label = new Label();
					label.SetBinding(Label.TextProperty, ".");
					return label;
				});
				handler.GetDesiredSize(240, 500);
				collectionView.ItemsSource = new[] { "First item" };
				await DrainEmptyViewLayoutCallbacks(host);
				Assert.IsNotType<EmptyViewAdapter>(recyclerView.GetAdapter());
				Assert.Equal("First item",
					Assert.IsType<TemplatedItemViewHolder>(recyclerView.FindViewHolderForAdapterPosition(0)).View.BindingContext);

				// Queue work for a not-yet-realized empty holder, then replace its
				// adapter before the next looper turn.
				collectionView.ItemsSource = Array.Empty<string>();
				Assert.Null(recyclerView.FindViewHolderForAdapterPosition(0));
				handler.PlatformArrange(new Rect(0, 0, 240, 180));
				collectionView.ItemsSource = new[] { "Replacement item" };
				await DrainEmptyViewLayoutCallbacks(host);
				Assert.IsNotType<EmptyViewAdapter>(recyclerView.GetAdapter());
				Assert.Equal("Replacement item",
					Assert.IsType<TemplatedItemViewHolder>(recyclerView.FindViewHolderForAdapterPosition(0)).View.BindingContext);

				collectionView.ItemsSource = Array.Empty<string>();
				await host.DuringNextLayout(() =>
				{
					handler.GetDesiredSize(240, double.PositiveInfinity);
					handler.PlatformArrange(new Rect(0, 0, 240, 180));
				});
				await DrainEmptyViewLayoutCallbacks(host);
				Assert.IsType<EmptyViewAdapter>(recyclerView.GetAdapter());
				AssertEmptyViewGeometry(recyclerView, 240, 180);
			});
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task EmptyViewWithHeaderFooterSettlesAtRemainingHeight(bool useTemplate)
		{
			await WithEmptyViewLayoutProbe(useTemplate, async (collectionView, handler, host, recyclerView) =>
			{
				await host.DuringNextLayout(() =>
				{
					recyclerView.LayoutRequests = 0;
					handler.GetDesiredSize(240, 500);
					handler.PlatformArrange(new Rect(0, 0, 240, 180));
				});
				await DrainEmptyViewLayoutCallbacks(host);

				Assert.Equal(0, recyclerView.LayoutRequests);
				AssertEmptyViewGeometry(recyclerView, 240, 130, position: 1);
				Assert.Equal(recyclerView.Context.ToPixels(20),
					recyclerView.FindViewHolderForAdapterPosition(0).ItemView.Height, 1d);
				Assert.Equal(recyclerView.Context.ToPixels(30),
					recyclerView.FindViewHolderForAdapterPosition(2).ItemView.Height, 1d);
			}, collectionView =>
			{
				collectionView.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical);
				collectionView.Header = useTemplate ? "Header" : new Label { Text = "Header", HeightRequest = 20 };
				collectionView.Footer = useTemplate ? "Footer" : new Label { Text = "Footer", HeightRequest = 30 };
				if (useTemplate)
				{
					collectionView.HeaderTemplate = new DataTemplate(() => new Label { Text = "Header", HeightRequest = 20 });
					collectionView.FooterTemplate = new DataTemplate(() => new Label { Text = "Footer", HeightRequest = 30 });
				}
			}, emptyHeight: 130, emptyPosition: 1);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public async Task EmptyViewNativeMinimumOverflowDoesNotKeepRequestingLayout(bool useTemplate)
		{
			await WithEmptyViewLayoutProbe(useTemplate, async (collectionView, handler, host, recyclerView) =>
			{
				var item = Assert.IsAssignableFrom<ItemContentView>(GetEmptyViewLayoutItem(recyclerView));
				var content = Assert.IsAssignableFrom<AView>(item.View.Handler.PlatformView);
				content.SetMinimumWidth((int)content.Context.ToPixels(320));
				content.SetMinimumHeight((int)content.Context.ToPixels(260));
				item.RequestLayout();
				await DrainEmptyViewLayoutCallbacks(host);
				AssertEmptyViewGeometry(recyclerView, 320, 260);

				for (int pass = 0; pass < 3; pass++)
				{
					await host.DuringNextLayout(() =>
					{
						recyclerView.LayoutRequests = 0;
						handler.GetDesiredSize(240, 500);
						handler.PlatformArrange(new Rect(0, 0, 240, 180));
					});
					await DrainEmptyViewLayoutCallbacks(host);
					Assert.Equal(0, recyclerView.LayoutRequests);
					AssertEmptyViewGeometry(recyclerView, 320, 260);
				}
			});
		}

		[Fact]
		public async Task DisconnectingWhileEmptyViewArrangeLayoutIsQueuedDoesNotCrash()
		{
			SetupBuilder();
			var host = new Grid();

			await CreateHandlerAndAddToWindow<LayoutHandler>(host, async _ =>
			{
				var collectionView = new CollectionView
				{
					ItemsSource = Array.Empty<string>(),
					EmptyView = new Label { Text = "Empty" }
				};
				host.Add(collectionView);
				var handler = Assert.IsType<CollectionViewHandler>(collectionView.Handler);
				var recyclerView = handler.PlatformView;
				Assert.True(recyclerView.IsAttachedToWindow);
				Assert.IsType<EmptyViewAdapter>(recyclerView.GetAdapter());

				Assert.Null(recyclerView.FindViewHolderForAdapterPosition(0));
				handler.PlatformArrange(new Rect(0, 0, 317, 241));
				host.Remove(collectionView);
				((IElementHandler)handler).DisconnectHandler();
				Assert.Null(((IElementHandler)handler).PlatformView);

				await DrainEmptyViewLayoutCallbacks(MauiContext.Context.GetActivity().Window.DecorView);
				Assert.Null(((IElementHandler)handler).PlatformView);
			});
		}

		async Task WithEmptyViewLayoutProbe(bool useTemplate,
			Func<CollectionView, EmptyViewLayoutProbeHandler, EmptyViewLayoutHost, EmptyViewLayoutRecyclerView, Task> test,
			Action<CollectionView> configure = null, double emptyHeight = 180, int emptyPosition = 0)
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
				configure?.Invoke(collectionView);
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
						AssertEmptyViewGeometry(recyclerView, 240, emptyHeight, emptyPosition);

						// Positive control: observe a holder request posted during native
						// layout, not just requests made synchronously by the test.
						await host.DuringNextLayout(() =>
						{
							Assert.True(recyclerView.IsInLayout);
							recyclerView.LayoutRequests = 0;
							recyclerView.Post(() =>
								recyclerView.FindViewHolderForAdapterPosition(emptyPosition).ItemView.RequestLayout());
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

		static AView GetEmptyViewLayoutItem(RecyclerView recyclerView, int position = 0)
		{
			return Assert.IsAssignableFrom<AView>(Assert.IsAssignableFrom<IMauiRecyclerViewEmptyView>(
				recyclerView.FindViewHolderForAdapterPosition(position)?.ItemView));
		}

		static void AssertEmptyViewGeometry(RecyclerView recyclerView, double width, double height, int position = 0)
		{
			var nativeView = GetEmptyViewLayoutItem(recyclerView, position);
			Assert.Equal(recyclerView.Context.ToPixels(width), nativeView.MeasuredWidth, 1d);
			Assert.Equal(recyclerView.Context.ToPixels(height), nativeView.MeasuredHeight, 1d);
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
