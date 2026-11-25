using System;
using System.Collections.Generic;

namespace Microsoft.Maui.Controls
{
	/// <summary>A native rich text editor, independent of the plain-text Editor control.</summary>
	public partial class RichEditor : View, IRichEditor
	{
		/// <summary>Identifies the two-way document property.</summary>
		public static readonly BindableProperty DocumentProperty = BindableProperty.Create(nameof(Document), typeof(RichTextDocument), typeof(RichEditor),
			RichTextDocument.Empty, BindingMode.TwoWay, validateValue: (_, value) => value is RichTextDocument,
			propertyChanged: (bindable, _, _) => ((RichEditor)bindable).DocumentChanged());
		/// <summary>Identifies the logical UTF-16 cursor position property.</summary>
		public static readonly BindableProperty CursorPositionProperty = BindableProperty.Create(nameof(CursorPosition), typeof(int), typeof(RichEditor), 0,
			BindingMode.TwoWay, validateValue: (bindable, value) => ((RichEditor)bindable).ValidSelection((int)value, ((RichEditor)bindable).SelectionLength),
			propertyChanged: (bindable, _, _) => ((RichEditor)bindable).SelectionChanged());
		/// <summary>Identifies the logical UTF-16 selection length property.</summary>
		public static readonly BindableProperty SelectionLengthProperty = BindableProperty.Create(nameof(SelectionLength), typeof(int), typeof(RichEditor), 0,
			BindingMode.TwoWay, validateValue: (bindable, value) => ((RichEditor)bindable).ValidSelection(((RichEditor)bindable).CursorPosition, (int)value),
			propertyChanged: (bindable, _, _) => ((RichEditor)bindable).SelectionChanged());
		/// <summary>Identifies the style applied to newly typed characters.</summary>
		public static readonly BindableProperty TypingStyleProperty = BindableProperty.Create(nameof(TypingStyle), typeof(RichTextStyle), typeof(RichEditor),
			RichTextStyle.Default, validateValue: (_, value) => value is RichTextStyle,
			propertyChanged: (bindable, _, _) => ((RichEditor)bindable)._typingTransaction = false);
		/// <summary>Identifies the read-only property.</summary>
		public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create(nameof(IsReadOnly), typeof(bool), typeof(RichEditor), false);

		readonly Stack<Snapshot> _undo = new Stack<Snapshot>();
		readonly Stack<Snapshot> _redo = new Stack<Snapshot>();
		bool _editing;
		bool _settingSelection;
		bool _typingTransaction;
		int _typingEnd;

		/// <summary>Creates an empty rich editor.</summary>
		public RichEditor() { }

		/// <summary>Gets or replaces the rich document. Replacement clears edit history.</summary>
		public RichTextDocument Document
		{
			get => (RichTextDocument)GetValue(DocumentProperty);
			set => SetValue(DocumentProperty, value);
		}
		/// <summary>Gets or sets the selection start, excluding rendered list markers.</summary>
		public int CursorPosition
		{
			get => (int)GetValue(CursorPositionProperty);
			set => SetValue(CursorPositionProperty, value);
		}
		/// <summary>Gets or sets the selection length in UTF-16 units.</summary>
		public int SelectionLength
		{
			get => (int)GetValue(SelectionLengthProperty);
			set => SetValue(SelectionLengthProperty, value);
		}
		/// <summary>Gets or sets the formatting used for inserted text.</summary>
		public RichTextStyle TypingStyle
		{
			get => (RichTextStyle)GetValue(TypingStyleProperty);
			set => SetValue(TypingStyleProperty, value);
		}
		/// <summary>Gets or sets whether user editing and formatting commands are disabled.</summary>
		public bool IsReadOnly
		{
			get => (bool)GetValue(IsReadOnlyProperty);
			set => SetValue(IsReadOnlyProperty, value);
		}
		/// <summary>Gets whether an edit can be undone.</summary>
		public bool CanUndo => _undo.Count > 0;
		/// <summary>Gets whether an edit can be redone.</summary>
		public bool CanRedo => _redo.Count > 0;
		/// <summary>Occurs after the document changes.</summary>
		public event EventHandler? TextChanged;

