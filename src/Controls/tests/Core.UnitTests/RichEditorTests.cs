using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;
using Xunit;

namespace Microsoft.Maui.Controls.Core.UnitTests
{
	public class RichEditorTests : BaseTestFixture
	{
		[Fact]
		public void DefaultRegistrationKeepsEditorIndependent()
		{
			using var app = MauiApp.CreateBuilder().UseMauiApp<ApplicationStub>().Build();
			var handlers = app.Services.GetRequiredService<IMauiHandlersFactory>();
			Assert.IsType<RichEditorHandler>(handlers.GetHandler(typeof(RichEditor)));
			Assert.IsType<EditorHandler>(handlers.GetHandler(typeof(Editor)));
		}
		[Fact]
		public void FormattingSplitsRunsAndTogglesWithoutChangingText()
		{
			var editor = new RichEditor { Document = RichTextDocument.FromText("hello world") };
			editor.Select(6, 5);
			editor.ApplyBold();
			Assert.Equal("hello world", editor.Document.Text);
			Assert.Equal(2, editor.Document.Paragraphs[0].Runs.Count);
			Assert.True(editor.Document.Paragraphs[0].Runs[1].Style.Bold);
			editor.ApplyBold();
			Assert.Single(editor.Document.Paragraphs[0].Runs);
			Assert.False(editor.Document.Paragraphs[0].Runs[0].Style.Bold);
		}

		[Fact]
		public void FormattingAcrossParagraphsPreservesOtherStyles()
		{
			var editor = new RichEditor { Document = RichTextDocument.FromText("one\ntwo", new RichTextStyle(italic: true)) };
			editor.Select(1, 5);
			editor.ApplyUnderline();
			Assert.Equal("one\ntwo", editor.Document.Text);
			Assert.True(editor.Document.Paragraphs[0].Runs[1].Style.Underline);
			Assert.True(editor.Document.Paragraphs[1].Runs[0].Style.Underline);
			Assert.True(editor.Document.Paragraphs[1].Runs[0].Style.Italic);
			Assert.False(editor.Document.Paragraphs[1].Runs[1].Style.Underline);
		}

		[Fact]
		public void EmptySelectionChangesTypingStyleAndInsertedText()
		{
			var editor = new RichEditor();
			editor.ApplyBold();
			editor.InsertText("bold");
			Assert.True(editor.Document.Paragraphs[0].Runs[0].Style.Bold);
			editor.ApplyBold();
			editor.InsertText(" normal");
			Assert.False(editor.Document.Paragraphs[0].Runs[1].Style.Bold);
		}

		[Fact]
		public void UndoRedoRestoresDocumentSelectionAndStyle()
		{
			var editor = new RichEditor { Document = RichTextDocument.FromText("abc") };
			editor.Select(1, 1);
			editor.ApplyFont("serif", 24);
			editor.InsertText("XY");
			editor.Undo();
			Assert.Equal("abc", editor.Document.Text);
			Assert.Equal(1, editor.CursorPosition);
			Assert.Equal(1, editor.SelectionLength);
			Assert.Equal(24, editor.TypingStyle.FontSize);
			editor.Undo();
			Assert.Null(editor.Document.Paragraphs[0].Runs[0].Style.FontSize);
			editor.Redo();
			editor.Redo();
			Assert.Equal("aXYc", editor.Document.Text);
			Assert.Equal(3, editor.CursorPosition);
		}

		[Fact]
		public void NativeTypingCoalescesAndAnInterveningCommandBreaksHistory()
		{
			var editor = new RichEditor();
			var native = (IRichEditor)editor;
			native.UpdateText("a", 1, 0);
			native.UpdateText("ab", 2, 0);
			native.UpdateSelection(2, 0);
			native.UpdateText("abc", 3, 0);
			editor.Undo();
			Assert.Equal("", editor.Document.Text);
			editor.Redo();
			editor.ApplyBold();
			native.UpdateText("abcd", 4, 0);
			editor.Undo();
			Assert.Equal("abc", editor.Document.Text);
			Assert.True(editor.TypingStyle.Bold);
		}

		[Fact]
		public void DocumentReplacementClearsHistoryAndClampsSelection()
		{
			var editor = new RichEditor();
			editor.InsertText("long document");
			editor.Document = RichTextDocument.FromText("x");
			Assert.Equal(1, editor.CursorPosition);
			Assert.False(editor.CanUndo);
			Assert.False(editor.CanRedo);
		}

