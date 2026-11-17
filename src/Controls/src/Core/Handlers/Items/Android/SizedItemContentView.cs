#nullable disable
using System;
using Android.Content;
using Android.Views;

namespace Microsoft.Maui.Controls.Handlers.Items
{
	internal class SizedItemContentView : ItemContentView
	{
		protected readonly Func<double> _width;
		protected readonly Func<double> _height;

		public SizedItemContentView(Context context, Func<double> width, Func<double> height)
			: base(context)
		{
			_width = width;
			_height = height;
		}

		protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
		{
			if (Content == null)
			{
				SetMeasuredDimension(0, 0);
				return;
			}

			double targetWidth = NormalizeDimension(_width());
			double targetHeight = NormalizeDimension(_height());

			if (!double.IsInfinity(targetWidth))
				targetWidth = Context.FromPixels(targetWidth);

			if (!double.IsInfinity(targetHeight))
				targetHeight = Context.FromPixels(targetHeight);

			if (Content.VirtualView.Handler is IPlatformViewHandler pvh)
			{
				var widthSpec = Context.CreateMeasureSpec(targetWidth,
					double.IsInfinity(targetWidth) ? double.NaN : targetWidth
					, minimumSize: double.NaN, maximumSize: targetWidth);

				var heightSpec = Context.CreateMeasureSpec(targetHeight, double.IsInfinity(targetHeight) ? double.NaN : targetHeight
					, minimumSize: double.NaN, maximumSize: targetHeight);

				var size = pvh.MeasureVirtualView(widthSpec, heightSpec);

				SetMeasuredDimension((int)size.Width, (int)size.Height);
			}
		}

		static double NormalizeDimension(double value) => value == int.MaxValue ? double.PositiveInfinity : value;
	}

	internal class EmptyViewContentView : SizedItemContentView, IMauiRecyclerViewEmptyView
	{
		bool _layoutCheckPending;

		public EmptyViewContentView(Context context, Func<double> width, Func<double> height)
			: base(context, width, height)
		{
		}

		internal void RequestLayoutIfSizeChanged()
		{
			if (_layoutCheckPending)
				return;

			// Measure and arrange can supply different constraints in the same pass.
			// Check the resulting geometry once both have finished.
			_layoutCheckPending = Post(CheckLayoutSize);
		}

		void CheckLayoutSize()
		{
			_layoutCheckPending = false;
			if (!this.IsAlive() || Content?.PlatformView is not { } platformView || !platformView.IsAlive())
				return;

			// Match MeasureVirtualView, where the content's native minimum size wins.
			var width = Math.Max(_width(), platformView.MinimumWidth);
			var height = Math.Max(_height(), platformView.MinimumHeight);
			if (MeasuredWidth != width || Width != width || MeasuredHeight != height || Height != height)
				PlatformInterop.RequestLayoutIfNeeded(this);
		}
	}
}
