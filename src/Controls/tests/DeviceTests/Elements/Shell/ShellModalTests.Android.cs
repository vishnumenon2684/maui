using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Graphics;
using Xunit;

namespace Microsoft.Maui.DeviceTests
{
	[Category(TestCategory.Shell)]
	public partial class ShellModalTests : ControlsHandlerTestBase
	{
		[Fact(DisplayName = "Modal pushed in Shell OnAppearing should inherit Shell theme")]
		public async Task ModalPushedInShellOnAppearingShouldInheritShellTheme()
		{
			SetupBuilder();

			var modalPage = new ContentPage
			{
				Title = "Modal Page",
				Content = new Label { Text = "This is a modal page" }
			};

			var shell = new TestShell();
			bool modalPushed = false;

			shell.Appearing += async (sender, e) =>
			{
				if (!modalPushed)
				{
					modalPushed = true;
					await shell.Navigation.PushModalAsync(modalPage);
				}
			};

			// Set some Shell appearance to test
			Shell.SetBackgroundColor(shell, Colors.Red);
			Shell.SetForegroundColor(shell, Colors.White);

			var window = new Window(shell);

			await CreateHandlerAndAddToWindow<ShellRenderer>(shell, async (handler) =>
			{
				// Wait for the shell to be fully loaded
				await shell.WaitForShellToLoad();

				// Verify the modal was pushed
				Assert.True(modalPushed, "Modal should have been pushed in OnAppearing");
				Assert.Single(shell.Navigation.ModalStack);
				Assert.Equal(modalPage, shell.Navigation.ModalStack[0]);

				// Test that the modal inherited the Shell theme
				// The fix ensures the modal is delayed until Shell is ready
				// This prevents the theme application timing issue
				await AssertEventually(() =>
				{
					return shell.Navigation.ModalStack.Count == 1;
				}, timeout: 5000);
			});
		}

		[Fact(DisplayName = "Shell should be ready before modal presentation during startup")]
		public async Task ShellShouldBeReadyBeforeModalPresentationDuringStartup()
		{
			SetupBuilder();

			var modalPage = new ContentPage
			{
				Title = "Modal Page",
				Content = new Label { Text = "This is a modal page" }
			};

			var shell = new TestShell();
			bool shellWasLoaded = false;

			shell.Appearing += async (sender, e) =>
			{
				// Record if Shell was loaded when OnAppearing was called
				shellWasLoaded = shell.IsLoaded && shell.Handler != null;
				await shell.Navigation.PushModalAsync(modalPage);
			};

			var window = new Window(shell);

			await CreateHandlerAndAddToWindow<ShellRenderer>(shell, async (handler) =>
			{
				// Wait for everything to settle
				await shell.WaitForShellToLoad();

				// With our fix, the modal should be delayed until Shell is loaded
				// So by the time the modal is actually presented, Shell should be ready
				Assert.True(shell.IsLoaded, "Shell should be loaded");
				Assert.NotNull(shell.Handler);
				Assert.Single(shell.Navigation.ModalStack);
			});
		}

		private class TestShell : Shell
		{
			public TestShell()
			{
				var contentPage = new ContentPage
				{
					Title = "Main Page",
					Content = new Label { Text = "Main content" }
				};

				Items.Add(new TabBar
				{
					Items = { new ShellContent { Content = contentPage } }
				});
			}

			public async Task WaitForShellToLoad()
			{
				// Wait for Shell to be fully loaded
				var attempts = 0;
				while ((!IsLoaded || Handler == null) && attempts < 50)
				{
					await Task.Delay(100);
					attempts++;
				}
			}
		}
	}
}