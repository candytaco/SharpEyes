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
	}
}
