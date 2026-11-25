using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Parser.Tokens;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Microsoft.Maui.Controls
{
	public partial class RichEditor
	{
		static readonly MarkdownPipeline RichMarkdownPipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();

		/// <summary>Replaces the document with paragraphs, emphasis, and lists parsed from a bounded Markdown dialect.</summary>
		/// <remarks>
		/// Inline HTML is limited to balanced u and span elements. Spans accept only font-family and
		/// positive font-size values in px; quoted font names support quote and backslash escapes only.
		/// An empty span represents an empty paragraph. Headings, links,
		/// code, tables, images, other HTML, and multiline list items are not supported.
		/// Parsing does not execute scripts or load resources. Invalid input leaves the document unchanged.
		/// </remarks>
		/// <param name="markdown">The Markdown to import.</param>
		/// <exception cref="ArgumentNullException">The input is null.</exception>
		/// <exception cref="FormatException">The input contains unsupported or invalid content.</exception>
		public void LoadMarkdown(string markdown)
		{
			if (markdown == null)
				throw new ArgumentNullException(nameof(markdown));
			RichTextDocument document;
			try
			{
				if (markdown.IndexOf("\0", StringComparison.Ordinal) >= 0)
					throw new FormatException("Markdown cannot preserve null characters.");
				_ = RichTextDocument.FromText(markdown);
				var paragraphs = new List<RichTextParagraph>();
				var syntax = Markdig.Markdown.Parse(RichTextDocument.NormalizeText(markdown), RichMarkdownPipeline);
				if (syntax.GetLinkReferenceDefinitions(false).Links.Count > 0)
					throw new FormatException("Markdown link reference definitions are not supported.");
				ReadMarkdownBlocks(syntax, paragraphs, 0);
				document = new RichTextDocument(paragraphs);
			}
			catch (HtmlParseException exception)
			{
				throw new FormatException("Invalid inline HTML in Markdown.", exception);
			}
			catch (ArgumentException exception)
			{
				throw new FormatException("Markdown cannot be represented as a rich text document.", exception);
			}
			Document = document;
		}

		/// <summary>Explicitly exports the current document using the supported Markdown dialect.</summary>
		/// <remarks>
		/// Inline spans and character references preserve empty paragraphs, literal Markdown punctuation,
		/// whitespace, and font overrides. Export is not a general-purpose or lossless Markdown serializer.
		/// Ordered markers are limited to nine decimal digits, as required by Markdown.
		/// List and inline nesting are limited to 64 levels. Null text characters and font names containing
		/// control characters or unpaired surrogates cannot be exported.
		/// </remarks>
		/// <returns>Markdown representing the supported document state.</returns>
		/// <exception cref="FormatException">Document state cannot be represented by the supported dialect.</exception>
		public string GetMarkdown()
		{
			var document = Document;
			var result = new StringBuilder();
			var levels = new List<MarkdownListSequence>();
			for (int i = 0; i < document.Paragraphs.Count; i++)
			{
				var paragraph = document.Paragraphs[i];
				if (paragraph.ListLevel > 64)
					throw new FormatException("Markdown list nesting cannot exceed 64 levels.");
				if (i > 0)
					result.Append("\n\n");
				if (paragraph.ListKind == RichTextListKind.None)
					levels.Clear();
				else
				{
					while (levels.Count > paragraph.ListLevel + 1)
						levels.RemoveAt(levels.Count - 1);
					if (levels.Count == paragraph.ListLevel)
						levels.Add(new MarkdownListSequence(paragraph));
					var sequence = levels[paragraph.ListLevel];
					if (sequence.Kind != paragraph.ListKind || sequence.Start != paragraph.ListStart)
					{
						sequence.Delimiter = sequence.Delimiter == '.' ? ')' : '.';
						sequence.Kind = paragraph.ListKind;
						sequence.Start = paragraph.ListStart;
					}
					for (int level = 0; level < paragraph.ListLevel; level++)
						result.Append(' ', levels[level].Width);
					string marker = "- ";
					if (paragraph.ListKind == RichTextListKind.Numbered)
					{
						long number = GetMarkdownListNumber(document, i);
						if (number > 999999999)
							throw new FormatException("Markdown ordered list markers cannot exceed nine digits.");
						marker = number.ToString(CultureInfo.InvariantCulture) + sequence.Delimiter + " ";
					}
					sequence.Width = marker.Length;
					result.Append(marker);
				}
				if (paragraph.Runs.Count == 0)
					result.Append("<span></span>");
				foreach (var run in paragraph.Runs)
					WriteMarkdownRun(result, run);
			}
			return result.ToString();
		}

		static long GetMarkdownListNumber(RichTextDocument document, int index)
		{
			var paragraph = document.Paragraphs[index];
			long number = paragraph.ListStart;
			for (int i = index - 1; i >= 0; i--)
			{
				var previous = document.Paragraphs[i];
				if (previous.ListKind == RichTextListKind.None || previous.ListLevel < paragraph.ListLevel)
					break;
				if (previous.ListLevel == paragraph.ListLevel)
				{
					if (previous.ListKind != paragraph.ListKind || previous.ListStart != paragraph.ListStart)
						break;
					number++;
				}
			}
			return number;
		}

		static void ReadMarkdownBlocks(ContainerBlock blocks, List<RichTextParagraph> result, int level)
		{
			if (level > 64)
				throw new FormatException("Markdown list nesting cannot exceed 64 levels.");
			foreach (var block in blocks)
			{
				if (block is ParagraphBlock paragraph)
					ReadMarkdownParagraph(paragraph, result, RichTextListKind.None, 0, 1);
				else if (block is ListBlock list)
					ReadMarkdownList(list, result, level);
				else
					throw UnsupportedMarkdown(block);
			}
		}

		static void ReadMarkdownList(ListBlock list, List<RichTextParagraph> result, int level)
		{
			if (level > 64)
				throw new FormatException("Markdown list nesting cannot exceed 64 levels.");
			var kind = list.IsOrdered ? RichTextListKind.Numbered : RichTextListKind.Bullet;
			int start = 1;
			if (list.IsOrdered && (!int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out start) || start < 1))
				throw new FormatException("Markdown list starts must be positive decimal numbers.");
			long expected = start;
			foreach (var block in list)
			{
				if (!(block is ListItemBlock item))
					throw UnsupportedMarkdown(block);
				if (list.IsOrdered)
				{
					int number = item.Order;
					if (number < 1)
						throw new FormatException("Markdown list markers must be positive decimal numbers.");
					if (number != expected)
						start = number;
					expected = (long)number + 1;
				}
				bool hasParagraph = false;
				foreach (var child in item)
				{
					if (child is ParagraphBlock paragraph && !hasParagraph)
					{
						ReadMarkdownParagraph(paragraph, result, kind, level, start);
						hasParagraph = true;
					}
					else if (child is ListBlock nested && hasParagraph)
						ReadMarkdownList(nested, result, level + 1);
					else
						throw new FormatException("Each Markdown list item must contain one paragraph followed by optional nested lists.");
				}
				if (!hasParagraph)
					result.Add(new RichTextParagraph(Array.Empty<RichTextRun>(), kind, level, start));
			}
		}

		static void ReadMarkdownParagraph(ParagraphBlock paragraph, List<RichTextParagraph> result, RichTextListKind kind, int level, int start)
		{
			var html = new StringBuilder();
			BuildMarkdownHtml(paragraph.Inline, html, 0);
			var tags = new Queue<HtmlTagToken>();
			var htmlStack = new Stack<string>();
			var parser = new HtmlParser(new HtmlParserOptions
			{
				IsScripting = false,
				IsStrictMode = true,
				OnToken = (token, _) =>
				{
					if (token is HtmlTagToken tag)
					{
						if ((tag.Name != "u" && tag.Name != "span") || tag.IsSelfClosing)
							throw new FormatException("Markdown accepts only balanced u and span inline HTML elements.");
						if (tag.Type == HtmlTokenType.StartTag)
						{
							if (htmlStack.Count >= 64)
								throw new FormatException("Markdown inline nesting cannot exceed 64 levels.");
							htmlStack.Push(tag.Name);
						}
						else if (htmlStack.Count == 0 || htmlStack.Pop() != tag.Name)
							throw new FormatException("Markdown inline HTML elements must be properly nested.");
						tags.Enqueue(tag);
					}
					else if (token.Type != HtmlTokenType.Character && token.Type != HtmlTokenType.EndOfFile)
						throw new FormatException("Markdown accepts only u and span inline HTML elements.");
				}
			});
			using (var context = new HtmlParser().ParseDocument("<!doctype html><html><head></head><body></body></html>"))
				parser.ParseFragment(html.ToString(), context.CreateElement("div"));
			if (htmlStack.Count != 0)
				throw new FormatException("Markdown inline HTML elements must be closed.");
			var runs = new List<RichTextRun>();
			ReadMarkdownInlines(paragraph.Inline, RichTextStyle.Default, runs, result, tags, kind, level, start, 0);
			result.Add(new RichTextParagraph(runs, kind, level, start));
		}

		static void BuildMarkdownHtml(ContainerInline? container, StringBuilder result, int depth)
		{
			if (depth > 64)
				throw new FormatException("Markdown inline nesting cannot exceed 64 levels.");
			if (container == null)
				return;
			for (var inline = container.FirstChild; inline != null; inline = inline.NextSibling)
			{
				if (inline is HtmlInline html)
					result.Append(html.Tag);
				else if (inline is EmphasisInline emphasis && (emphasis.DelimiterChar == '*' || emphasis.DelimiterChar == '_') &&
					(emphasis.DelimiterCount == 1 || emphasis.DelimiterCount == 2))
					BuildMarkdownHtml(emphasis, result, depth + 1);
				else if (inline is LiteralInline || inline is HtmlEntityInline || inline is LineBreakInline)
					result.Append('x');
				else
					throw UnsupportedMarkdown(inline);
			}
		}

		static void ReadMarkdownInlines(ContainerInline? container, RichTextStyle initial, List<RichTextRun> runs,
			List<RichTextParagraph> paragraphs, Queue<HtmlTagToken> tags, RichTextListKind kind, int level, int start, int depth)
		{
			if (container == null)
				return;
			var stack = new Stack<KeyValuePair<string, RichTextStyle>>();
			var style = initial;
			for (var inline = container.FirstChild; inline != null; inline = inline.NextSibling)
			{
				if (inline is LiteralInline literal)
					runs.Add(new RichTextRun(literal.Content.ToString(), style));
				else if (inline is HtmlEntityInline entity)
					runs.Add(new RichTextRun(entity.Transcoded.ToString(), style));
				else if (inline is EmphasisInline emphasis)
					ReadMarkdownInlines(emphasis, new RichTextStyle(style.Bold || emphasis.DelimiterCount == 2,
						style.Italic || emphasis.DelimiterCount == 1, style.Underline, style.FontFamily, style.FontSize),
						runs, paragraphs, tags, kind, level, start, depth + 1);
				else if (inline is LineBreakInline)
				{
					if (kind != RichTextListKind.None)
						throw new FormatException("Multiline Markdown list items are not supported.");
					paragraphs.Add(new RichTextParagraph(runs));
					runs.Clear();
				}
				else if (inline is HtmlInline)
				{
					var tag = tags.Dequeue();
					if ((tag.Name != "u" && tag.Name != "span") || tag.IsSelfClosing)
						throw new FormatException("Markdown accepts only balanced u and span inline HTML elements.");
					if (tag.Type == HtmlTokenType.EndTag)
					{
						if (tag.Attributes.Count != 0 || stack.Count == 0 || stack.Peek().Key != tag.Name)
							throw new FormatException("Markdown inline HTML elements must be properly nested within emphasis.");
						style = stack.Pop().Value;
					}
					else
					{
						if (stack.Count + depth > 64)
							throw new FormatException("Markdown inline nesting cannot exceed 64 levels.");
						stack.Push(new KeyValuePair<string, RichTextStyle>(tag.Name, style));
						if (tag.Name == "u")
						{
							if (tag.Attributes.Count != 0)
								throw new FormatException("Markdown u elements cannot have attributes.");
							style = new RichTextStyle(style.Bold, style.Italic, true, style.FontFamily, style.FontSize);
						}
						else
						{
							if (tag.Attributes.Count > 1 || (tag.Attributes.Count == 1 && tag.Attributes[0].Name != "style"))
								throw new FormatException("Markdown spans accept only a style attribute.");
							if (tag.Attributes.Count == 1)
								style = ReadMarkdownFont(tag.Attributes[0].Value, style);
						}
					}
				}
			}
			if (stack.Count != 0)
				throw new FormatException("Markdown inline HTML elements must be closed within their paragraph or emphasis.");
		}

		static RichTextStyle ReadMarkdownFont(string css, RichTextStyle inherited)
		{
			string? family = inherited.FontFamily;
			double? size = inherited.FontSize;
			bool hasFamily = false;
			bool hasSize = false;
			int position = 0;
			while (position < css.Length)
			{
				SkipMarkdownCssSpace(css, ref position);
				if (position == css.Length)
					break;
				int begin = position;
				while (position < css.Length && (char.IsLetter(css[position]) || css[position] == '-'))
					position++;
				string property = css.Substring(begin, position - begin);
				SkipMarkdownCssSpace(css, ref position);
				if (position == css.Length || css[position++] != ':')
					throw new FormatException("Invalid Markdown font declaration.");
				SkipMarkdownCssSpace(css, ref position);
				if (string.Equals(property, "font-family", StringComparison.OrdinalIgnoreCase) && !hasFamily)
				{
					hasFamily = true;
					if (position < css.Length && (css[position] == '"' || css[position] == '\''))
					{
						char quote = css[position++];
						var value = new StringBuilder();
						bool closed = false;
						while (position < css.Length)
						{
							char character = css[position++];
							if (character == quote)
							{
								closed = true;
								break;
							}
							if (character == '\\')
							{
								if (position == css.Length)
									throw new FormatException("Unterminated Markdown font escape.");
								character = css[position++];
								if (character != '\\' && character != '"' && character != '\'')
									throw new FormatException("Markdown font escapes support only quotes and backslashes.");
							}
							if (char.IsControl(character))
								throw new FormatException("Markdown font names cannot contain control characters.");
							value.Append(character);
						}
						if (!closed)
							throw new FormatException("Unterminated Markdown font name.");
						family = value.ToString();
					}
					else
					{
						begin = position;
						while (position < css.Length && css[position] != ';')
						{
							char character = css[position++];
							if (!char.IsLetterOrDigit(character) && character != '-' && character != '_' && character != ' ')
								throw new FormatException("Quote font-family names containing punctuation.");
						}
						family = css.Substring(begin, position - begin).Trim();
					}
					if (string.IsNullOrWhiteSpace(family))
						throw new FormatException("Markdown font-family must not be empty.");
				}
				else if (string.Equals(property, "font-size", StringComparison.OrdinalIgnoreCase) && !hasSize)
				{
					hasSize = true;
					begin = position;
					while (position < css.Length && css[position] != ';')
						position++;
					string value = css.Substring(begin, position - begin).Trim();
					if (!value.EndsWith("px", StringComparison.OrdinalIgnoreCase) ||
						!double.TryParse(value.Substring(0, value.Length - 2), NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
							CultureInfo.InvariantCulture, out double number) || number <= 0 || double.IsInfinity(number) || double.IsNaN(number))
						throw new FormatException("Markdown font-size must be a positive finite number in px.");
					size = number;
				}
				else
					throw new FormatException("Markdown accepts each of font-family and font-size at most once, and no other CSS.");
				SkipMarkdownCssSpace(css, ref position);
				if (position < css.Length && css[position++] != ';')
					throw new FormatException("Invalid Markdown font declaration separator.");
			}
			return new RichTextStyle(inherited.Bold, inherited.Italic, inherited.Underline, family, size);
		}

		static void SkipMarkdownCssSpace(string css, ref int position)
		{
			while (position < css.Length && (css[position] == ' ' || css[position] == '\t' || css[position] == '\r' || css[position] == '\n'))
				position++;
		}

		static void WriteMarkdownRun(StringBuilder result, RichTextRun run)
		{
			var style = run.Style;
			result.Append("<span");
			if (style.FontFamily != null || style.FontSize.HasValue)
			{
				var css = new StringBuilder();
				if (style.FontFamily != null)
				{
					for (int i = 0; i < style.FontFamily.Length; i++)
					{
						char character = style.FontFamily[i];
						if (char.IsHighSurrogate(character))
						{
							if (++i == style.FontFamily.Length || !char.IsLowSurrogate(style.FontFamily[i]))
								throw new FormatException("Markdown font names cannot contain unpaired surrogates.");
						}
						else if (char.IsLowSurrogate(character))
							throw new FormatException("Markdown font names cannot contain unpaired surrogates.");
					}
					css.Append("font-family:\"");
					foreach (char character in style.FontFamily)
					{
						if (char.IsControl(character))
							throw new FormatException("Markdown font names cannot contain control characters.");
						if (character == '\\' || character == '"')
							css.Append('\\');
						css.Append(character);
					}
					css.Append("\";");
				}
				if (style.FontSize.HasValue)
					css.Append("font-size:").Append(style.FontSize.Value.ToString("R", CultureInfo.InvariantCulture)).Append("px;");
				result.Append(" style=\"").Append(WebUtility.HtmlEncode(css.ToString())).Append('"');
			}
			result.Append('>');
			if (style.Underline)
				result.Append("<u>");
			string delimiter = style.Bold ? (style.Italic ? "***" : "**") : (style.Italic ? "*" : "");
			result.Append(delimiter);
			// Tags keep emphasis delimiters adjacent to non-whitespace even for whitespace-only runs.
			if (delimiter.Length > 0)
				result.Append("<span>");
			foreach (char character in run.Text)
			{
				if (character == '\0')
					throw new FormatException("Markdown cannot preserve null characters.");
				if (char.IsWhiteSpace(character) || (character < 128 && !char.IsLetterOrDigit(character)))
					result.Append("&#").Append(((int)character).ToString(CultureInfo.InvariantCulture)).Append(';');
				else
					result.Append(character);
			}
			if (delimiter.Length > 0)
				result.Append("</span>");
			result.Append(delimiter);
			if (style.Underline)
				result.Append("</u>");
			result.Append("</span>");
		}

		static FormatException UnsupportedMarkdown(MarkdownObject node) =>
			new FormatException("Unsupported Markdown construct: " + node.GetType().Name + ".");

		sealed class MarkdownListSequence
		{
			internal MarkdownListSequence(RichTextParagraph paragraph)
			{
				Kind = paragraph.ListKind;
				Start = paragraph.ListStart;
			}
			internal RichTextListKind Kind;
			internal int Start;
			internal char Delimiter = '.';
			internal int Width;
		}
	}
}
