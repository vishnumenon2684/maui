using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;
using Xunit;
using static Microsoft.Maui.DeviceTests.AssertHelpers;
#if ANDROID
using NativeEditor = AndroidX.AppCompat.Widget.AppCompatEditText;
#elif IOS || MACCATALYST
using NativeEditor = UIKit.UITextView;
#elif WINDOWS
using NativeEditor = Microsoft.UI.Xaml.Controls.RichEditBox;
#endif

namespace Microsoft.Maui.DeviceTests
{
	[Category("RichEditor")]
	public partial class RichEditorTests : ControlsHandlerTestBase
	{
		void SetupBuilder() => EnsureHandlerCreated(builder =>
			builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<RichEditor, RichEditorHandler>()));

		[Fact]
		public async Task NativeReplacementPreservesRichStyleAndUndo()
		{
			SetupBuilder();
			var editor = new RichEditor { Document = RichTextDocument.FromText("bold", new RichTextStyle(bold: true)) };
			await AttachAndRun<RichEditorHandler>(editor, async handler =>
			{
				editor.Select(4, 0);
				ReplaceNativeText(handler.PlatformView, 4, 0, "!");
				await AssertEventually(() => editor.Document.Text == "bold!");
				Assert.True(editor.Document.Paragraphs[0].Runs[0].Style.Bold);
				editor.Undo();
				Assert.Equal("bold", NativeText(handler));
				editor.Redo();
				Assert.Equal("bold!", NativeText(handler));
			});
		}

		[Fact]
		public async Task NativeEnterContinuesAndTerminatesList()
		{
			SetupBuilder();
			var editor = new RichEditor { Document = RichTextDocument.FromText("item") };
			await AttachAndRun<RichEditorHandler>(editor, async handler =>
			{
				editor.ApplyList(RichTextListKind.Numbered);
				editor.Select(4, 0);
				ReplaceNativeText(handler.PlatformView, 4, 0, "\n");
				await AssertEventually(() => editor.Document.Paragraphs.Count == 2);
				Assert.Equal(RichTextListKind.Numbered, editor.Document.Paragraphs[1].ListKind);
				ReplaceNativeText(handler.PlatformView, 5, 0, "\n");
				await AssertEventually(() => editor.Document.Paragraphs[1].ListKind == RichTextListKind.None);
				Assert.Equal("item\n", NativeText(handler));
				Assert.Equal(5, editor.CursorPosition);
			});
		}

		[Fact]
		public async Task DisconnectStopsNativeEditsFromChangingDocument()
		{
			SetupBuilder();
			var editor = new RichEditor { Document = RichTextDocument.FromText("original") };
			var handler = await CreateHandlerAsync<RichEditorHandler>(editor);
			await InvokeOnMainThreadAsync(() =>
			{
				var nativeView = handler.PlatformView;
				((IElementHandler)handler).DisconnectHandler();
				ReplaceNativeText(nativeView, 0, 8, "detached");
			});
			await InvokeOnMainThreadAsync(() => Assert.Equal("original", editor.Document.Text));
		}

		static string NativeText(RichEditorHandler handler)
		{
#if ANDROID || IOS || MACCATALYST
			return (handler.PlatformView.Text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
#elif WINDOWS
			handler.PlatformView.Document.GetText(Microsoft.UI.Text.TextGetOptions.NoHidden, out string text);
			text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
			return text.EndsWith("\n", StringComparison.Ordinal) ? text.Substring(0, text.Length - 1) : text;
#endif
		}

		static void ReplaceNativeText(NativeEditor nativeView, int start, int length, string text)
		{
#if ANDROID
			using var inserted = new Java.Lang.String(text);
			nativeView.EditableText!.Replace(start, start + length, inserted);
			nativeView.SetSelection(start + text.Length);
#elif IOS || MACCATALYST
			using var replacement = new Foundation.NSAttributedString(text);
			nativeView.TextStorage.Replace(new Foundation.NSRange(start, length), replacement);
			nativeView.SelectedRange = new Foundation.NSRange(start + text.Length, 0);
			nativeView.Delegate?.Changed(nativeView);
#elif WINDOWS
			var range = nativeView.Document.GetRange(start, start + length);
			range.Text = text.Replace('\n', '\r');
			nativeView.Document.Selection.SetRange(start + text.Length, start + text.Length);
#endif
		}
	}
}
