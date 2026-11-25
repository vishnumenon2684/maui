using System;
using System.ComponentModel;
using Microsoft.Maui;

namespace Maui.Controls.Sample.Pages
{
	public partial class RichEditorPage
	{
		public RichEditorPage()
		{
			InitializeComponent();
			MarkdownText.Text = "Select some **bold** or *italic* text.\n\n- First item\n- Second item";
			RichText.LoadMarkdown(MarkdownText.Text);
			RichText.PropertyChanged += OnEditorPropertyChanged;
			UpdateSelection();
		}

		void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(RichText.CursorPosition) || e.PropertyName == nameof(RichText.SelectionLength))
				UpdateSelection();
		}

		void UpdateSelection() => Selection.Text = $"Selection: {RichText.CursorPosition}, length: {RichText.SelectionLength}";
		void OnBold(object? sender, EventArgs e) => RichText.ApplyBold();
		void OnItalic(object? sender, EventArgs e) => RichText.ApplyItalic();
		void OnUnderline(object? sender, EventArgs e) => RichText.ApplyUnderline();
		void OnFont(object? sender, EventArgs e) => RichText.ApplyFont(null, 24);
		void OnDefaultFont(object? sender, EventArgs e) => RichText.ApplyFont(null, null);
		void OnBullets(object? sender, EventArgs e) => RichText.ApplyList(RichTextListKind.Bullet);
		void OnNumbers(object? sender, EventArgs e) => RichText.ApplyList(RichTextListKind.Numbered);
		void OnNoList(object? sender, EventArgs e) => RichText.ApplyList(RichTextListKind.None);
		void OnUndo(object? sender, EventArgs e) => RichText.Undo();
		void OnRedo(object? sender, EventArgs e) => RichText.Redo();

		void OnImport(object? sender, EventArgs e)
		{
			try
			{
				RichText.LoadMarkdown(MarkdownText.Text ?? string.Empty);
				ErrorText.Text = string.Empty;
			}
			catch (FormatException error)
			{
				ErrorText.Text = error.Message;
			}
		}

		void OnExport(object? sender, EventArgs e)
		{
			try
			{
				MarkdownText.Text = RichText.GetMarkdown();
				ErrorText.Text = string.Empty;
			}
			catch (FormatException error)
			{
				ErrorText.Text = error.Message;
			}
		}
	}
}
