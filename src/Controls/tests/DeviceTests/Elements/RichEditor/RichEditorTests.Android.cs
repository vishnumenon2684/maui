using System;
using System.Threading.Tasks;
using Android.Content;
using Android.Text;
using Android.Text.Style;
using Android.Views.InputMethods;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;
using Xunit;
using static Microsoft.Maui.DeviceTests.AssertHelpers;

namespace Microsoft.Maui.DeviceTests
{
	[Category("RichEditor")]
	public class RichEditorAndroidTests : ControlsHandlerTestBase
	{
		void SetupBuilder() => EnsureHandlerCreated(builder =>
			builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<RichEditor, RichEditorHandler>()));

		[Fact]
		public async Task CompositionCommitsAsOneStyledUndoTransaction()
		{
			SetupBuilder();
			var editor = new RichEditor();
			editor.ApplyBold();
			await AttachAndRun<RichEditorHandler>(editor, async handler =>
			{
				handler.PlatformView.RequestFocus();
				using var info = new EditorInfo();
				using var connection = handler.PlatformView.OnCreateInputConnection(info);
				Assert.NotNull(connection);
				using var intermediate = new Java.Lang.String("compos");
				Assert.True(connection.SetComposingText(intermediate, 1));
				await Task.Yield();
				Assert.Equal("", editor.Document.Text);
				using var final = new Java.Lang.String("composed");
				Assert.True(connection.CommitText(final, 1));
				await AssertEventually(() => editor.Document.Text == "composed");
				Assert.True(editor.Document.Paragraphs[0].Runs[0].Style.Bold);
				editor.Undo();
				Assert.Equal("", handler.PlatformView.Text);
			});
		}

		[Fact]
		public async Task NativeSpansReflectFormattingAndRestoreDefaultSize()
		{
			SetupBuilder();
			var editor = new RichEditor { Document = RichTextDocument.FromText("format") };
			await AttachAndRun<RichEditorHandler>(editor, handler =>
			{
				float defaultSize = handler.PlatformView.TextSize;
				editor.Select(0, 6);
				editor.ApplyBold();
				editor.ApplyUnderline();
				editor.ApplyFont(null, 24);
				AssertSpan(handler, true, 24 * handler.PlatformView.Resources!.DisplayMetrics!.ScaledDensity);
				editor.ApplyFont(null, null);
				AssertSpan(handler, true, defaultSize);
			});
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task ReadOnlyAndDisabledRejectInputConnectionEdits(bool readOnly)
		{
			SetupBuilder();
			var editor = new RichEditor { Document = RichTextDocument.FromText("original") };
			await AttachAndRun<RichEditorHandler>(editor, handler =>
			{
				using var info = new EditorInfo();
				using var connection = handler.PlatformView.OnCreateInputConnection(info);
				Assert.NotNull(connection);
				editor.IsReadOnly = readOnly;
				editor.IsEnabled = readOnly;
				using var inserted = new Java.Lang.String("changed");
				Assert.False(connection.CommitText(inserted, 1));
				Assert.False(connection.SetComposingText(inserted, 1));
				Assert.False(connection.DeleteSurroundingText(1, 1));
				Assert.False(connection.DeleteSurroundingTextInCodePoints(1, 1));
				handler.PlatformView.SetSelection(0, 8);
				Assert.True(handler.PlatformView.OnTextContextMenuItem(global::Android.Resource.Id.Cut));
				Assert.Equal("original", handler.PlatformView.Text);
				editor.IsReadOnly = false;
				editor.IsEnabled = true;
				Assert.NotNull(handler.PlatformView.KeyListener);
			});
		}

		static void AssertSpan(RichEditorHandler handler, bool underline, float size)
		{
			using var spanClass = Java.Lang.Class.FromType(typeof(MetricAffectingSpan));
			var spans = handler.PlatformView.EditableText!.GetSpans(0, 6, spanClass);
			Assert.NotEmpty(spans);
			using var paint = new TextPaint(handler.PlatformView.Paint);
			for (int i = 0; i < spans.Length; i++)
			{
				if (spans[i] is MetricAffectingSpan span)
					span.UpdateDrawState(paint);
			}
			Assert.True(paint.Typeface!.IsBold);
			Assert.Equal(underline, paint.UnderlineText);
			Assert.Equal(size, paint.TextSize, 0.1f);
		}
	}
}
