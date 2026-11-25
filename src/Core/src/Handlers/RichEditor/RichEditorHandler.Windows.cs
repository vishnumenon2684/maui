#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Microsoft.Maui.Handlers
{
	public partial class RichEditorHandler
	{
		protected override RichEditBox CreatePlatformView() => new NativeRichEditor
		{
			AcceptsReturn = true,
			TextWrapping = TextWrapping.Wrap,
			AllowDrop = false
		};

		protected override void ConnectHandler(RichEditBox platformView)
		{
			base.ConnectHandler(platformView);
			((NativeRichEditor)platformView).Connect(this);
		}

		protected override void DisconnectHandler(RichEditBox platformView)
		{
			((NativeRichEditor)platformView).Disconnect();
			base.DisconnectHandler(platformView);
		}

		void Dispatch(Action<NativeRichEditor> update)
		{
			if (PlatformView is not NativeRichEditor native)
				return;
			if (native.DispatcherQueue.HasThreadAccess)
			{
				if (VirtualView != null && native.IsConnectedTo(this))
					update(native);
			}
			else
				native.DispatcherQueue.TryEnqueue(() =>
				{
					if (VirtualView != null && ReferenceEquals(PlatformView, native) && native.IsConnectedTo(this))
						update(native);
				});
		}

		partial void UpdateDocument() => Dispatch(native => native.UpdateDocument());
		partial void UpdateSelection() => Dispatch(native => native.UpdateSelection());
		partial void UpdateTypingStyle() => Dispatch(native => native.UpdateTypingStyle());
		partial void UpdateIsReadOnly() => Dispatch(native => native.IsReadOnly = VirtualView.IsReadOnly);

		sealed class NativeRichEditor : RichEditBox
		{
			RichEditorHandler? _owner;
			ITextCharacterFormat? _defaultCharacterFormat;
			ITextParagraphFormat? _defaultParagraphFormat;
			FlyoutBase? _defaultContextFlyout;
			FlyoutBase? _defaultSelectionFlyout;
			uint _defaultUndoLimit;
			bool _defaultIsReadOnly;
			bool _updating;
			bool _committing;
			bool _composing;
			bool _pasting;
			RichTextDocument? _compositionDocument;
			RichTextDocument? _renderedDocument;

			IRichEditor? Editor => _owner?.VirtualView;

			internal bool IsConnectedTo(RichEditorHandler owner) => ReferenceEquals(_owner, owner);

			internal void Connect(RichEditorHandler owner)
			{
				_owner = owner;
				_defaultCharacterFormat = Document.GetDefaultCharacterFormat().GetClone();
				_defaultParagraphFormat = Document.GetDefaultParagraphFormat().GetClone();
				_defaultUndoLimit = Document.UndoLimit;
				_defaultIsReadOnly = IsReadOnly;
				_defaultContextFlyout = ContextFlyout;
				_defaultSelectionFlyout = SelectionFlyout;
				// The model owns history; native formatting transactions must never enter it.
				Document.UndoLimit = 0;
				ContextFlyout = CreateMenu();
				SelectionFlyout = CreateMenu();
				TextChanged += OnTextChanged;
				SelectionChanged += OnSelectionChanged;
				TextCompositionStarted += OnCompositionStarted;
				TextCompositionEnded += OnCompositionEnded;
				Paste += OnPaste;
				PreviewKeyDown += OnPreviewKeyDown;
			}

			internal void Disconnect()
			{
				TextChanged -= OnTextChanged;
				SelectionChanged -= OnSelectionChanged;
				TextCompositionStarted -= OnCompositionStarted;
				TextCompositionEnded -= OnCompositionEnded;
				Paste -= OnPaste;
				PreviewKeyDown -= OnPreviewKeyDown;
				_owner = null;
				_updating = true;
				try
				{
					ContextFlyout = _defaultContextFlyout;
					SelectionFlyout = _defaultSelectionFlyout;
					Document.UndoLimit = _defaultUndoLimit;
					IsReadOnly = _defaultIsReadOnly;
					if (_defaultCharacterFormat != null)
						Document.SetDefaultCharacterFormat(_defaultCharacterFormat);
					if (_defaultParagraphFormat != null)
						Document.SetDefaultParagraphFormat(_defaultParagraphFormat);
				}
				finally
				{
					_defaultCharacterFormat = null;
					_defaultParagraphFormat = null;
					_defaultContextFlyout = null;
					_defaultSelectionFlyout = null;
					_renderedDocument = null;
					_compositionDocument = null;
					_composing = _committing = _pasting = _updating = false;
				}
			}

			internal void UpdateDocument()
			{
				if (Editor is not { } editor || _composing || _committing || _updating)
					return;
				_updating = true;
				try
				{
					var model = editor.Document;
					var map = ReadOffsets();
					if (map.Text != model.Text)
					{
						FindChange(map.Text, model.Text, out int start, out int oldEnd, out int newEnd);
						Document.GetRange(map.ToNative(start), map.ToNative(oldEnd))
							.SetText(TextSetOptions.None, model.Text.Substring(start, newEnd - start).Replace('\n', '\r'));
						map = ReadOffsets();
						_renderedDocument = null;
					}
					ApplyFormatting(model, map);
					_renderedDocument = model;
					SetSelection(editor, map);
					SetTypingStyle(editor);
				}
				finally
				{
					_updating = false;
				}
			}

			void ApplyFormatting(RichTextDocument model, NativeOffsets map)
			{
				if (ReferenceEquals(model, _renderedDocument))
					return;
				int offset = 0;
				for (int i = 0; i < model.Paragraphs.Count; i++)
				{
					var paragraph = model.Paragraphs[i];
					int end = offset + paragraph.Text.Length;
					if (_renderedDocument is { } previous && i < previous.Paragraphs.Count &&
						SameParagraph(paragraph, previous.Paragraphs[i]) &&
						(paragraph.ListKind != RichTextListKind.Numbered || model.GetListNumber(i) == previous.GetListNumber(i)))
					{
						offset = end + 1;
						continue;
					}
					// Include the paragraph mark, including the undeletable final native mark.
					var range = Document.GetRange(map.ToNative(offset),
						i == model.Paragraphs.Count - 1 ? map.NativeEnd : map.ToNative(end + 1));
					range.CharacterFormat = _defaultCharacterFormat!.GetClone();
					var format = _defaultParagraphFormat!.GetClone();
					format.ListType = paragraph.ListKind switch
					{
						RichTextListKind.Bullet => MarkerType.Bullet,
						RichTextListKind.Numbered => MarkerType.Arabic,
						_ => MarkerType.None
					};
					format.ListLevelIndex = paragraph.ListKind == RichTextListKind.None ? 0 : paragraph.ListLevel + 1;
					if (paragraph.ListKind != RichTextListKind.None)
					{
						format.ListStyle = paragraph.ListKind == RichTextListKind.Numbered ? MarkerStyle.Period : MarkerStyle.Plain;
						format.ListStart = paragraph.ListKind == RichTextListKind.Numbered ? model.GetListNumber(i) : 1;
						format.SetIndents(-18, 18 * (paragraph.ListLevel + 1), 0);
						format.ListTab = 18;
					}
					range.ParagraphFormat = format;
					foreach (var run in paragraph.Runs)
					{
						Document.GetRange(map.ToNative(offset), map.ToNative(offset + run.Text.Length))
							.CharacterFormat = CharacterFormat(run.Style);
						offset += run.Text.Length;
					}
					offset = end + 1;
				}
			}

			static bool SameParagraph(RichTextParagraph left, RichTextParagraph right)
			{
				if (ReferenceEquals(left, right))
					return true;
				if (left.Text != right.Text || left.ListKind != right.ListKind || left.ListLevel != right.ListLevel ||
					left.ListStart != right.ListStart || left.Runs.Count != right.Runs.Count)
					return false;
				for (int i = 0; i < left.Runs.Count; i++)
				{
					if (left.Runs[i].Text != right.Runs[i].Text || !left.Runs[i].Style.Equals(right.Runs[i].Style))
						return false;
				}
				return true;
			}

			ITextCharacterFormat CharacterFormat(RichTextStyle style)
			{
				var format = _defaultCharacterFormat!.GetClone();
				format.Bold = style.Bold ? FormatEffect.On : FormatEffect.Off;
				format.Italic = style.Italic ? FormatEffect.On : FormatEffect.Off;
				format.Underline = style.Underline ? UnderlineType.Single : UnderlineType.None;
				if (style.FontFamily != null)
					format.Name = style.FontFamily;
				if (style.FontSize is double size)
					format.Size = (float)(size * 72 / 96);
				return format;
			}

			internal void UpdateSelection()
			{
				if (Editor is not { } editor || _composing || _committing || _updating)
					return;
				_updating = true;
				try
				{
					SetSelection(editor, ReadOffsets());
				}
				finally
				{
					_updating = false;
				}
			}

			void SetSelection(IRichEditor editor, NativeOffsets map)
			{
				int start = map.ToNative(editor.CursorPosition);
				int end = map.ToNative(editor.CursorPosition + editor.SelectionLength);
				if (Document.Selection.StartPosition != start || Document.Selection.EndPosition != end)
					Document.Selection.SetRange(start, end);
			}

			internal void UpdateTypingStyle()
			{
				if (Editor is not { } editor || _composing || _committing || _updating)
					return;
				_updating = true;
				try
				{
					SetTypingStyle(editor);
				}
				finally
				{
					_updating = false;
				}
			}

			void SetTypingStyle(IRichEditor editor)
			{
				if (Document.Selection.StartPosition == Document.Selection.EndPosition)
					Document.Selection.CharacterFormat = CharacterFormat(editor.TypingStyle);
			}

			void OnTextChanged(object sender, RoutedEventArgs args)
			{
				if (!_updating && !_composing && !_committing)
					CommitText();
			}

			void CommitText()
			{
				if (Editor is not { } editor)
					return;
				if (editor.IsReadOnly || !editor.IsEnabled)
				{
					_owner?.UpdateValue(nameof(IRichEditor.Document));
					return;
				}
				var map = ReadOffsets();
				if (map.Text == editor.Document.Text)
					return;
				int start = map.ToLogical(Document.Selection.StartPosition);
				int end = map.ToLogical(Document.Selection.EndPosition);
				_committing = true;
				try
				{
					editor.UpdateText(map.Text, start, Math.Max(0, end - start));
				}
				finally
				{
					_committing = false;
				}
				// The model may normalize the edit (for example, Enter on an empty list item).
				_owner?.UpdateValue(nameof(IRichEditor.Document));
			}

			void OnSelectionChanged(object sender, RoutedEventArgs args)
			{
				if (Editor is not { } editor || _updating || _committing || _composing)
					return;
				var map = ReadOffsets();
				// Selection can change before TextChanged; do not send offsets into a stale model.
				if (map.Text != editor.Document.Text)
					return;
				int start = map.ToLogical(Document.Selection.StartPosition);
				int end = map.ToLogical(Document.Selection.EndPosition);
				editor.UpdateSelection(start, Math.Max(0, end - start));
			}

			void OnCompositionStarted(RichEditBox sender, TextCompositionStartedEventArgs args)
			{
				_composing = true;
				_compositionDocument = Editor?.Document;
			}

			void OnCompositionEnded(RichEditBox sender, TextCompositionEndedEventArgs args)
			{
				_composing = false;
				// An external document replacement wins over the in-flight native composition.
				if (ReferenceEquals(Editor?.Document, _compositionDocument))
					CommitText();
				_compositionDocument = null;
				_owner?.UpdateValue(nameof(IRichEditor.Document));
				_owner?.UpdateValue(nameof(IRichEditor.CursorPosition));
				_owner?.UpdateValue(nameof(IRichEditor.TypingStyle));
			}

			void OnPaste(object sender, TextControlPasteEventArgs args)
			{
				args.Handled = true;
				PastePlainText();
			}

			void PastePlainText()
			{
				if (!CanEdit() || _pasting)
					return;
				_pasting = true;
				try
				{
					// CF_UNICODETEXT excludes RTF, embedded objects, and clipboard list markers.
					if (Clipboard.GetContent().Contains(StandardDataFormats.Text))
						Document.Selection.Paste(13);
				}
				catch (COMException)
				{
					// Another process can take ownership of the clipboard between checking and pasting.
				}
				finally
				{
					_pasting = false;
				}
				CommitText();
			}

			static bool IsKeyDown(VirtualKey key) =>
				(InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;

			void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
			{
				if (Editor is not { } editor)
					return;
				bool control = IsKeyDown(VirtualKey.Control);
				bool shift = IsKeyDown(VirtualKey.Shift);
				bool alt = IsKeyDown(VirtualKey.Menu);
				if (args.Key == VirtualKey.Enter && !control && !shift && !alt && CanEdit() &&
					editor.SelectionLength == 0 && IsEmptyListItem(editor))
				{
					args.Handled = true;
					int cursor = editor.CursorPosition;
					editor.UpdateText(editor.Document.Text.Insert(cursor, "\n"), cursor + 1, 0);
				}
				else if ((control && (args.Key == VirtualKey.Z || args.Key == VirtualKey.Y)) ||
					(alt && args.Key == VirtualKey.Back))
				{
					args.Handled = true;
					if (CanEdit())
					{
						if (args.Key == VirtualKey.Y || shift)
							editor.Redo();
						else
							editor.Undo();
					}
				}
				else if (control && (args.Key == VirtualKey.B || args.Key == VirtualKey.I || args.Key == VirtualKey.U))
					args.Handled = true;
				else if ((control && args.Key == VirtualKey.V) || (shift && args.Key == VirtualKey.Insert))
				{
					args.Handled = true;
					PastePlainText();
				}
			}

			static bool IsEmptyListItem(IRichEditor editor)
			{
				int index = editor.Document.ParagraphIndex(editor.CursorPosition, out _);
				var paragraph = editor.Document.Paragraphs[index];
				return paragraph.Text.Length == 0 && paragraph.ListKind != RichTextListKind.None;
			}

			MenuFlyout CreateMenu()
			{
				var menu = new MenuFlyout();
				AddMenuItem(menu, StandardUICommandKind.Undo, () => { if (CanEdit()) Editor?.Undo(); });
				AddMenuItem(menu, StandardUICommandKind.Redo, () => { if (CanEdit()) Editor?.Redo(); });
				menu.Items.Add(new MenuFlyoutSeparator());
				AddMenuItem(menu, StandardUICommandKind.Cut, () => { if (CanEdit()) Document.Selection.Cut(); });
				AddMenuItem(menu, StandardUICommandKind.Copy, () => { if (Editor != null && !_composing) Document.Selection.Copy(); });
				AddMenuItem(menu, StandardUICommandKind.Paste, PastePlainText);
				AddMenuItem(menu, StandardUICommandKind.SelectAll, () =>
				{
					if (Editor != null && !_composing)
					{
						var map = ReadOffsets();
						Document.Selection.SetRange(0, map.ToNative(map.Text.Length));
					}
				});
				return menu;
			}

			bool CanEdit() => Editor is { IsReadOnly: false, IsEnabled: true } && !_composing;

			static void AddMenuItem(MenuFlyout menu, StandardUICommandKind kind, Action action)
			{
				var command = new StandardUICommand(kind);
				var item = new MenuFlyoutItem { Text = command.Label, AccessKey = command.AccessKey };
				item.Click += (_, _) => action();
				menu.Items.Add(item);
			}

			NativeOffsets ReadOffsets()
			{
				// Numbering is layout metadata, not text: never request IncludeNumbering.
				Document.GetText(TextGetOptions.AllowFinalEop, out string text);
				return new NativeOffsets(text);
			}

			static void FindChange(string before, string after, out int start, out int oldEnd, out int newEnd)
			{
				start = 0;
				while (start < before.Length && start < after.Length && before[start] == after[start])
					start++;
				if (start > 0 && ((start < before.Length && char.IsLowSurrogate(before[start])) ||
					(start < after.Length && char.IsLowSurrogate(after[start]))))
					start--;
				oldEnd = before.Length;
				newEnd = after.Length;
				while (oldEnd > start && newEnd > start && before[oldEnd - 1] == after[newEnd - 1])
				{
					oldEnd--;
					newEnd--;
				}
				if ((oldEnd < before.Length && char.IsLowSurrogate(before[oldEnd])) ||
					(newEnd < after.Length && char.IsLowSurrogate(after[newEnd])))
				{
					oldEnd++;
					newEnd++;
				}
			}

			sealed class NativeOffsets
			{
				readonly int[] _logicalToNative;
				readonly int[] _nativeToLogical;

				internal NativeOffsets(string native)
				{
					NativeEnd = native.Length;
					int end = native.Length;
					// Remove exactly one final native paragraph, preserving user-entered newlines.
					if (end > 0 && native[end - 1] == '\n')
					{
						end--;
						if (end > 0 && native[end - 1] == '\r')
							end--;
					}
					else if (end > 0 && native[end - 1] == '\r')
						end--;
					var text = new StringBuilder(end);
					var offsets = new List<int> { 0 };
					_nativeToLogical = new int[native.Length + 1];
					for (int i = 0; i < end; i++)
					{
						_nativeToLogical[i] = text.Length;
						char character = native[i];
						if (character == '\r')
						{
							if (i + 1 < end && native[i + 1] == '\n')
								_nativeToLogical[++i] = text.Length;
							character = '\n';
						}
						text.Append(character);
						offsets.Add(i + 1);
					}
					for (int i = end; i <= native.Length; i++)
						_nativeToLogical[i] = text.Length;
					Text = text.ToString();
					_logicalToNative = offsets.ToArray();
				}

				internal string Text { get; }
				internal int NativeEnd { get; }
				internal int ToNative(int logical) => _logicalToNative[Math.Max(0, Math.Min(logical, Text.Length))];
				internal int ToLogical(int native)
				{
					int logical = _nativeToLogical[Math.Max(0, Math.Min(native, _nativeToLogical.Length - 1))];
					if (logical > 0 && logical < Text.Length && char.IsHighSurrogate(Text[logical - 1]) &&
						char.IsLowSurrogate(Text[logical]))
						logical--;
					return logical;
				}
			}
		}
	}
}
