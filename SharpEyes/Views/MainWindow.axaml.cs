using System.ComponentModel;
using Avalonia.Controls;

namespace SharpEyes.Views
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			//ExtendClientAreaToDecorationsHint = true;
			ExtendClientAreaTitleBarHeightHint = -1;

			TransparencyLevelHint = WindowTransparencyLevel.AcrylicBlur;
		}

		private void Window_OnClosing(object? sender, CancelEventArgs e)
		{
			PupilFindingUserControl.OnClosing();
		}
	}
}
