namespace Microsoft.Maui.Controls.Xaml.UnitTests.GlobalUsingTestTypes;

public class CustomLabel : Label
{
	public static readonly BindableProperty SubtitleProperty =
		BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(CustomLabel), default(string));

	public string Subtitle
	{
		get => (string)GetValue(SubtitleProperty);
		set => SetValue(SubtitleProperty, value);
	}
}