		/// <summary>Sets an entire selection atomically. Selections cannot split surrogate pairs.</summary>
		public void Select(int start, int length)
		{
			Document.ValidateRange(start, length);
			if (start == CursorPosition && length == SelectionLength)
				return;
			_settingSelection = true;
			try
			{
				CursorPosition = start;
				SelectionLength = length;
			}
			finally
			{
				_settingSelection = false;
			}
			SelectionChanged();
		}

		/// <summary>Toggles bold on the selection, or the typing style for an empty selection.</summary>
		public void ApplyBold() => ChangeStyle(s => new RichTextStyle(!TypingStyle.Bold, s.Italic, s.Underline, s.FontFamily, s.FontSize));
		/// <summary>Toggles italic on the selection, or the typing style for an empty selection.</summary>
		public void ApplyItalic() => ChangeStyle(s => new RichTextStyle(s.Bold, !TypingStyle.Italic, s.Underline, s.FontFamily, s.FontSize));
		/// <summary>Toggles underline on the selection, or the typing style for an empty selection.</summary>
		public void ApplyUnderline() => ChangeStyle(s => new RichTextStyle(s.Bold, s.Italic, !TypingStyle.Underline, s.FontFamily, s.FontSize));

		/// <summary>Applies a font override; null values restore platform defaults.</summary>
		public void ApplyFont(string? fontFamily, double? fontSize)
		{
			_ = new RichTextStyle(fontFamily: fontFamily, fontSize: fontSize);
			ChangeStyle(s => new RichTextStyle(s.Bold, s.Italic, s.Underline, fontFamily, fontSize));
		}

		/// <summary>Applies list formatting to selected paragraphs. None removes list formatting.</summary>
		public void ApplyList(RichTextListKind kind, int level = 0, int start = 1)
		{
			EnsureEditable();
			var document = Document.SetList(CursorPosition, SelectionLength, kind, level, start);
			Commit(document, CursorPosition, SelectionLength, false);
		}

		/// <summary>Inserts plain text into the selection using its current typing style.</summary>
		public void InsertText(string text)
		{
			EnsureEditable();
			if (text == null)
				throw new ArgumentNullException(nameof(text));
			text = RichTextDocument.NormalizeText(text);
			var document = Document.Replace(CursorPosition, SelectionLength, text, TypingStyle);
			int insertedLength = document.Text.Length - Document.Text.Length + SelectionLength;
			int cursor = CursorPosition + insertedLength;
			Commit(document, cursor, 0, false);
		}

		/// <summary>Undoes the latest edit, restoring selection and typing style.</summary>
		public void Undo()
		{
			EnsureEditable();
			if (!CanUndo)
				return;
			_redo.Push(Capture());
			Restore(_undo.Pop());
			NotifyHistory();
		}

		/// <summary>Redoes the latest undone edit.</summary>
		public void Redo()
		{
			EnsureEditable();
			if (!CanRedo)
				return;
			_undo.Push(Capture());
			Restore(_redo.Pop());
			NotifyHistory();
		}

		void IRichEditor.UpdateText(string text, int cursorPosition, int selectionLength)
		{
			EnsureEditable();
			text = RichTextDocument.NormalizeText(text);
			string old = Document.Text;
			if (text == old)
			{
				Select(cursorPosition, selectionLength);
				return;
			}
			int prefix = 0;
			while (prefix < old.Length && prefix < text.Length && old[prefix] == text[prefix])
				prefix++;
			while (prefix > 0 && (!Document.IsBoundary(prefix) || !TextBoundary(text, prefix)))
				prefix--;
			int suffix = 0;
			while (suffix < old.Length - prefix && suffix < text.Length - prefix && old[old.Length - suffix - 1] == text[text.Length - suffix - 1])
				suffix++;
			while (suffix > 0 && (!Document.IsBoundary(old.Length - suffix) || !TextBoundary(text, text.Length - suffix)))
				suffix--;
			// Prefer the known selection when repeated text makes the diff ambiguous.
			int selectionInsertion = text.Length - old.Length + SelectionLength;
			int selectionSuffix = old.Length - CursorPosition - SelectionLength;
			if (selectionInsertion >= 0 &&
				string.CompareOrdinal(old, 0, text, 0, CursorPosition) == 0 &&
				string.CompareOrdinal(old, CursorPosition + SelectionLength, text, CursorPosition + selectionInsertion, selectionSuffix) == 0)
			{
				prefix = CursorPosition;
				suffix = selectionSuffix;
			}
			string inserted = text.Substring(prefix, text.Length - prefix - suffix);
			int removed = old.Length - prefix - suffix;
			bool typing = removed == 0 && inserted.Length > 0 && inserted.IndexOf("\n", StringComparison.Ordinal) < 0;
			var document = Document.Replace(prefix, removed, inserted, TypingStyle);
			int adjustment = text.Length - document.Text.Length;
			cursorPosition = Math.Min(Math.Max(0, cursorPosition - adjustment), document.Text.Length);
			selectionLength = Math.Min(selectionLength, document.Text.Length - cursorPosition);
			document.ValidateRange(cursorPosition, selectionLength);
			Commit(document, cursorPosition, selectionLength, typing && _typingTransaction && prefix == _typingEnd);
			_typingTransaction = typing;
			_typingEnd = cursorPosition;
		}

