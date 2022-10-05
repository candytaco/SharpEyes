using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CrashReporter
{
	public partial class MainWindow : Window
	{
		public string? SentryEventID { get; set; } = null;
		public MainWindow()
		{
			InitializeComponent();
		}

		public MainWindow(string sentryEventID)
		{
			InitializeComponent();
			SentryEventID = sentryEventID;
		}

		public string IDDisplayText => SentryEventID == null
			? "This event has no ID"
			: String.Format("The event ID for this crash is {0}", SentryEventID);

		private void SubmitReportButton_OnClick(object? sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start(new ProcessStartInfo(
			"https://github.com/candytaco/SharpEyes/issues/new/choose")
			{
				UseShellExecute = true
			});
			Close();
		}

		private void RestartButton_OnClick(object? sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start(new ProcessStartInfo(
				"SharpEyes")
			{
				UseShellExecute = true
			});
			Close();
		}

		private void ExitButton_OnClick(object? sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
