using System;
using Xunit;

namespace Microsoft.Maui.Controls.Core.UnitTests
{
	public class RichEditorMarkdownTests : BaseTestFixture
	{
		[Fact]
		public void ImportsParagraphsAndNestedEmphasis()
		{
			var editor = new RichEditor();
			editor.LoadMarkdown("plain **bold *both* bold** end\r\nnext\r\n\r\nlast");

			Assert.Equal("plain bold both bold end\nnext\nlast", editor.Document.Text);
			Assert.Equal(3, editor.Document.Paragraphs.Count);
			var runs = editor.Document.Paragraphs[0].Runs;
			Assert.Equal(5, runs.Count);
			Assert.False(runs[0].Style.Bold);
			Assert.True(runs[1].Style.Bold);
			Assert.False(runs[1].Style.Italic);
			Assert.True(runs[2].Style.Bold);
			Assert.True(runs[2].Style.Italic);
			Assert.True(runs[3].Style.Bold);
			Assert.False(runs[4].Style.Bold);
		}

		[Fact]
		public void ImportsNestedListsAndExplicitNumberRestarts()
		{
			var editor = new RichEditor();
			editor.LoadMarkdown("3. first\n   - child\n\n     7. grandchild\n4. second\n9. restart\n10. next");

			Assert.Equal("first\nchild\ngrandchild\nsecond\nrestart\nnext", editor.Document.Text);
			AssertParagraph(editor.Document.Paragraphs[0], RichTextListKind.Numbered, 0, 3);
			AssertParagraph(editor.Document.Paragraphs[1], RichTextListKind.Bullet, 1, 1);
			AssertParagraph(editor.Document.Paragraphs[2], RichTextListKind.Numbered, 2, 7);
			AssertParagraph(editor.Document.Paragraphs[3], RichTextListKind.Numbered, 0, 3);
			AssertParagraph(editor.Document.Paragraphs[4], RichTextListKind.Numbered, 0, 9);
			AssertParagraph(editor.Document.Paragraphs[5], RichTextListKind.Numbered, 0, 9);
		}

		[Fact]
		public void ImportsWhitelistedHtmlAndInheritedFontStyles()
		{
			var editor = new RichEditor();
			editor.LoadMarkdown("<span style=\"font-family: '字体 &quot;A&quot;'; font-size: 12.5px\"><u>**bold** plain</u>" +
				"<span style=\"font-size: 20px\">*italic*</span></span> default");

			var runs = editor.Document.Paragraphs[0].Runs;
			Assert.Equal("bold plainitalic default", editor.Document.Text);
			Assert.Equal("字体 \"A\"", runs[0].Style.FontFamily);
			Assert.Equal(12.5, runs[0].Style.FontSize);
			Assert.True(runs[0].Style.Bold);
			Assert.True(runs[0].Style.Underline);
			Assert.True(runs[1].Style.Underline);
			Assert.False(runs[1].Style.Bold);
			Assert.Equal("字体 \"A\"", runs[2].Style.FontFamily);
			Assert.Equal(20, runs[2].Style.FontSize);
			Assert.True(runs[2].Style.Italic);
			Assert.False(runs[2].Style.Underline);
			Assert.Equal(RichTextStyle.Default, runs[3].Style);
		}

		[Fact]
		public void RoundTripPreservesLiteralSyntaxWhitespaceEmptyParagraphsAndMixedStyles()
		{
			var original = new RichTextDocument(new[]
			{
				new RichTextParagraph(Array.Empty<RichTextRun>()),
				new RichTextParagraph(new[]
				{
					new RichTextRun(" # heading [link](https://example.test) `code` <script> & \\ * _ | \t ",
						new RichTextStyle(bold: true)),
					new RichTextRun(" italic ", new RichTextStyle(italic: true)),
					new RichTextRun("both 👩🏽‍💻", new RichTextStyle(bold: true, italic: true, underline: true,
						fontFamily: "字体 \"quoted\" 'single'; \\ family", fontSize: 0.0000001)),
					new RichTextRun("plain")
				}),
				new RichTextParagraph(Array.Empty<RichTextRun>()),
				new RichTextParagraph(Array.Empty<RichTextRun>())
			});
			AssertRoundTrip(original);
		}

