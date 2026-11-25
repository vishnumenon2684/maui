using System;
using System.Threading.Tasks;
using Foundation;
using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Handlers;
using UIKit;
using Xunit;

namespace Microsoft.Maui.DeviceTests
{
	[Category("RichEditor")]
	public class RichEditorHandlerTests : CoreHandlerTestBase<RichEditorHandler, RichEditorHandlerTests.RichEditorStub>
	{
		[Fact]
		public Task ListsKeepLogicalTextAndFormattingUpdatesPreserveSelection() =>
			InvokeOnMainThreadAsync(() =>
			{
				var editor = new RichEditorStub
				{
					Document = CreateDocument(new RichTextStyle(bold: true, italic: true, fontSize: 24)),
					CursorPosition = 1,
					SelectionLength = 2
				};
				var handler = CreateHandler(editor);
				try
				{
					var native = handler.PlatformView;
					Assert.Equal("alpha\nbeta", native.Text);
					var font = (UIFont)native.TextStorage.GetAttribute(UIStringAttributeKey.Font, 0, out _);
					Assert.True(font.FontDescriptor.SymbolicTraits.HasFlag(UIFontDescriptorSymbolicTraits.Bold));
					Assert.True(font.FontDescriptor.SymbolicTraits.HasFlag(UIFontDescriptorSymbolicTraits.Italic));
					Assert.Equal(24, (double)font.PointSize);
					var paragraph = (NSParagraphStyle)native.TextStorage.GetAttribute(UIStringAttributeKey.ParagraphStyle, 0, out _);
					Assert.True(paragraph.HeadIndent > 0);

					editor.Document = CreateDocument(new RichTextStyle(underline: true, fontSize: 18));
					handler.UpdateValue(nameof(IRichEditor.Document));
					Assert.Equal("alpha\nbeta", native.Text);
					Assert.Equal(new NSRange(1, 2), native.SelectedRange);
					var underline = (NSNumber)native.TextStorage.GetAttribute(UIStringAttributeKey.UnderlineStyle, 0, out _);
					Assert.Equal((int)NSUnderlineStyle.Single, underline.Int32Value);
				}
				finally
				{
					((IElementHandler)handler).DisconnectHandler();
				}
			});

		[Fact]
		public Task PasteNormalizesTextAndSharedHistoryOwnsUndoRedo() =>
			InvokeOnMainThreadAsync(() =>
			{
				var editor = new RichEditorStub
				{
					Document = RichTextDocument.FromText("a"),
					CursorPosition = 1
				};
				var handler = CreateHandler(editor);
				var clipboard = UIPasteboard.General.Items;
				try
				{
					UIPasteboard.General.String = "b\r\nc";
					var native = handler.PlatformView;
					native.Paste(native);
					Assert.Equal("ab\nc", editor.Document.Text);
					Assert.Equal(new NSRange(4, 0), native.SelectedRange);
					Assert.Equal(4, editor.CursorPosition);
					native.UndoManager!.Undo();
					native.UndoManager.Redo();
					Assert.Equal(1, editor.UndoCalls);
					Assert.Equal(1, editor.RedoCalls);

					editor.IsReadOnly = true;
					handler.UpdateValue(nameof(IRichEditor.IsReadOnly));
					native.InsertText("ignored");
					native.UndoManager.Undo();
					Assert.Equal("ab\nc", native.Text);
					Assert.Equal(1, editor.UndoCalls);
				}
				finally
				{
					UIPasteboard.General.Items = clipboard;
					((IElementHandler)handler).DisconnectHandler();
				}
			});

		static RichTextDocument CreateDocument(RichTextStyle style) => new(new[]
		{
			new RichTextParagraph(new[] { new RichTextRun("alpha", style) }, RichTextListKind.Numbered, listStart: 3),
			new RichTextParagraph(new[] { new RichTextRun("beta") }, RichTextListKind.Numbered, listStart: 3)
		});

		public class RichEditorStub : StubBase, IRichEditor
		{
			public RichTextDocument Document { get; set; } = RichTextDocument.Empty;
			public int CursorPosition { get; set; }
			public int SelectionLength { get; set; }
			public RichTextStyle TypingStyle { get; set; } = RichTextStyle.Default;
			public bool IsReadOnly { get; set; }
			public bool CanUndo { get; set; } = true;
			public bool CanRedo { get; set; } = true;
			public int UndoCalls { get; private set; }
			public int RedoCalls { get; private set; }

			public void UpdateText(string text, int cursorPosition, int selectionLength)
			{
				Document = RichTextDocument.FromText(text);
				// Deliberately remap a transient selection during the native callback.
				CursorPosition = 0;
				SelectionLength = 0;
				Handler.UpdateValue(nameof(IRichEditor.Document));
				Handler.UpdateValue(nameof(IRichEditor.CursorPosition));
				UpdateSelection(cursorPosition, selectionLength);
			}

			public void UpdateSelection(int cursorPosition, int selectionLength)
			{
				CursorPosition = cursorPosition;
				SelectionLength = selectionLength;
				Handler.UpdateValue(nameof(IRichEditor.CursorPosition));
				Handler.UpdateValue(nameof(IRichEditor.SelectionLength));
			}

			public void Undo() => UndoCalls++;
			public void Redo() => RedoCalls++;
		}
	}
}
