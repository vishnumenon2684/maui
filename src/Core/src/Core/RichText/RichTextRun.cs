using System;

namespace Microsoft.Maui
{
	/// <summary>A single immutable text run within a paragraph.</summary>
	public sealed class RichTextRun
	{
		/// <summary>Creates a run. Paragraph separators belong to the document, not to runs.</summary>
		public RichTextRun(string text, RichTextStyle? style = null)
		{
			if (text == null)
				throw new ArgumentNullException(nameof(text));
			if (text.IndexOfAny(new[] { '\r', '\n' }) >= 0)
				throw new ArgumentException("A run cannot contain paragraph separators.", nameof(text));
			for (int i = 0; i < text.Length; i++)
			{
				if (char.IsHighSurrogate(text[i]))
				{
					if (++i >= text.Length || !char.IsLowSurrogate(text[i]))
						throw new ArgumentException("Text contains an unpaired surrogate.", nameof(text));
				}
				else if (char.IsLowSurrogate(text[i]))
					throw new ArgumentException("Text contains an unpaired surrogate.", nameof(text));
			}
			Text = text;
			Style = style ?? RichTextStyle.Default;
		}

		/// <summary>Gets the run's text.</summary>
		public string Text { get; }
		/// <summary>Gets the run's character formatting.</summary>
		public RichTextStyle Style { get; }
	}
}
