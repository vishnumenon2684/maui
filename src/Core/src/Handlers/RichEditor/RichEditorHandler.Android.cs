using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.AppCompat.Widget;
using Java.Lang;
using ALayout = Android.Text.Layout;
using Math = System.Math;

namespace Microsoft.Maui.Handlers
{
	public partial class RichEditorHandler
	{
		readonly List<Java.Lang.Object> _spans = new List<Java.Lang.Object>();
		TextWatcher? _watcher;
		Typeface? _defaultTypeface;
		float _defaultSize;
		global::Android.Text.Method.IKeyListener? _defaultKeyListener;
		bool _updating;
		bool _posted;
		bool _pendingDocument;
		bool _connected;

		/// <inheritdoc/>
		protected override AppCompatEditText CreatePlatformView()
		{
			var editor = new NativeRichEditor(Context, this)
			{
				Gravity = GravityFlags.Top | GravityFlags.Start,
				InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine | InputTypes.TextFlagCapSentences,
			};
			editor.SetSingleLine(false);
			editor.SetHorizontallyScrolling(false);
			return editor;
		}

		/// <inheritdoc/>
		protected override void ConnectHandler(AppCompatEditText platformView)
		{
			base.ConnectHandler(platformView);
			_connected = true;
			_defaultTypeface = platformView.Typeface;
			_defaultSize = platformView.TextSize;
			_defaultKeyListener = platformView.KeyListener;
			((NativeRichEditor)platformView).Owner = this;
			_watcher = new TextWatcher(this);
			platformView.AddTextChangedListener(_watcher);
		}

		/// <inheritdoc/>
		protected override void DisconnectHandler(AppCompatEditText platformView)
		{
			_connected = false;
			((NativeRichEditor)platformView).Owner = null;
			platformView.RemoveTextChangedListener(_watcher);
			_watcher?.Dispose();
			_watcher = null;
			ClearSpans(platformView.EditableText);
			_pendingDocument = false;
			_posted = false;
			_defaultTypeface = null;
			platformView.KeyListener = _defaultKeyListener;
			_defaultKeyListener = null;
			base.DisconnectHandler(platformView);
		}

		bool Composing => _connected && PlatformView?.EditableText is ISpannable text && BaseInputConnection.GetComposingSpanStart(text) >= 0;

		partial void UpdateDocument()
		{
			if (!_connected || VirtualView == null || PlatformView?.EditableText is not IEditable editable)
				return;
			if (Composing)
			{
				_pendingDocument = true;
				return;
			}
			_updating = true;
			try
			{
				string text = VirtualView.Document.Text;
				string old = editable.ToString() ?? string.Empty;
				if (text != old)
				{
					int first = 0;
					while (first < text.Length && first < old.Length && text[first] == old[first])
						first++;
					int end = 0;
					while (end < text.Length - first && end < old.Length - first && text[text.Length - end - 1] == old[old.Length - end - 1])
						end++;
					using var replacement = new Java.Lang.String(text.Substring(first, text.Length - first - end));
					editable.Replace(first, old.Length - end, replacement);
				}
				ClearSpans(editable);
				int offset = 0;
				float density = PlatformView.Resources?.DisplayMetrics?.Density ?? 1;
				float scaledDensity = PlatformView.Resources?.DisplayMetrics?.ScaledDensity ?? density;
				var document = VirtualView.Document;
				for (int p = 0; p < document.Paragraphs.Count; p++)
				{
					var paragraph = document.Paragraphs[p];
					int paragraphStart = offset;
					for (int r = 0; r < paragraph.Runs.Count; r++)
					{
						var run = paragraph.Runs[r];
						var span = new RunStyleSpan(run.Style, _defaultTypeface, _defaultSize, scaledDensity);
						_spans.Add(span);
						editable.SetSpan(span, offset, offset + run.Text.Length, SpanTypes.ExclusiveExclusive);
						offset += run.Text.Length;
					}
					if (paragraph.ListKind != RichTextListKind.None)
					{
						string marker = paragraph.ListKind == RichTextListKind.Bullet ? "\u2022" :
							document.GetListNumber(p).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
						var span = new ListMarkerSpan(marker, paragraph.ListLevel, density, PlatformView.Paint);
						_spans.Add(span);
						int paragraphEnd = offset < text.Length ? offset + 1 : offset;
						editable.SetSpan(span, paragraphStart, paragraphEnd, SpanTypes.InclusiveInclusive);
					}
					offset++;
				}
			}
			finally
			{
				_updating = false;
			}
			UpdateValue(nameof(IRichEditor.CursorPosition));
		}

