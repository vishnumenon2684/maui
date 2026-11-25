namespace Microsoft.Maui
{
	/// <summary>A native rich text editing view with a structured document.</summary>
	public interface IRichEditor : IView
	{
		/// <summary>Gets the authoritative document.</summary>
		RichTextDocument Document { get; }
		/// <summary>Gets the UTF-16 selection start in logical document text.</summary>
		int CursorPosition { get; }
		/// <summary>Gets the UTF-16 selection length.</summary>
		int SelectionLength { get; }
		/// <summary>Gets the formatting applied to newly typed characters.</summary>
		RichTextStyle TypingStyle { get; }
		/// <summary>Gets whether editing is disabled.</summary>
		bool IsReadOnly { get; }
		/// <summary>Gets whether a shared editing transaction can be undone.</summary>
		bool CanUndo { get; }
		/// <summary>Gets whether a shared editing transaction can be redone.</summary>
		bool CanRedo { get; }
		/// <summary>Receives committed native text and selection. Composition is committed only when complete.</summary>
		void UpdateText(string text, int cursorPosition, int selectionLength);
		/// <summary>Receives native selection changes.</summary>
		void UpdateSelection(int cursorPosition, int selectionLength);
		/// <summary>Undoes the latest editing transaction.</summary>
		void Undo();
		/// <summary>Redoes the latest undone editing transaction.</summary>
		void Redo();
	}
}
