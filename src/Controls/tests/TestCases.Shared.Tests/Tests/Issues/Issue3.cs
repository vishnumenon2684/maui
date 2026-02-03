using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Maui.Controls.Sample.Issues
{
	[Issue(IssueTracker.Github, 3, "Shell theme incorrect when pushing a modal page at startup",
		PlatformAffected.Android)]
	public partial class Issue3 : Shell
	{
		public Issue3()
		{
			Title = "Shell Modal Theme Test";
			
			// Set custom Shell appearance
			Shell.SetBackgroundColor(this, Color.FromArgb("#2c3e50"));
			Shell.SetForegroundColor(this, Colors.White);
			Shell.SetTitleColor(this, Colors.White);

			var mainPage = new ContentPage
			{
				Title = "Main Page",
				Content = new StackLayout
				{
					Children =
					{
						new Label
						{
							Text = "This is the main page. A modal should be pushed at startup.",
							HorizontalOptions = LayoutOptions.Center,
							VerticalOptions = LayoutOptions.Center
						}
					}
				}
			};

			Items.Add(new TabBar
			{
				Items = { new ShellContent { Content = mainPage } }
			});
		}

		protected override async void OnAppearing()
		{
			base.OnAppearing();
			
			// This reproduces the issue: pushing modal in OnAppearing
			var modalPage = new ContentPage
			{
				Title = "Modal Page",
				BackgroundColor = Colors.LightBlue,
				Content = new StackLayout
				{
					Padding = 20,
					Children =
					{
						new Label
						{
							Text = "This is a modal page pushed in Shell.OnAppearing().",
							HorizontalOptions = LayoutOptions.Center,
							VerticalOptions = LayoutOptions.Start,
							Margin = new Thickness(0, 50, 0, 20)
						},
						new Label
						{
							Text = "Before the fix: The Shell theme colors (#1B3147) would appear instead of the expected Shell appearance.",
							HorizontalOptions = LayoutOptions.Center,
							VerticalOptions = LayoutOptions.Center,
							Margin = new Thickness(0, 0, 0, 20)
						},
						new Label
						{
							Text = "After the fix: The Shell appearance should be properly applied.",
							HorizontalOptions = LayoutOptions.Center,
							VerticalOptions = LayoutOptions.Center,
							Margin = new Thickness(0, 0, 0, 20)
						},
						new Button
						{
							Text = "Close Modal",
							HorizontalOptions = LayoutOptions.Center,
							Command = new Command(async () => await Navigation.PopModalAsync())
						}
					}
				}
			};

			await Navigation.PushModalAsync(modalPage);
		}
	}
}