		[Fact]
		public void RoundTripPreservesNestedListKindsStartsAndEmptyItems()
		{
			AssertRoundTrip(new RichTextDocument(new[]
			{
				Paragraph("first", RichTextListKind.Numbered, 0, 345),
				Paragraph("", RichTextListKind.Bullet, 1),
				Paragraph("grandchild", RichTextListKind.Numbered, 2, 8),
				Paragraph("second", RichTextListKind.Numbered, 0, 345),
				Paragraph("restart", RichTextListKind.Numbered, 0, 2),
				Paragraph("another restart", RichTextListKind.Numbered, 0, 7),
				Paragraph("bullet", RichTextListKind.Bullet, 0),
				Paragraph("number", RichTextListKind.Numbered, 0, 1),
				new RichTextParagraph(Array.Empty<RichTextRun>()),
				Paragraph("separate", RichTextListKind.Numbered, 0, 1)
			}));
		}

		[Fact]
		public void RoundTripPreservesWhitespaceOnlyRunsAcrossEveryEmphasisCombination()
		{
			var runs = new RichTextRun[8];
			for (int i = 0; i < runs.Length; i++)
				runs[i] = new RichTextRun(" \t ", new RichTextStyle(
					bold: (i & 1) != 0, italic: (i & 2) != 0, underline: (i & 4) != 0));
			AssertRoundTrip(new RichTextDocument(new[] { new RichTextParagraph(runs) }));
		}

		[Fact]
		public void RoundTripEndsNestedListsBeforeAnUnlistedParagraph()
		{
			AssertRoundTrip(new RichTextDocument(new[]
			{
				Paragraph("parent", RichTextListKind.Bullet, 0),
				Paragraph("nested", RichTextListKind.Numbered, 1, 25),
				Paragraph("restart", RichTextListKind.Numbered, 1, 3),
				Paragraph("bullet", RichTextListKind.Bullet, 1),
				Paragraph("number", RichTextListKind.Numbered, 1, 12),
				new RichTextParagraph(new[] { new RichTextRun("unlisted") }),
				Paragraph("", RichTextListKind.Numbered, 0, 4)
			}));
		}

		[Theory]
		[InlineData("# heading")]
		[InlineData("heading\n===")]
		[InlineData("[link](https://example.test)")]
		[InlineData("![image](https://example.test/image.png)")]
		[InlineData("<https://example.test>")]
		[InlineData("`code`")]
		[InlineData("```\ncode\n```")]
		[InlineData("> quote")]
		[InlineData("---")]
		[InlineData("| a | b |\n| --- | --- |\n| x | y |")]
		[InlineData("[reference]: https://example.test")]
		[InlineData("<script>alert('x')</script>")]
		[InlineData("<img src=\"https://example.test/a.png\">")]
		[InlineData("<span onclick=\"alert(1)\">text</span>")]
		[InlineData("<u style=\"color:red\">text</u>")]
		[InlineData("<span style=\"color:red\">text</span>")]
		[InlineData("<span style=\"font-size:0px\">text</span>")]
		[InlineData("<span style=\"font-size:NaNpx\">text</span>")]
		[InlineData("<span style=\"font-size:12pt\">text</span>")]
		[InlineData("<span style=\"font-family:url(https://example.test)\">text</span>")]
		[InlineData("<span style=\"font-family:'x';font-family:'y'\">text</span>")]
		[InlineData("<span style=\"font-family:'unterminated\">text</span>")]
		[InlineData("<span style=\"font-family:'\\41 rial'\">text</span>")]
		[InlineData("<span style=\"font-size:12px\" style=\"font-size:14px\">text</span>")]
		[InlineData("<u>unclosed")]
		[InlineData("closing</u>")]
		[InlineData("<u><span>misnested</u></span>")]
		[InlineData("<span/>")]
		[InlineData("<!-- comment -->")]
		[InlineData("0. item")]
		[InlineData("- first\n  continuation")]
		[InlineData("- first\n\n  second paragraph")]
		[InlineData("null\0character")]
		public void RejectedImportIsTransactional(string markdown)
		{
			var editor = new RichEditor();
			editor.InsertText("existing");
			editor.Select(1, 3);
			var document = editor.Document;
			var style = editor.TypingStyle;
			int changes = 0;
			editor.TextChanged += (_, _) => changes++;

			var exception = Assert.Throws<FormatException>(() => editor.LoadMarkdown(markdown));

			Assert.NotEmpty(exception.Message);
			Assert.Same(document, editor.Document);
			Assert.Equal(1, editor.CursorPosition);
			Assert.Equal(3, editor.SelectionLength);
			Assert.Same(style, editor.TypingStyle);
			Assert.True(editor.CanUndo);
			Assert.Equal(0, changes);
		}

