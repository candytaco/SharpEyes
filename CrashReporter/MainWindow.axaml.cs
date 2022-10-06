using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace CrashReporter
{
	public partial class MainWindow : Window
	{
		public string? SentryEventID => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
			? desktop.Args[0]
			: null;
		private bool isWindows = true;
		public MainWindow()
		{
			InitializeComponent();
			isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
		}

		public string IDDisplayText => SentryEventID == null
			? "This event has no ID"
			: String.Format("The event ID for this crash is {0}", SentryEventID);

		private void SubmitReportButton_OnClick(object? sender, RoutedEventArgs e)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo()
			{
				FileName = isWindows
					? "https://github.com/candytaco/SharpEyes/issues/new/choose"
					: "xdg-open",
				UseShellExecute = true
			};
			if (!isWindows)
				startInfo.Arguments = "https://github.com/candytaco/SharpEyes/issues/new/choose";
			try
			{
				System.Diagnostics.Process.Start(startInfo);
			}
			catch
			{
			}

			Close();
		}

		private void RestartButton_OnClick(object? sender, RoutedEventArgs e)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo()
			{
				FileName = isWindows
					? "SharpEyes.exe"
					: "SharpEyes",
				UseShellExecute = true
			};
			try
			{
				System.Diagnostics.Process.Start(startInfo);
			}
			catch
			{
			}

			Close();
		}

		private void ExitButton_OnClick(object? sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
