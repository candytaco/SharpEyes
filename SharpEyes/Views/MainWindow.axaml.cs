using System;
using System.ComponentModel;
using Avalonia.Controls;
using DynamicData.Kernel;
using MessageBox.Avalonia.BaseWindows.Base;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using Sentry;

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

		public void GlobalExceptionHandler(Exception e)
		{
			IMsBoxWindow<MessageBox.Avalonia.Enums.ButtonResult> messageBox =
				MessageBoxManager.GetMessageBoxStandardWindow("Internal error", 
					"SharpEyes has encountered an internal error and is exiting.\nAn error report will be sent", 
					ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Error);
			SentrySdk.CaptureException(e);
			messageBox.ShowDialog(this).Wait(2000);
		}

		private void Window_OnClosing(object? sender, CancelEventArgs e)
		{
			PupilFindingUserControl.OnClosing();
		}
	}
}
