using System;
using System.Reflection;
using System.Windows;

namespace Eyetracking
{
	public partial class MainWindow
	{
		public static void ShowAboutDialogue()
		{
			Version version = Assembly.GetEntryAssembly().GetName().Version;
			Assembly assembly = Assembly.GetExecutingAssembly();
			string build = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
			MessageBox.Show(
				String.Format(
					"SharpEyes\nVersion {0} {1} \nIcon from Icons8\nLibraries: Numsharp Lite & OpenCVSharp\nt.zhang\nThis is a work in progress and a lot of things don't work.",
					version, build),
				"About SharpEyes", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		public static string SecondsToDurationString(double seconds)
		{
			int hours = (int) (seconds / 3600);
			seconds -= hours * 3600;
			int minutes = (int) (seconds / 60);
			seconds -= minutes;
			return String.Format("{0:00}:{1:00}:{2:00.000}", hours, minutes, seconds);
		}

		public static string FramesToDurationString(int frameCount, int fps)
		{
			int seconds = frameCount / fps;
			int hours = seconds / 3600;
			seconds -= hours * 3600;
			int minutes = seconds / 60;
			seconds -= minutes * 60;
			int frames = frameCount % fps;
			return String.Format("{0:00}:{1:00}:{2:00};{3:00}", hours, minutes, seconds, frames);
		}
	}
}