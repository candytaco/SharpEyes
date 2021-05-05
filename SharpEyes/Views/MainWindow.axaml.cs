using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SharpEyes.Views
{
	public class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
			ExtendClientAreaTitleBarHeightHint = -1;

			TransparencyLevelHint = WindowTransparencyLevel.AcrylicBlur;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void OpenFileMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void LoadSavedDataMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void LoadSavedEyetrackingMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void LoadSavedTemplatesMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void SaveTimestampsMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void SaveEyetrackingMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void SaveTemplatesMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void SaveAllMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void StimulusViewMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void HelpMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void ReportBugmenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void AboutMenuItem_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}
	}
}