		[Fact]
		public void RejectsUnpairedSurrogates()
		{
			RejectedImportIsTransactional("unpaired" + new string((char)0xD800, 1));
		}

		[Fact]
		public void SuccessfulImportReplacesDocumentAndClearsHistory()
		{
			var editor = new RichEditor();
			editor.InsertText("old");
			int changes = 0;
			editor.TextChanged += (_, _) => changes++;

			editor.LoadMarkdown("**new**");

			Assert.Equal("new", editor.Document.Text);
			Assert.False(editor.CanUndo);
			Assert.False(editor.CanRedo);
			Assert.Equal(1, changes);
			editor.LoadMarkdown("");
			Assert.Single(editor.Document.Paragraphs);
			Assert.Empty(editor.Document.Text);
			Assert.Throws<ArgumentNullException>(() => editor.LoadMarkdown(null!));
		}

		[Fact]
		public void ExportDoesNotChangeDocumentOrHistoryAndRejectsUnrepresentableState()
		{
			var editor = new RichEditor();
			editor.InsertText("text");
			var document = editor.Document;
			_ = editor.GetMarkdown();
			Assert.Same(document, editor.Document);
			Assert.True(editor.CanUndo);

			editor.Document = new RichTextDocument(new[] { Paragraph("item", RichTextListKind.Numbered, 0, int.MaxValue) });
			Assert.Throws<FormatException>(() => editor.GetMarkdown());
			editor.Document = RichTextDocument.FromText("null\0character");
			Assert.Throws<FormatException>(() => editor.GetMarkdown());
		}

		static RichTextParagraph Paragraph(string text, RichTextListKind kind, int level, int start = 1) =>
			new RichTextParagraph(new[] { new RichTextRun(text) }, kind, level, start);

		static void AssertParagraph(RichTextParagraph paragraph, RichTextListKind kind, int level, int start)
		{
			Assert.Equal(kind, paragraph.ListKind);
			Assert.Equal(level, paragraph.ListLevel);
			Assert.Equal(start, paragraph.ListStart);
		}

		static void AssertRoundTrip(RichTextDocument original)
		{
			var exporter = new RichEditor { Document = original };
			var importer = new RichEditor();
			importer.LoadMarkdown(exporter.GetMarkdown());
			Assert.Equal(original.Text, importer.Document.Text);
			Assert.Equal(original.Paragraphs.Count, importer.Document.Paragraphs.Count);
			for (int i = 0; i < original.Paragraphs.Count; i++)
			{
				var expected = original.Paragraphs[i];
				var actual = importer.Document.Paragraphs[i];
				AssertParagraph(actual, expected.ListKind, expected.ListLevel, expected.ListStart);
				Assert.Equal(expected.Runs.Count, actual.Runs.Count);
				for (int r = 0; r < expected.Runs.Count; r++)
				{
					Assert.Equal(expected.Runs[r].Text, actual.Runs[r].Text);
					Assert.Equal(expected.Runs[r].Style, actual.Runs[r].Style);
				}
			}
		}
	}
}
