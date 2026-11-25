using System;
using System.Collections.Generic;
using System.Globalization;
using CoreFoundation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using ObjCRuntime;
using UIKit;

namespace Microsoft.Maui.Handlers
{
	public partial class RichEditorHandler : ViewHandler<IRichEditor, UITextView>
	{
		NativeRichEditor.NativeResources? _nativeResources;

		/// <inheritdoc/>
		protected override UITextView CreatePlatformView() => new NativeRichEditor();

		/// <inheritdoc/>
		protected override void ConnectHandler(UITextView platformView)
		{
			base.ConnectHandler(platformView);
			_nativeResources = new NativeRichEditor.NativeResources();
			((NativeRichEditor)platformView).Connect(this, _nativeResources);
		}

		/// <inheritdoc/>
		protected override void DisconnectHandler(UITextView platformView)
		{
			((NativeRichEditor)platformView).Disconnect();
			_nativeResources?.Dispose();
			_nativeResources = null;
			base.DisconnectHandler(platformView);
		}

		partial void UpdateDocument() => OnMainThread(view => view.MapDocument());
		partial void UpdateSelection() => OnMainThread(view => view.MapSelection());
		partial void UpdateTypingStyle() => OnMainThread(view => view.MapTypingStyle());
		partial void UpdateIsReadOnly() => OnMainThread(view => view.MapIsReadOnly());

		void OnMainThread(Action<NativeRichEditor> update)
		{
			var platformView = PlatformView;
			var editor = VirtualView;
			if (platformView is not NativeRichEditor native || editor == null)
				return;

			void Apply()
			{
				if (PlatformView == native && VirtualView == editor && native.IsConnected)
					update(native);
			}

			if (MainThread.IsMainThread)
				Apply();
			else
				MainThread.BeginInvokeOnMainThread(Apply);
		}

		sealed class NativeRichEditor : UITextView
		{
			WeakReference<RichEditorHandler>? _handler;
			WeakReference<NativeResources>? _resources;
			RichTextDocument? _renderedDocument;
			RichTextDocument? _deferredDocument;
			int _deferredCursor;
			int _deferredLength;
			bool _updating;
			bool _reporting;
			bool _settingMarkedText;
			bool _composing;
			bool _refreshRequested;
			readonly List<ListMarker> _markers = new();
			readonly Dictionary<RichTextStyle, UIFont> _fonts = new();

			IRichEditor? Editor => _handler != null && _handler.TryGetTarget(out var handler) ? handler.VirtualView : null;
			NativeResources? Resources => _resources != null && _resources.TryGetTarget(out var resources) ? resources : null;
			public bool IsConnected => Editor != null;

			public void Connect(RichEditorHandler handler, NativeResources resources)
			{
				_handler = new(handler);
				_resources = new(resources);
				resources.DefaultFont = Font ?? UIFont.PreferredBody ?? UIFont.SystemFontOfSize(UIFont.SystemFontSize);
				resources.DefaultColor = TextColor ?? UIColor.Label;
				resources.DefaultParagraph = new NSMutableParagraphStyle();
				resources.DefaultParagraph.SetParagraphStyle(new UIStringAttributes(TypingAttributes2).ParagraphStyle ?? NSParagraphStyle.Default);
				resources.Delegate = new EditorDelegate(this);
				Delegate = resources.Delegate;
				resources.History = new SharedUndoManager(this);
				// UIKit must not maintain a second history alongside the shared document.
				resources.History.DisableUndoRegistration();
				AllowsEditingTextAttributes = false;
				using var z = new NSString("z");
				using var y = new NSString("y");
				resources.Commands = new[]
				{
					UIKeyCommand.Create(z, UIKeyModifierFlags.Command, new Selector("undo:")),
					UIKeyCommand.Create(z, UIKeyModifierFlags.Command | UIKeyModifierFlags.Shift, new Selector("redo:")),
					UIKeyCommand.Create(y, UIKeyModifierFlags.Command, new Selector("redo:"))
				};
			}

			public void Disconnect()
			{
				var resources = Resources;
				_handler = null;
				_resources = null;
				Delegate = null!;
				resources?.Dispose();
				_renderedDocument = null;
				_deferredDocument = null;
				_markers.Clear();
				_fonts.Clear();
				_composing = false;
				_refreshRequested = false;
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing)
					Disconnect();
				base.Dispose(disposing);
			}