		void IRichEditor.UpdateSelection(int cursorPosition, int selectionLength) => Select(cursorPosition, selectionLength);

		static bool TextBoundary(string text, int position) => position == 0 || position == text.Length ||
			!(char.IsHighSurrogate(text[position - 1]) && char.IsLowSurrogate(text[position]));

		bool ValidSelection(int start, int length) => _settingSelection || (start >= 0 && length >= 0 &&
			start <= Document.Text.Length && length <= Document.Text.Length - start && Document.IsBoundary(start) && Document.IsBoundary(start + length));

		void SelectionChanged()
		{
			if (_settingSelection)
				return;
			if (SelectionLength > Document.Text.Length - CursorPosition)
				Select(CursorPosition, Document.Text.Length - CursorPosition);
			if (!_editing)
			{
				_typingTransaction = false;
				TypingStyle = Document.StyleAt(CursorPosition);
			}
		}

		void DocumentChanged()
		{
			if (!_editing)
			{
				_undo.Clear();
				_redo.Clear();
				_typingTransaction = false;
				int position = Math.Min(CursorPosition, Document.Text.Length);
				if (!Document.IsBoundary(position))
					position--;
				Select(position, 0);
				TypingStyle = Document.StyleAt(position);
				NotifyHistory();
			}
			TextChanged?.Invoke(this, EventArgs.Empty);
		}

		void ChangeStyle(Func<RichTextStyle, RichTextStyle> change)
		{
			EnsureEditable();
			if (SelectionLength > 0)
			{
				var style = change(TypingStyle);
				Commit(Document.Format(CursorPosition, SelectionLength, change), CursorPosition, SelectionLength, false, style);
			}
			else
			{
				_undo.Push(Capture());
				_redo.Clear();
				_typingTransaction = false;
				TypingStyle = change(TypingStyle);
				NotifyHistory();
			}
		}

		void Commit(RichTextDocument document, int cursor, int length, bool coalesce, RichTextStyle? style = null)
		{
			if (!coalesce)
				_undo.Push(Capture());
			_redo.Clear();
			Restore(new Snapshot(document, cursor, length, style ?? TypingStyle));
			NotifyHistory();
		}

		Snapshot Capture() => new Snapshot(Document, CursorPosition, SelectionLength, TypingStyle);

		void Restore(Snapshot snapshot)
		{
			_editing = true;
			try
			{
				Document = snapshot.Document;
				Select(snapshot.Cursor, snapshot.Length);
				TypingStyle = snapshot.Style;
			}
			finally
			{
				_editing = false;
				_typingTransaction = false;
			}
		}

		void EnsureEditable()
		{
			if (IsReadOnly || !IsEnabled)
				throw new InvalidOperationException("RichEditor cannot be edited while read-only or disabled.");
		}

		void NotifyHistory()
		{
			OnPropertyChanged(nameof(CanUndo));
			OnPropertyChanged(nameof(CanRedo));
		}

		readonly struct Snapshot
		{
			public Snapshot(RichTextDocument document, int cursor, int length, RichTextStyle style)
			{
				Document = document;
				Cursor = cursor;
				Length = length;
				Style = style;
			}
			public RichTextDocument Document { get; }
			public int Cursor { get; }
			public int Length { get; }
			public RichTextStyle Style { get; }
		}
	}
}
