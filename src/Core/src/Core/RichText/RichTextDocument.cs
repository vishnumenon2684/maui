using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Maui
{
	/// <summary>An immutable rich document. Logical offsets use UTF-16 and one newline between paragraphs.</summary>
	public sealed class RichTextDocument
	{
		/// <summary>Gets an empty document containing one empty paragraph.</summary>
		public static RichTextDocument Empty { get; } = FromText(string.Empty);

		/// <summary>Creates a document, copying the paragraph collection.</summary>
		public RichTextDocument(IEnumerable<RichTextParagraph> paragraphs)
		{
			if (paragraphs == null)
				throw new ArgumentNullException(nameof(paragraphs));
			var copy = new List<RichTextParagraph>();
			var text = new StringBuilder();
			foreach (var paragraph in paragraphs)
			{
				if (paragraph == null)
					throw new ArgumentException("Paragraphs must not contain null.", nameof(paragraphs));
				if (paragraph.ListLevel > 0 && (copy.Count == 0 || copy[copy.Count - 1].ListKind == RichTextListKind.None ||
					paragraph.ListLevel > copy[copy.Count - 1].ListLevel + 1))
					throw new ArgumentException("Nested lists require a parent list paragraph.", nameof(paragraphs));
				if (copy.Count > 0)
					text.Append('\n');
				copy.Add(paragraph);
				text.Append(paragraph.Text);
			}
			if (copy.Count == 0)
				copy.Add(new RichTextParagraph(Array.Empty<RichTextRun>()));
			Paragraphs = copy.AsReadOnly();
			Text = text.ToString();
		}

		/// <summary>Gets the immutable paragraph collection.</summary>
		public IReadOnlyList<RichTextParagraph> Paragraphs { get; }
		/// <summary>Gets plain text; list markers are not included.</summary>
		public string Text { get; }

		/// <summary>Creates an unlisted document from text, normalizing line endings.</summary>
		public static RichTextDocument FromText(string text, RichTextStyle? style = null)
		{
			if (text == null)
				throw new ArgumentNullException(nameof(text));
			var paragraphs = new List<RichTextParagraph>();
			foreach (var line in NormalizeText(text).Split('\n'))
				paragraphs.Add(new RichTextParagraph(new[] { new RichTextRun(line, style) }));
			return new RichTextDocument(paragraphs);
		}

		internal static string NormalizeText(string text)
		{
			var result = new StringBuilder(text.Length);
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '\r')
				{
					result.Append('\n');
					if (i + 1 < text.Length && text[i + 1] == '\n')
						i++;
				}
				else
					result.Append(text[i]);
			}
			return result.ToString();
		}

		internal void ValidateRange(int start, int length)
		{
			if (start < 0 || length < 0 || start > Text.Length || length > Text.Length - start)
				throw new ArgumentOutOfRangeException(nameof(start), "The selection must be inside the document.");
			if (!IsBoundary(start) || !IsBoundary(start + length))
				throw new ArgumentException("A selection cannot split a Unicode surrogate pair.");
		}

		internal bool IsBoundary(int position) => position == 0 || position == Text.Length ||
			!(char.IsHighSurrogate(Text[position - 1]) && char.IsLowSurrogate(Text[position]));

		internal int ParagraphIndex(int position, out int local)
		{
			for (int i = 0; i < Paragraphs.Count; i++)
			{
				int count = Paragraphs[i].Text.Length;
				if (position <= count || i == Paragraphs.Count - 1)
				{
					local = position;
					return i;
				}
				position -= count + 1;
			}
			throw new ArgumentOutOfRangeException(nameof(position));
		}

		internal RichTextStyle StyleAt(int position)
		{
			ValidateRange(position, 0);
			var paragraph = Paragraphs[ParagraphIndex(position, out int local)];
			int offset = 0;
			for (int i = 0; i < paragraph.Runs.Count; i++)
			{
				var run = paragraph.Runs[i];
				offset += run.Text.Length;
				if (local <= offset)
					return run.Style;
			}
			return RichTextStyle.Default;
		}

		internal RichTextDocument Format(int start, int length, Func<RichTextStyle, RichTextStyle> change)
		{
			ValidateRange(start, length);
			var result = new List<RichTextParagraph>();
			int offset = 0;
			for (int p = 0; p < Paragraphs.Count; p++)
			{
				var paragraph = Paragraphs[p];
				var runs = new List<RichTextRun>();
				for (int r = 0; r < paragraph.Runs.Count; r++)
				{
					var run = paragraph.Runs[r];
					int from = Math.Max(0, start - offset);
					int to = Math.Min(run.Text.Length, start + length - offset);
					if (to > from)
					{
						AddSlice(runs, run, 0, from);
						runs.Add(new RichTextRun(run.Text.Substring(from, to - from), change(run.Style)));
						AddSlice(runs, run, to, run.Text.Length);
					}
					else
						runs.Add(run);
					offset += run.Text.Length;
				}
				result.Add(Copy(paragraph, runs));
				offset++;
			}
			return new RichTextDocument(result);
		}

		internal RichTextDocument SetList(int start, int length, RichTextListKind kind, int level, int listStart)
		{
			ValidateRange(start, length);
			int first = ParagraphIndex(start, out _);
			int last = ParagraphIndex(length == 0 ? start : start + length - 1, out _);
			var result = new List<RichTextParagraph>();
			for (int i = 0; i < Paragraphs.Count; i++)
			{
				var p = Paragraphs[i];
				result.Add(i >= first && i <= last
					? new RichTextParagraph(p.Runs, kind, level, listStart) : p);
			}
			NormalizeLevels(result);
			return new RichTextDocument(result);
		}

		internal RichTextDocument Replace(int start, int length, string text, RichTextStyle style)
		{
			ValidateRange(start, length);
			text = NormalizeText(text);
			int first = ParagraphIndex(start, out int localStart);
			int last = ParagraphIndex(start + length, out int localEnd);
			var before = Paragraphs[first];
			var after = Paragraphs[last];
			// Enter on an empty list item ends the list without adding another paragraph.
			if (length == 0 && text == "\n" && before.Text.Length == 0 && before.ListKind != RichTextListKind.None)
				return SetList(start, 0, RichTextListKind.None, 0, 1);
			var result = new List<RichTextParagraph>();
			for (int i = 0; i < first; i++)
				result.Add(Paragraphs[i]);
			var lines = text.Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				var runs = new List<RichTextRun>();
				if (i == 0)
					Slice(before, 0, localStart, runs);
				if (lines[i].Length > 0)
					runs.Add(new RichTextRun(lines[i], style));
				if (i == lines.Length - 1)
					Slice(after, localEnd, after.Text.Length, runs);
				result.Add(Copy(before, runs));
			}
			for (int i = last + 1; i < Paragraphs.Count; i++)
				result.Add(Paragraphs[i]);
			NormalizeLevels(result);
			return new RichTextDocument(result);
		}

		internal int GetListNumber(int index)
		{
			var paragraph = Paragraphs[index];
			int number = paragraph.ListStart;
			for (int i = index - 1; i >= 0; i--)
			{
				var preceding = Paragraphs[i];
				if (preceding.ListKind == RichTextListKind.None || preceding.ListLevel < paragraph.ListLevel)
					break;
				if (preceding.ListLevel == paragraph.ListLevel)
				{
					if (preceding.ListKind != paragraph.ListKind || preceding.ListStart != paragraph.ListStart)
						break;
					number++;
				}
			}
			return number;
		}

		static void NormalizeLevels(List<RichTextParagraph> paragraphs)
		{
			for (int i = 0; i < paragraphs.Count; i++)
			{
				var p = paragraphs[i];
				int maximum = i == 0 || paragraphs[i - 1].ListKind == RichTextListKind.None ? 0 : paragraphs[i - 1].ListLevel + 1;
				if (p.ListLevel > maximum)
					paragraphs[i] = new RichTextParagraph(p.Runs, p.ListKind, maximum, p.ListStart);
			}
		}

		static RichTextParagraph Copy(RichTextParagraph p, IEnumerable<RichTextRun> runs) =>
			new RichTextParagraph(runs, p.ListKind, p.ListLevel, p.ListStart);

		static void Slice(RichTextParagraph paragraph, int start, int end, List<RichTextRun> result)
		{
			int offset = 0;
			for (int i = 0; i < paragraph.Runs.Count; i++)
			{
				var run = paragraph.Runs[i];
				AddSlice(result, run, Math.Max(0, start - offset), Math.Min(run.Text.Length, end - offset));
				offset += run.Text.Length;
			}
		}

		static void AddSlice(List<RichTextRun> result, RichTextRun run, int start, int end)
		{
			if (end > start)
				result.Add(new RichTextRun(run.Text.Substring(start, end - start), run.Style));
		}
	}
}