		void ClearSpans(IEditable? editable)
		{
			for (int i = 0; i < _spans.Count; i++)
			{
				editable?.RemoveSpan(_spans[i]);
				_spans[i].Dispose();
			}
			_spans.Clear();
		}

		partial void UpdateSelection()
		{
			if (!_connected || _updating || Composing || VirtualView == null || PlatformView == null)
				return;
			int start = Math.Min(VirtualView.CursorPosition, PlatformView.Length());
			int end = Math.Min(start + VirtualView.SelectionLength, PlatformView.Length());
			_updating = true;
			try
			{
				PlatformView.SetSelection(start, end);
			}
			finally
			{
				_updating = false;
			}
		}

		partial void UpdateTypingStyle()
		{
			// Native edits inherit spans; the committed edit is styled by the shared model.
			if (!_updating)
				ScheduleCommit();
		}

		partial void UpdateIsReadOnly()
		{
			if (!_connected || VirtualView == null || PlatformView == null)
				return;
			PlatformView.ShowSoftInputOnFocus = !VirtualView.IsReadOnly;
			PlatformView.SetCursorVisible(!VirtualView.IsReadOnly);
			PlatformView.KeyListener = VirtualView.IsReadOnly ? null : _defaultKeyListener;
		}

		void ScheduleCommit()
		{
			if (!_connected || _updating || _posted || VirtualView == null || PlatformView == null)
				return;
			_posted = true;
			PlatformView.Post(() =>
			{
				_posted = false;
				CommitNativeText();
			});
		}

		void CommitNativeText()
		{
			if (!_connected || _updating || Composing || VirtualView == null || PlatformView == null)
				return;
			if (_pendingDocument)
			{
				_pendingDocument = false;
				UpdateValue(nameof(IRichEditor.Document));
				return;
			}
			string text = PlatformView.Text ?? string.Empty;
			int start = Math.Max(0, Math.Min(PlatformView.SelectionStart, PlatformView.SelectionEnd));
			int length = Math.Abs(PlatformView.SelectionEnd - PlatformView.SelectionStart);
			if (text != VirtualView.Document.Text)
			{
				if (VirtualView.IsReadOnly || !VirtualView.IsEnabled)
				{
					UpdateValue(nameof(IRichEditor.Document));
					return;
				}
				VirtualView.UpdateText(text, start, length);
			}
			else
				VirtualView.UpdateSelection(start, length);
		}

		sealed class TextWatcher : Java.Lang.Object, ITextWatcher
		{
			readonly RichEditorHandler _owner;
			public TextWatcher(RichEditorHandler owner) => _owner = owner;
			public void AfterTextChanged(IEditable? s) => _owner.ScheduleCommit();
			public void BeforeTextChanged(ICharSequence? s, int start, int count, int after) { }
			public void OnTextChanged(ICharSequence? s, int start, int before, int count) { }
		}

		sealed class NativeRichEditor : AppCompatEditText
		{
			public RichEditorHandler? Owner { get; set; }
			public NativeRichEditor(Context context, RichEditorHandler owner) : base(context) => Owner = owner;

			protected override void OnSelectionChanged(int selStart, int selEnd)
			{
				base.OnSelectionChanged(selStart, selEnd);
				Owner?.ScheduleCommit();
			}

			public override IInputConnection? OnCreateInputConnection(EditorInfo? outAttrs)
			{
				var connection = base.OnCreateInputConnection(outAttrs);
				return connection == null ? null : new RichInputConnection(connection, this);
			}

			public override bool OnTextContextMenuItem(int id)
			{
				var owner = Owner;
				if (owner?.VirtualView == null)
					return base.OnTextContextMenuItem(id);
				owner.CommitNativeText();
				if (id == global::Android.Resource.Id.Undo || id == global::Android.Resource.Id.Redo)
				{
					if (!owner.VirtualView.IsReadOnly && owner.VirtualView.IsEnabled)
					{
						if (id == global::Android.Resource.Id.Undo)
							owner.VirtualView.Undo();
						else
							owner.VirtualView.Redo();
					}
					return true;
				}
				if (id == global::Android.Resource.Id.Paste || id == global::Android.Resource.Id.PasteAsPlainText)
				{
					if (owner.VirtualView.IsReadOnly || !owner.VirtualView.IsEnabled)
						return true;
					return base.OnTextContextMenuItem(global::Android.Resource.Id.PasteAsPlainText);
				}
				if ((owner.VirtualView.IsReadOnly || !owner.VirtualView.IsEnabled) && id == global::Android.Resource.Id.Cut)
					return true;
				return base.OnTextContextMenuItem(id);
			}