			public override NSUndoManager? UndoManager => Resources?.History;
			public override UIKeyCommand[] KeyCommands => Resources?.Commands ?? Array.Empty<UIKeyCommand>();

			[Export("undo:")]
			void Undo(NSObject? sender) => PerformHistory(false);

			[Export("redo:")]
			void Redo(NSObject? sender) => PerformHistory(true);

			void PerformHistory(bool redo)
			{
				if (Editor is not { IsReadOnly: false, IsEnabled: true })
					return;
				UnmarkText();
				if (Editor is not { IsReadOnly: false, IsEnabled: true } editor)
					return;
				if (redo)
					editor.Redo();
				else
					editor.Undo();
			}

			public override bool CanPerform(Selector action, NSObject? withSender)
			{
				switch (action.Name)
				{
					case "undo:":
						return Editor is { IsReadOnly: false, IsEnabled: true, CanUndo: true };
					case "redo:":
						return Editor is { IsReadOnly: false, IsEnabled: true, CanRedo: true };
					case "cut:":
						return Editor is { IsReadOnly: false, IsEnabled: true } && base.CanPerform(action, withSender);
					case "paste:":
						return Editor is { IsReadOnly: false, IsEnabled: true } && UIPasteboard.General.HasStrings;
					case "pasteAndMatchStyle:":
					case "pasteAndGo:":
					case "pasteAndSearch:":
					case "toggleBoldface:":
					case "toggleItalics:":
					case "toggleUnderline:":
						return false;
				}
				return base.CanPerform(action, withSender);
			}

			public override void Paste(NSObject? sender)
			{
				if (Editor is not { IsReadOnly: false, IsEnabled: true } || UIPasteboard.General.String is not string text)
					return;
				UnmarkText();
				MapTypingStyle();
				InsertText(RichTextDocument.NormalizeText(text));
				NativeChanged();
			}

			public override void InsertText(string text)
			{
				if (Editor is { IsReadOnly: false, IsEnabled: true })
					base.InsertText(RichTextDocument.NormalizeText(text));
			}

			public override void SetMarkedText(string markedText, NSRange selectedRange)
			{
				if (Editor is not { IsReadOnly: false, IsEnabled: true })
					return;
				_settingMarkedText = true;
				_composing = true;
				try
				{
					base.SetMarkedText(markedText, selectedRange);
				}
				finally
				{
					_settingMarkedText = false;
				}
				if (MarkedTextRange == null)
					NativeChanged();
			}

			public override void UnmarkText()
			{
				base.UnmarkText();
				NativeChanged();
			}

			public void MapDocument()
			{
				if (Editor is not IRichEditor editor)
					return;
				if (_reporting)
				{
					_refreshRequested = true;
					return;
				}
				if (_settingMarkedText || MarkedTextRange != null)
				{
					_deferredDocument = editor.Document;
					_deferredCursor = editor.CursorPosition;
					_deferredLength = editor.SelectionLength;
					return;
				}
				ApplyDocument(editor.Document, false);
			}

			public void MapSelection()
			{
				if (Editor is not IRichEditor editor || _reporting || _updating)
					return;
				if (_settingMarkedText || MarkedTextRange != null)
				{
					_deferredCursor = editor.CursorPosition;
					_deferredLength = editor.SelectionLength;
					return;
				}
				_updating = true;
				try
				{
					SetSelection(editor.CursorPosition, editor.SelectionLength);
					ApplyTypingStyle();
				}
				finally
				{
					_updating = false;
				}
			}

			public void MapTypingStyle()
			{
				if (_reporting || _updating || _settingMarkedText || MarkedTextRange != null)
					return;
				_updating = true;
				try
				{
					ApplyTypingStyle();
				}
				finally
				{
					_updating = false;
				}
			}

			public void MapIsReadOnly()
			{
				if (Editor is not IRichEditor editor)
					return;
				if (editor.IsReadOnly && MarkedTextRange != null)
					UnmarkText();
				Editable = !editor.IsReadOnly;
				Selectable = true;
			}