		[Fact]
		public void EnterContinuesNumberedListAndEmptyEnterExitsIt()
		{
			var editor = new RichEditor();
			editor.ApplyList(RichTextListKind.Numbered, start: 5);
			editor.InsertText("first\n");
			Assert.Equal(2, editor.Document.Paragraphs.Count);
			Assert.Equal(RichTextListKind.Numbered, editor.Document.Paragraphs[1].ListKind);
			Assert.Equal(6, editor.Document.GetListNumber(1));
			editor.InsertText("\n");
			Assert.Equal(2, editor.Document.Paragraphs.Count);
			Assert.Equal(RichTextListKind.None, editor.Document.Paragraphs[1].ListKind);
			Assert.Equal(6, editor.CursorPosition);
		}

		[Fact]
		public void NativeEmptyListEnterDoesNotAdvanceIntoFollowingParagraph()
		{
			var editor = new RichEditor
			{
				Document = new RichTextDocument(new[]
				{
					new RichTextParagraph(Array.Empty<RichTextRun>(), RichTextListKind.Bullet),
					new RichTextParagraph(new[] { new RichTextRun("next") })
				})
			};
			((IRichEditor)editor).UpdateText("\n\nnext", 1, 0);
			Assert.Equal("\nnext", editor.Document.Text);
			Assert.Equal(0, editor.CursorPosition);
		}

		[Fact]
		public void DeletingParagraphSeparatorMergesRunsAndRenumbersList()
		{
			var editor = new RichEditor();
			editor.ApplyList(RichTextListKind.Numbered);
			editor.InsertText("a\nb\nc");
			((IRichEditor)editor).UpdateText("ab\nc", 1, 0);
			Assert.Equal(2, editor.Document.Paragraphs.Count);
			Assert.Equal(2, editor.Document.GetListNumber(1));
		}

		[Fact]
		public void ReplacingAcrossParagraphsPreservesTheTrailingRunStyle()
		{
			var editor = new RichEditor
			{
				Document = new RichTextDocument(new[]
				{
					new RichTextParagraph(new[] { new RichTextRun("first") }),
					new RichTextParagraph(new[] { new RichTextRun("last", new RichTextStyle(underline: true)) })
				})
			};
			editor.Select(2, 6);
			editor.InsertText("X");
			Assert.Equal("fiXst", editor.Document.Text);
			Assert.True(editor.Document.Paragraphs[0].Runs[1].Style.Underline);
		}

		[Fact]
		public void SurrogateSelectionsAreRejectedAndNativeReplacementKeepsPairsIntact()
		{
			var editor = new RichEditor { Document = RichTextDocument.FromText("a\U0001f600z") };
			Assert.Throws<ArgumentException>(() => editor.Select(2, 0));
			((IRichEditor)editor).UpdateText("a\U0001f601z", 3, 0);
			Assert.Equal("a\U0001f601z", editor.Document.Text);
			editor.Select(1, 2);
			editor.ApplyItalic();
			Assert.Equal("\U0001f601", editor.Document.Paragraphs[0].Runs[1].Text);
		}

		[Fact]
		public void DocumentCollectionsAreCopiedAndReadOnly()
		{
			var runs = new List<RichTextRun> { new RichTextRun("original") };
			var paragraphs = new List<RichTextParagraph> { new RichTextParagraph(runs) };
			var document = new RichTextDocument(paragraphs);
			runs.Clear();
			paragraphs.Clear();
			Assert.Equal("original", document.Text);
			Assert.Throws<NotSupportedException>(() => ((IList<RichTextParagraph>)document.Paragraphs).Clear());
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		[InlineData(double.NaN)]
		[InlineData(double.PositiveInfinity)]
		public void InvalidFontsDoNotChangeDocument(double size)
		{
			var editor = new RichEditor();
			Assert.Throws<ArgumentOutOfRangeException>(() => editor.ApplyFont(null, size));
			Assert.False(editor.CanUndo);
		}

		[Fact]
		public void ReadOnlyCommandsReportErrors()
		{
			var editor = new RichEditor { IsReadOnly = true };
			Assert.Throws<InvalidOperationException>(() => editor.InsertText("x"));
			Assert.Throws<InvalidOperationException>(() => editor.ApplyBold());
			Assert.Throws<InvalidOperationException>(() => editor.ApplyList(RichTextListKind.Bullet));
			Assert.Equal("", editor.Document.Text);
		}
	}
}
