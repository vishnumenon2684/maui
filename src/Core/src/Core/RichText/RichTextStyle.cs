using System;

namespace Microsoft.Maui
{
	/// <summary>Immutable character formatting for a rich text run.</summary>
	public sealed class RichTextStyle : IEquatable<RichTextStyle>
	{
		/// <summary>Gets the platform-default character style.</summary>
		public static RichTextStyle Default { get; } = new RichTextStyle();

		/// <summary>Creates character formatting. A null font or size uses the native default.</summary>
		public RichTextStyle(bool bold = false, bool italic = false, bool underline = false, string? fontFamily = null, double? fontSize = null)
		{
			if (fontFamily != null && string.IsNullOrWhiteSpace(fontFamily))
				throw new ArgumentException("A font family must not be empty.", nameof(fontFamily));
			if (fontSize.HasValue && (fontSize.Value <= 0 || double.IsNaN(fontSize.Value) || double.IsInfinity(fontSize.Value)))
				throw new ArgumentOutOfRangeException(nameof(fontSize));
			Bold = bold;
			Italic = italic;
			Underline = underline;
			FontFamily = fontFamily;
			FontSize = fontSize;
		}

		/// <summary>Gets whether characters are bold.</summary>
		public bool Bold { get; }
		/// <summary>Gets whether characters are italic.</summary>
		public bool Italic { get; }
		/// <summary>Gets whether characters are underlined.</summary>
		public bool Underline { get; }
		/// <summary>Gets the font family override.</summary>
		public string? FontFamily { get; }
		/// <summary>Gets the font size override in device-independent units.</summary>
		public double? FontSize { get; }

		/// <inheritdoc/>
		public bool Equals(RichTextStyle? other) => other != null && Bold == other.Bold && Italic == other.Italic &&
			Underline == other.Underline && FontFamily == other.FontFamily && FontSize == other.FontSize;
		/// <inheritdoc/>
		public override bool Equals(object? obj) => obj is RichTextStyle style && Equals(style);
		/// <inheritdoc/>
		public override int GetHashCode()
		{
			unchecked
			{
				int hash = Bold.GetHashCode();
				hash = hash * 397 ^ Italic.GetHashCode();
				hash = hash * 397 ^ Underline.GetHashCode();
				hash = hash * 397 ^ (FontFamily == null ? 0 : StringComparer.Ordinal.GetHashCode(FontFamily));
				return hash * 397 ^ FontSize.GetHashCode();
			}
		}
	}
}