			void ApplyDocument(RichTextDocument document, bool preserveSelection, NSRange? logicalSelection = null)
			{
				var selection = logicalSelection ?? SelectedRange;
				var scroll = ContentOffset;
				_updating = true;
				try
				{
					var storage = TextStorage;
					storage.BeginEditing();
					try
					{
						ReplaceChangedText(document.Text);
						if (storage.Length > 0)
						{
							var attributes = CreateAttributes(RichTextStyle.Default);
							storage.SetAttributes(attributes.Dictionary, new NSRange(0, storage.Length));
						}
						_markers.Clear();
						int offset = 0;
						for (int p = 0; p < document.Paragraphs.Count; p++)
						{
							var paragraph = document.Paragraphs[p];
							using var paragraphStyle = CreateParagraphStyle(document, p, offset);
							int paragraphLength = paragraph.Text.Length + (p + 1 < document.Paragraphs.Count ? 1 : 0);
							if (paragraphLength > 0)
								storage.AddAttribute(UIStringAttributeKey.ParagraphStyle, paragraphStyle, new NSRange(offset, paragraphLength));
							foreach (var run in paragraph.Runs)
							{
								var attributes = CreateAttributes(run.Style);
								// Add rather than replace so paragraph attributes survive run formatting.
								storage.AddAttributes(attributes.Dictionary, new NSRange(offset, run.Text.Length));
								offset += run.Text.Length;
							}
							offset++;
						}
						_renderedDocument = document;
					}
					finally
					{
						storage.EndEditing();
					}
					if (preserveSelection)
						SetSelection((int)selection.Location, (int)selection.Length);
					else if (Editor is IRichEditor editor)
						SetSelection(editor.CursorPosition, editor.SelectionLength);
					ApplyTypingStyle();
					SetContentOffset(scroll, false);
					SetNeedsDisplay();
				}
				finally
				{
					_updating = false;
				}
			}

			void ReplaceChangedText(string text)
			{
				string old = Text ?? string.Empty;
				if (old == text)
					return;
				int prefix = 0;
				while (prefix < old.Length && prefix < text.Length && old[prefix] == text[prefix])
					prefix++;
				if (prefix > 0 && prefix < old.Length && char.IsLowSurrogate(old[prefix]))
					prefix--;
				int suffix = 0;
				while (suffix < old.Length - prefix && suffix < text.Length - prefix &&
					old[old.Length - suffix - 1] == text[text.Length - suffix - 1])
					suffix++;
				if (suffix > 0 && char.IsLowSurrogate(old[old.Length - suffix]))
					suffix--;
				TextStorage.Replace(new NSRange(prefix, old.Length - prefix - suffix),
					text.Substring(prefix, text.Length - prefix - suffix));
			}

			UIStringAttributes CreateAttributes(RichTextStyle style)
			{
				if (!_fonts.TryGetValue(style, out var font))
				{
					var defaultFont = Resources?.DefaultFont ?? UIFont.SystemFontOfSize(UIFont.SystemFontSize)!;
					nfloat size = style.FontSize.HasValue ? (nfloat)style.FontSize.Value : defaultFont.PointSize;
					using var seed = new UIFontDescriptor();
					using var family = style.FontFamily == null ? null : seed.CreateWithFamily(style.FontFamily);
					var original = (style.FontFamily == null ? defaultFont.WithSize(size) :
						UIFont.FromName(style.FontFamily, size) ?? UIFont.FromDescriptor(family!, size)) ??
						defaultFont.WithSize(size)!;
					var originalDescriptor = original.FontDescriptor;
					var traits = originalDescriptor.SymbolicTraits & ~(UIFontDescriptorSymbolicTraits.Bold | UIFontDescriptorSymbolicTraits.Italic);
					if (style.Bold)
						traits |= UIFontDescriptorSymbolicTraits.Bold;
					if (style.Italic)
						traits |= UIFontDescriptorSymbolicTraits.Italic;
					using var descriptor = originalDescriptor.CreateWithTraits(traits) ??
						defaultFont.FontDescriptor.CreateWithTraits(traits);
					// Some named fonts have no matching face; UIKit's descriptor matching
					// supplies a usable fallback without changing logical run metadata.
					font = UIFont.FromDescriptor(descriptor ?? originalDescriptor, size)!;
					_fonts.Add(style, font);
				}
				return new UIStringAttributes
				{
					Font = font,
					ForegroundColor = Resources?.DefaultColor ?? UIColor.Label,
					UnderlineStyle = style.Underline ? NSUnderlineStyle.Single : NSUnderlineStyle.None
				};
			}

