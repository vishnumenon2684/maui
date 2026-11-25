using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Maui
{
	/// <summary>The marker used for a rich text paragraph.</summary>
	public enum RichTextListKind
	{
		/// <summary>No list marker.</summary>
		None,
		/// <summary>An unordered bullet.</summary>
		Bullet,
		/// <summary>An ordered decimal marker.</summary>
		Numbered
	}

	/// <summary>An immutable paragraph containing styled runs and optional list metadata.</summary>
	public sealed class RichTextParagraph
	{
		/// <summary>Creates a paragraph. List levels are zero-based and list starts are positive.</summary>
		public RichTextParagraph(IEnumerable<RichTextRun> runs, RichTextListKind listKind = RichTextListKind.None, int listLevel = 0, int listStart = 1)
		{
			if (runs == null)
				throw new ArgumentNullException(nameof(runs));
			if (!Enum.IsDefined(typeof(RichTextListKind), listKind))
				throw new ArgumentOutOfRangeException(nameof(listKind));
			if (listLevel < 0 || (listKind == RichTextListKind.None && listLevel != 0))
				throw new ArgumentOutOfRangeException(nameof(listLevel));
			if (listStart < 1 || (listKind != RichTextListKind.Numbered && listStart != 1))
				throw new ArgumentOutOfRangeException(nameof(listStart));
			var normalized = new List<RichTextRun>();
			var text = new StringBuilder();
			foreach (var run in runs)
			{
				if (run == null)
					throw new ArgumentException("Runs must not contain null.", nameof(runs));
				text.Append(run.Text);
				if (run.Text.Length == 0)
					continue;
				int last = normalized.Count - 1;
				if (last >= 0 && normalized[last].Style.Equals(run.Style))
					normalized[last] = new RichTextRun(normalized[last].Text + run.Text, run.Style);
				else
					normalized.Add(run);
			}
			Runs = normalized.AsReadOnly();
			Text = text.ToString();
			ListKind = listKind;
			ListLevel = listLevel;
			ListStart = listStart;
		}

		/// <summary>Gets normalized runs; adjacent equal styles are merged.</summary>
		public IReadOnlyList<RichTextRun> Runs { get; }
		/// <summary>Gets the paragraph's plain text without its separator or marker.</summary>
		public string Text { get; }
		/// <summary>Gets the list marker kind.</summary>
		public RichTextListKind ListKind { get; }
		/// <summary>Gets the zero-based nesting level.</summary>
		public int ListLevel { get; }
		/// <summary>Gets the starting number for this paragraph's list sequence.</summary>
		public int ListStart { get; }
	}
}
