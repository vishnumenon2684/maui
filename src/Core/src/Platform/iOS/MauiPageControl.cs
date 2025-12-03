using System;
using System.Diagnostics.CodeAnalysis;
using CoreGraphics;
using ObjCRuntime;
using UIKit;

namespace Microsoft.Maui.Platform
{
	public class MauiPageControl : UIPageControl, IUIViewLifeCycleEvents
	{
		const int DefaultIndicatorSize = 6;

		WeakReference<IIndicatorView>? _indicatorView;
		bool _updatingPosition;
		CGRect _lastTemplatedIndicatorViewFrame = CGRect.Empty;

		public MauiPageControl()
		{
			ValueChanged += MauiPageControlValueChanged;
			if (OperatingSystem.IsIOSVersionAtLeast(14) || OperatingSystem.IsMacCatalystVersionAtLeast(14) || OperatingSystem.IsTvOSVersionAtLeast(14))
			{
				AllowsContinuousInteraction = false;
				BackgroundStyle = UIPageControlBackgroundStyle.Minimal;
			}
		}

		public void SetIndicatorView(IIndicatorView? indicatorView)
		{
			if (indicatorView == null)
			{
				ValueChanged -= MauiPageControlValueChanged;
			}
			_indicatorView = indicatorView is null ? null : new(indicatorView);

		}

		public bool IsSquare { get; set; }

		public double IndicatorSize { get; set; }

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				ValueChanged -= MauiPageControlValueChanged;

			base.Dispose(disposing);
		}


		public override void LayoutSubviews()
		{
			base.LayoutSubviews();

			if (Subviews.Length == 0)
				return;

			UpdateIndicatorSize();
			UpdateTemplatedIndicatorFrame();

			if (!IsSquare)
				return;

			UpdateSquareShape();
		}

		public void UpdateIndicatorSize()
		{
			if (IndicatorSize == 0 || IndicatorSize == DefaultIndicatorSize)
				return;

			float scale = (float)IndicatorSize / DefaultIndicatorSize;
			var newTransform = CGAffineTransform.MakeScale(scale, scale);

			Transform = newTransform;
		}

		public void UpdatePosition()
		{
			_updatingPosition = true;
			this.UpdateCurrentPage(GetCurrentPage());
			_updatingPosition = false;

			int GetCurrentPage()
			{
				if (_indicatorView is null || !_indicatorView.TryGetTarget(out var indicatorView))
					return -1;

				var maxVisible = indicatorView.GetMaximumVisible();
				var position = indicatorView.Position;
				var index = position >= maxVisible ? maxVisible - 1 : position;
				return index;
			}
		}

		public void UpdateIndicatorCount()
		{
			if (_indicatorView is null || !_indicatorView.TryGetTarget(out var indicatorView))
				return;
			this.UpdatePages(indicatorView.GetMaximumVisible());
			UpdatePosition();
		}

		void UpdateSquareShape()
		{
			if (!(OperatingSystem.IsIOSVersionAtLeast(14) || OperatingSystem.IsTvOSVersionAtLeast(14)))
			{
				UpdateCornerRadius();
				return;
			}

			var uiPageControlContentView = Subviews[0];
			if (uiPageControlContentView.Subviews.Length > 0)
			{
				var uiPageControlIndicatorContentView = uiPageControlContentView.Subviews[0];

				foreach (var view in uiPageControlIndicatorContentView.Subviews)
				{
					if (view is UIImageView imageview)
					{
						if (OperatingSystem.IsIOSVersionAtLeast(13) || OperatingSystem.IsTvOSVersionAtLeast(13))
							imageview.Image = UIImage.GetSystemImage("squareshape.fill");
						var frame = imageview.Frame;
						//the square shape is not the same size as the circle so we might need to correct the frame
						imageview.Frame = new CGRect(frame.X - 6, frame.Y, frame.Width, frame.Height);
					}
				}
			}
		}

		void UpdateCornerRadius()
		{
			foreach (var view in Subviews)
			{
				view.Layer.CornerRadius = 0;
			}
		}

		void MauiPageControlValueChanged(object? sender, System.EventArgs e)
		{
			if (_updatingPosition || _indicatorView is null || !_indicatorView.TryGetTarget(out var indicatorView))
				return;

			indicatorView.Position = (int)CurrentPage;
			//if we are iOS13 or lower and we are using a Square shape
			//we need to update the CornerRadius of the new shape.
			if (IsSquare && !(OperatingSystem.IsIOSVersionAtLeast(14) || OperatingSystem.IsTvOSVersionAtLeast(14)))
				LayoutSubviews();

		}

		/// <summary>
		/// Updates the frame of the templated indicator view to center it within the page control bounds.
		/// This is necessary to ensure custom indicator templates are properly positioned,
		/// especially when FlowDirection is RightToLeft.
		/// </summary>
		/// <remarks>
		/// Called during LayoutSubviews to adjust the frame only when it has changed,
		/// avoiding unnecessary layout updates. Uses SizeThatFits to determine the content's
		/// intrinsic size and centers it within the available bounds.
		/// </remarks>
		void UpdateTemplatedIndicatorFrame()
		{
			// Check if we have a valid indicator view with a template
			if (_indicatorView?.TryGetTarget(out var indicatorView) == true &&
				indicatorView is ITemplatedIndicatorView templatedView &&
				templatedView.IndicatorsLayoutOverride is not null)
			{
				// Get the native platform view for the template
				var handler = templatedView.IndicatorsLayoutOverride.Handler?.PlatformView as UIView;
				
				// Validate that we have a valid handler and non-zero bounds
				if (handler is not null && Bounds.Width > 0 && Bounds.Height > 0)
				{
					// Get the size that the template content wants to be
					var size = handler.SizeThatFits(Bounds.Size);
					
					// Center the content within the page control bounds
					var x = (Bounds.Width - size.Width) / 2;
					var y = (Bounds.Height - size.Height) / 2;
					var newFrame = new CGRect(x, y, size.Width, size.Height);

					// Only update if the frame has actually changed to avoid unnecessary layout work
					if (!newFrame.Equals(_lastTemplatedIndicatorViewFrame))
					{
						handler.Frame = newFrame;
						_lastTemplatedIndicatorViewFrame = newFrame;
					}
				}
			}
		}

		[UnconditionalSuppressMessage("Memory", "MEM0002", Justification = IUIViewLifeCycleEvents.UnconditionalSuppressMessage)]
		EventHandler? _movedToWindow;
		event EventHandler IUIViewLifeCycleEvents.MovedToWindow
		{
			add => _movedToWindow += value;
			remove => _movedToWindow -= value;
		}

		public override void MovedToWindow()
		{
			_movedToWindow?.Invoke(this, EventArgs.Empty);
		}
	}
}