			NSMutableParagraphStyle CreateParagraphStyle(RichTextDocument document, int index, int offset, bool addMarker = true)
			{
				var style = new NSMutableParagraphStyle();
				if (Resources?.DefaultParagraph is NSParagraphStyle defaultParagraph)
					style.SetParagraphStyle(defaultParagraph);
				var paragraph = document.Paragraphs[index];
				if (paragraph.ListKind != RichTextListKind.None)
				{
					string label = paragraph.ListKind == RichTextListKind.Bullet ? "\u2022" :
						document.GetListNumber(index).ToString(CultureInfo.InvariantCulture) + ".";
					var attributes = CreateAttributes(paragraph.Runs.Count == 0 ? RichTextStyle.Default : paragraph.Runs[0].Style);
					using var marker = new NSAttributedString(label, attributes);
					nfloat indent = (nfloat)(24 * (paragraph.ListLevel + 1)) + marker.Size.Width;
					style.FirstLineHeadIndent = indent;
					style.HeadIndent = indent;
					if (addMarker)
						_markers.Add(new ListMarker(offset, label, attributes.Font!, indent));
				}
				return style;
			}

			void ApplyTypingStyle()
			{
				if (Editor is not IRichEditor editor)
					return;
				var attributes = CreateAttributes(editor.TypingStyle);
				var document = _renderedDocument ?? editor.Document;
				int cursor = Math.Min((int)SelectedRange.Location, document.Text.Length);
				int paragraph = document.ParagraphIndex(cursor, out _);
				using var paragraphStyle = CreateParagraphStyle(document, paragraph, cursor, false);
				attributes.ParagraphStyle = paragraphStyle;
				TypingAttributes2 = attributes.Dictionary;
			}

			void SetSelection(int cursor, int length)
			{
				string text = Text ?? string.Empty;
				int start = Math.Clamp(cursor, 0, text.Length);
				int end = start + Math.Clamp(length, 0, text.Length - start);
				if (start > 0 && start < text.Length && char.IsLowSurrogate(text[start]) && char.IsHighSurrogate(text[start - 1]))
					start--;
				if (length == 0)
					end = start;
				else if (end > 0 && end < text.Length && char.IsLowSurrogate(text[end]) && char.IsHighSurrogate(text[end - 1]))
					end++;
				var range = new NSRange(start, end - start);
				if (!SelectedRange.Equals(range))
					SelectedRange = range;
			}

			static int LogicalOffset(string native, int offset)
			{
				offset = Math.Clamp(offset, 0, native.Length);
				if (offset > 0 && offset < native.Length && char.IsLowSurrogate(native[offset]) && char.IsHighSurrogate(native[offset - 1]))
					offset--;
				int logical = offset;
				for (int i = 0; i + 1 < offset; i++)
					if (native[i] == '\r' && native[i + 1] == '\n')
					{
						logical--;
						i++;
					}
				return logical;
			}

			public void NativeChanged()
			{
				if (_updating || _reporting || Editor == null)
					return;
				if (_settingMarkedText || MarkedTextRange != null)
				{
					_composing = true;
					return;
				}
				var editor = Editor;
				string native = Text ?? string.Empty;
				string text = RichTextDocument.NormalizeText(native);
				int cursor = LogicalOffset(native, (int)SelectedRange.Location);
				int end = LogicalOffset(native, (int)(SelectedRange.Location + SelectedRange.Length));
				bool externalWins = _deferredDocument != null && _deferredDocument.Text != _renderedDocument?.Text;
				if (externalWins)
				{
					// An external replacement during IME is authoritative. Do not diff the
					// obsolete composition against that replacement in shared UpdateText.
					text = _deferredDocument!.Text;
					cursor = _deferredCursor;
					end = cursor + _deferredLength;
				}
				_deferredDocument = null;
				bool commit = _composing || externalWins || text != editor.Document.Text;
				_composing = false;
				_refreshRequested = false;
				_reporting = true;
				try
				{
					if (commit)
						editor.UpdateText(text, cursor, end - cursor);
					else
						editor.UpdateSelection(cursor, end - cursor);
				}
				finally
				{
					_reporting = false;
				}
				if (Editor != editor)
					return;
				// Reentrant shared mappings are coalesced; they cannot move the user's
				// selection while UpdateText/UpdateSelection is reporting it.
				if (commit || _refreshRequested || _renderedDocument != editor.Document)
					ApplyDocument(editor.Document, !externalWins && editor.Document.Text == text, new NSRange(cursor, end - cursor));
				else
					MapTypingStyle();
			}

