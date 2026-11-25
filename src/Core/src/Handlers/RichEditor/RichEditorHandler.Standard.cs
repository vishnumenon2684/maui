using System;

namespace Microsoft.Maui.Handlers
{
	public partial class RichEditorHandler
	{
		/// <inheritdoc/>
		protected override object CreatePlatformView() => throw new PlatformNotSupportedException("RichEditor requires a native MAUI platform.");
	}
}
