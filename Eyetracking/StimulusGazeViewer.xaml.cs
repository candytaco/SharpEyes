using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Eyetracking
{
	/// <summary>
	/// Interaction logic for StimulusGazeViewer.xaml
	/// </summary>
	public partial class StimulusGazeViewer : Window
	{
		private DispatcherTimer timer;
		private bool isPlaying = false;
		public bool IsPlaying
		{
			get { return isPlaying; }
			set
			{
				isPlaying = value;
				if (value)
				{
					PlayPauseButton.Content = "Pause";
					VideoMediaElement.Play();
					timer.Start();
				}
				else
				{
					PlayPauseButton.Content = "Play";
					VideoMediaElement.Pause();
					timer.Stop();
				}
			}
		}

		/// <summary>
		/// Start time pf data in the stimulus video, in milliseconds
		/// </summary>
		private Nullable<int> dataStartTime = null;

		/// <summary>
		/// Timings of keyframes, i.e. frames on which the gaze location has been manually edited.
		/// Values are milliseconds from start.
		/// </summary>
		private List<int> keyFrames;

		public StimulusGazeViewer()
		{
			timer = new DispatcherTimer();
			InitializeComponent();
		}

		private void OpenVideoMenuItem_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				DefaultExt = ".mkv",
				Filter = "Stimulus videos (.avi, .mp4, .mkv)|*.avi;*.mp4;*.mkv",
				Title = "Open stimulus video..."
			};
			if (openFileDialog.ShowDialog() == true)
			{
				LoadFile(openFileDialog.FileName);
			}
		}

		private void LoadFile(string videoFileName)
		{
			VideoMediaElement.Source = new Uri(videoFileName);
			VideoMediaElement.LoadedBehavior = MediaState.Manual;
			VideoMediaElement.Play();
			VideoMediaElement.Position = TimeSpan.Zero;
			VideoMediaElement.Pause();
			isPlaying = false;

			loadGazeButton.IsEnabled = true;
			moveGazeButton.IsEnabled = true;
			loadGazeMenuItem.IsEnabled = true;
			saveGazeMenuItem.IsEnabled = true;
			saveGazeAsMenuItem.IsEnabled = true;
			PlayPauseButton.IsEnabled = true;

			keyFrames = new List<int>();

			timer.Interval = new TimeSpan(10000000 / EyetrackingFPSPicker.Value.Value);
		}

		private void FindDataStart()
		{

		}

		private void LoadGazeMenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void SaveGazeMenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void MoveGazeButtom_Click(object sender, RoutedEventArgs e)
		{

		}

		private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{

		}

		private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{

		}

		private void Canvas_MouseMove(object sender, MouseEventArgs e)
		{

		}

		private void VideoMediaElement_MediaEnded(object sender, RoutedEventArgs e)
		{

		}

		private void VideoSlider_MouseDown(object sender, MouseButtonEventArgs e)
		{

		}

		private void VideoSlider_MouseUp(object sender, MouseButtonEventArgs e)
		{

		}

		private void VideoSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{

		}

		private void VideoSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
		{

		}

		private void SetCurrentAsDataStartButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void AutoFindDataStartButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
		{
			if (VideoMediaElement.Source == null)
			{
				return;
			}

			IsPlaying = !IsPlaying;
		}

		private void PreviousKeyFrameButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void NextKeyFrameButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void SaveGazeAsMenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void OpenEyetrackingDataCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void RightCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void LeftCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void PlayPauseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			PlayPauseButton_Click(null, null);
		}

		private void MovePupilCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void OpenVideoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void NewFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void SaveFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void SaveFileAsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void EyetrackingFPSPicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			timer.Interval = new TimeSpan(10000000 / EyetrackingFPSPicker.Value.Value);
		}

		private void GazeMarkerDiameterPicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
		}
	}
}