			public void NativeSelectionChanged()
			{
				if (_updating || _reporting || Editor == null || _settingMarkedText || MarkedTextRange != null)
					return;
				// UIKit may report selection before its text/composition notification.
				if (_composing || RichTextDocument.NormalizeText(Text ?? string.Empty) != Editor.Document.Text)
				{
					DispatchQueue.MainQueue.DispatchAsync(() =>
					{
						if (IsConnected)
							NativeChanged();
					});
					return;
				}
				NativeChanged();
			}

			public override void Draw(CGRect rect)
			{
				base.Draw(rect);
				foreach (var marker in _markers)
				{
					using var position = GetPosition(BeginningOfDocument, marker.Offset);
					if (position == null)
						continue;
					var caret = GetCaretRectForPosition(position);
					if (caret.Bottom < rect.Top || caret.Top > rect.Bottom)
						continue;
					using var label = new NSAttributedString(marker.Label, new UIStringAttributes
					{
						Font = marker.Font,
						ForegroundColor = Resources?.DefaultColor ?? UIColor.Label
					});
					label.DrawString(new CGPoint(TextContainerInset.Left + TextContainer.LineFragmentPadding +
						marker.Indent - label.Size.Width - 8, caret.Y));
				}

			}

			public override void LayoutSubviews()
			{
				base.LayoutSubviews();
				if (_markers.Count > 0)
					SetNeedsDisplay();
			}

			sealed class ListMarker
			{
				public ListMarker(int offset, string label, UIFont font, nfloat indent)
				{
					Offset = offset;
					Label = label;
					Font = font;
					Indent = indent;
				}

				public int Offset { get; }
				public string Label { get; }
				public UIFont Font { get; }
				public nfloat Indent { get; }
			}

			public sealed class NativeResources : IDisposable
			{
				public EditorDelegate? Delegate;
				public SharedUndoManager? History;
				public UIKeyCommand[]? Commands;
				public UIFont? DefaultFont;
				public UIColor? DefaultColor;
				public NSMutableParagraphStyle? DefaultParagraph;

				public void Dispose()
				{
					Delegate?.Disconnect();
					Delegate?.Dispose();
					Delegate = null;
					History?.Disconnect();
					History?.Dispose();
					History = null;
					if (Commands != null)
						foreach (var command in Commands)
							command.Dispose();
					Commands = null;
					DefaultParagraph?.Dispose();
					DefaultParagraph = null;
					DefaultFont = null;
					DefaultColor = null;
				}
			}

			public sealed class EditorDelegate : UITextViewDelegate
			{
				WeakReference<NativeRichEditor>? _view;
				NativeRichEditor? View => _view != null && _view.TryGetTarget(out var view) ? view : null;

				public EditorDelegate(NativeRichEditor view) => _view = new(view);
				public void Disconnect() => _view = null;
				public override bool ShouldChangeText(UITextView textView, NSRange range, string text) =>
					View?.Editor is { IsReadOnly: false, IsEnabled: true };
				public override void Changed(UITextView textView) => View?.NativeChanged();
				public override void SelectionChanged(UITextView textView) => View?.NativeSelectionChanged();
				public override void EditingStarted(UITextView textView)
				{
					if (View?.Editor is IRichEditor editor)
						editor.IsFocused = true;
				}

				public override void EditingEnded(UITextView textView)
				{
					var view = View;
					view?.UnmarkText();
					if (view?.Editor is IRichEditor editor)
						editor.IsFocused = false;
				}
			}

			public sealed class SharedUndoManager : NSUndoManager
			{
				WeakReference<NativeRichEditor>? _view;
				NativeRichEditor? View => _view != null && _view.TryGetTarget(out var view) ? view : null;

				public SharedUndoManager(NativeRichEditor view) => _view = new(view);
				public void Disconnect() => _view = null;
				public override bool CanUndo => View?.Editor is { IsReadOnly: false, IsEnabled: true, CanUndo: true };
				public override bool CanRedo => View?.Editor is { IsReadOnly: false, IsEnabled: true, CanRedo: true };
				public override void Undo() => View?.PerformHistory(false);
				public override void Redo() => View?.PerformHistory(true);
			}
		}
	}
}