			public override bool OnKeyShortcut(Keycode keyCode, KeyEvent? e)
			{
				if (e?.IsCtrlPressed == true && keyCode == Keycode.Z)
					return OnTextContextMenuItem(e.IsShiftPressed ? global::Android.Resource.Id.Redo : global::Android.Resource.Id.Undo);
				if (e?.IsCtrlPressed == true && keyCode == Keycode.Y)
					return OnTextContextMenuItem(global::Android.Resource.Id.Redo);
				return base.OnKeyShortcut(keyCode, e);
			}

		}

		sealed class RichInputConnection : InputConnectionWrapper
		{
			readonly NativeRichEditor _editor;
			bool CanEdit => _editor.Owner?.VirtualView is { IsReadOnly: false, IsEnabled: true };
			public RichInputConnection(IInputConnection connection, NativeRichEditor editor) : base(connection, false) => _editor = editor;
			public override bool FinishComposingText()
			{
				bool result = base.FinishComposingText();
				_editor.Owner?.ScheduleCommit();
				return result;
			}
			public override bool CommitText(ICharSequence? text, int newCursorPosition)
			{
				if (!CanEdit)
					return false;
				bool result = base.CommitText(text, newCursorPosition);
				_editor.Owner?.ScheduleCommit();
				return result;
			}
			public override bool SetComposingText(ICharSequence? text, int newCursorPosition) =>
				CanEdit && base.SetComposingText(text, newCursorPosition);
			public override bool DeleteSurroundingText(int beforeLength, int afterLength) =>
				CanEdit && base.DeleteSurroundingText(beforeLength, afterLength);
			public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength) =>
				CanEdit && base.DeleteSurroundingTextInCodePoints(beforeLength, afterLength);
		}

		sealed class RunStyleSpan : MetricAffectingSpan
		{
			readonly Typeface? _typeface;
			readonly float _size;
			readonly bool _underline;
			public RunStyleSpan(RichTextStyle style, Typeface? defaultTypeface, float defaultSize, float density)
			{
				var traits = (style.Bold ? TypefaceStyle.Bold : TypefaceStyle.Normal) | (style.Italic ? TypefaceStyle.Italic : TypefaceStyle.Normal);
				_typeface = style.FontFamily == null ? Typeface.Create(defaultTypeface, traits) : Typeface.Create(style.FontFamily, traits);
				_size = style.FontSize.HasValue ? (float)style.FontSize.Value * density : defaultSize;
				_underline = style.Underline;
			}
			public override void UpdateDrawState(TextPaint? tp)
			{
				UpdateMeasureState(tp);
				if (tp != null)
					tp.UnderlineText = _underline;
			}
			public override void UpdateMeasureState(TextPaint? p)
			{
				if (p == null)
					return;
				p.SetTypeface(_typeface);
				p.TextSize = _size;
			}
			protected override void Dispose(bool disposing)
			{
				if (disposing)
					_typeface?.Dispose();
				base.Dispose(disposing);
			}
		}

		sealed class ListMarkerSpan : Java.Lang.Object, ILeadingMarginSpan
		{
			readonly string _marker;
			readonly int _indent;
			readonly int _margin;
			public ListMarkerSpan(string marker, int level, float density, Paint? paint)
			{
				_marker = marker;
				_indent = (int)((level + 1) * 24 * density);
				_margin = Math.Max(_indent, (int)((paint?.MeasureText(marker) ?? 0) + 12 * density));
			}
			public int GetLeadingMargin(bool first) => _margin;
			public void DrawLeadingMargin(Canvas? c, Paint? p, int x, int dir, int top, int baseline, int bottom,
				ICharSequence? text, int start, int end, bool first, ALayout? layout)
			{
				if (!first || c == null || p == null || text is not ISpanned spanned || spanned.GetSpanStart(this) != start)
					return;
				var alignment = p.TextAlign;
				p.TextAlign = dir > 0 ? Paint.Align.Left : Paint.Align.Right;
				c.DrawText(_marker, x + dir * (_indent - _margin), baseline, p);
				p.TextAlign = alignment;
			}
		}
	}
}
