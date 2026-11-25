#if IOS || MACCATALYST
using PlatformView = UIKit.UITextView;
#elif ANDROID
using PlatformView = AndroidX.AppCompat.Widget.AppCompatEditText;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.Controls.RichEditBox;
#else
using PlatformView = System.Object;
#endif

namespace Microsoft.Maui.Handlers
{
	/// <summary>Maps a rich editor to a dedicated native editing view.</summary>
	public partial class RichEditorHandler : ViewHandler<IRichEditor, PlatformView>
	{
		/// <summary>Gets the rich editor property mapper.</summary>
		public static IPropertyMapper<IRichEditor, RichEditorHandler> Mapper = new PropertyMapper<IRichEditor, RichEditorHandler>(ViewMapper)
		{
			[nameof(IRichEditor.Document)] = MapDocument,
			[nameof(IRichEditor.CursorPosition)] = MapSelection,
			[nameof(IRichEditor.SelectionLength)] = MapSelection,
			[nameof(IRichEditor.TypingStyle)] = MapTypingStyle,
			[nameof(IRichEditor.IsReadOnly)] = MapIsReadOnly
		};

		/// <summary>Creates a rich editor handler.</summary>
		public RichEditorHandler() : base(Mapper) { }

		/// <summary>Creates a rich editor handler with an extensible property and command mapper.</summary>
		public RichEditorHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null) : base(mapper ?? Mapper, commandMapper) { }

		/// <summary>Maps the document to native text and formatting.</summary>
		public static void MapDocument(RichEditorHandler handler, IRichEditor editor) => handler.UpdateDocument();
		/// <summary>Maps the logical selection to the native selection.</summary>
		public static void MapSelection(RichEditorHandler handler, IRichEditor editor) => handler.UpdateSelection();
		/// <summary>Maps the style for newly typed text.</summary>
		public static void MapTypingStyle(RichEditorHandler handler, IRichEditor editor) => handler.UpdateTypingStyle();
		/// <summary>Maps whether native text can be edited.</summary>
		public static void MapIsReadOnly(RichEditorHandler handler, IRichEditor editor) => handler.UpdateIsReadOnly();

		partial void UpdateDocument();
		partial void UpdateSelection();
		partial void UpdateTypingStyle();
		partial void UpdateIsReadOnly();
	}
}
