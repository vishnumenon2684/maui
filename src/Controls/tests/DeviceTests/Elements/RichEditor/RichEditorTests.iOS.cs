using System.Threading.Tasks;
using Foundation;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using ObjCRuntime;
using Xunit;
using static Microsoft.Maui.DeviceTests.AssertHelpers;

namespace Microsoft.Maui.DeviceTests
{
	public partial class RichEditorTests
	{
		[Fact]
		public async Task NativeUndoAvailabilityFollowsSharedHistory()
		{
			SetupBuilder();
			var editor = new RichEditor { Document = RichTextDocument.FromText("a") };
			await AttachAndRun<RichEditorHandler>(editor, handler =>
			{
				var history = handler.PlatformView.UndoManager!;
				Assert.False(history.CanUndo);
				Assert.False(history.CanRedo);
				editor.Select(1, 0);
				editor.InsertText("b");
				Assert.True(history.CanUndo);
				Assert.False(history.CanRedo);
				history.Undo();
				Assert.Equal("a", editor.Document.Text);
				Assert.False(history.CanUndo);
				Assert.True(history.CanRedo);
				history.Redo();
				Assert.Equal("ab", editor.Document.Text);
			});
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task NativeCommandsRejectReadOnlyAndDisabledEditing(bool readOnly)
		{
			SetupBuilder();
			var editor = new RichEditor { Document = RichTextDocument.FromText("a") };
			editor.Select(1, 0);
			editor.InsertText("b");
			await AttachAndRun<RichEditorHandler>(editor, handler =>
			{
				if (readOnly)
					editor.IsReadOnly = true;
				else
					editor.IsEnabled = false;
				var native = handler.PlatformView;
				var undo = new Selector("undo:");
				Assert.False(native.CanPerform(undo, native));
				Assert.False(native.UndoManager!.CanUndo);
				native.InsertText("ignored");
				native.SetMarkedText("ignored", new NSRange(0, 0));
				Assert.Equal("ab", native.Text);
				Assert.Equal("ab", editor.Document.Text);
				Assert.Null(native.MarkedTextRange);
			});
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task CompositionDefersCommitAndHonorsExternalReplacement(bool externalReplacement)
		{
			SetupBuilder();
			var editor = new RichEditor { Document = RichTextDocument.FromText("a") };
			editor.Select(1, 0);
			editor.ApplyBold();
			await AttachAndRun<RichEditorHandler>(editor, async handler =>
			{
				var native = handler.PlatformView;
				Assert.True(native.BecomeFirstResponder());
				native.SetMarkedText("b", new NSRange(1, 0));
				native.Delegate?.Changed(native);
				Assert.NotNull(native.MarkedTextRange);
				Assert.Equal("a", editor.Document.Text);
				if (externalReplacement)
					editor.Document = RichTextDocument.FromText("replacement");
				native.UnmarkText();
				await AssertEventually(() => editor.Document.Text == (externalReplacement ? "replacement" : "ab"));
				Assert.Equal(editor.Document.Text, native.Text);
				if (!externalReplacement)
				{
					Assert.True(editor.Document.Paragraphs[0].Runs[1].Style.Bold);
					editor.Undo();
					Assert.Equal("a", native.Text);
				}
			});
		}
	}
}
