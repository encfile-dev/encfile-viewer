using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ImageFanReloaded.Controls.Security;

public partial class PasswordEntryWindow : Window
{
	public string? Password { get; private set; }

	public PasswordEntryWindow()
	{
		InitializeComponent();
	}

	private void OnOkClick(object? sender, RoutedEventArgs e)
	{
		Password = _passwordTextBox.Text;
		Close(true);
	}

	private void OnCancelClick(object? sender, RoutedEventArgs e)
	{
		Close(false);
	}

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter)
		{
			OnOkClick(sender, e);
		}
	}
}
