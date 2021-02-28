﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NumSharp;
using Num = NumSharp.np;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Window = System.Windows.Window;
using Point = System.Windows.Point;
using System.IO;

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
		/// Start time of data in the stimulus video, in milliseconds
		/// </summary>
		private double? dataStartTime = null;

		/// <summary>
		/// Timings of keyframes, i.e. frames on which the gaze location has been manually edited.
		/// Values are milliseconds from start.
		/// </summary>
		private List<int> keyFrames;

		private NDArray gazeLocations = null;
		private bool isGazeLoaded = false;	// extra because checking gazeLocations != null throws a NullReferenceException....

		private double _gazeX = 0;
		private double _gazeY = 0;

		public double gazeX
		{
			get => _gazeX;
			set
			{
				_gazeX = value;
				Canvas.SetLeft(GazeEllipse, value - GazeMarkerDiameterPicker.Value.Value / 2); // replaced by binding
				XPositionText.Text = string.Format("X: {0:####.#}", value);
			}
		}

		public double gazeY
		{
			get => _gazeY;
			set
			{
				_gazeY = value;
				Canvas.SetTop(GazeEllipse, value - GazeMarkerDiameterPicker.Value.Value / 2); // replaced by binding
				YPositionText.Text = string.Format("Y: {0:####.#}", value);
			}
		}

		/// <summary>
		/// Duration of a frame in the stimulus video in milliseconds
		/// </summary>
		private double stimulusFrameDuration = 0;

		/// <summary>
		/// Duration of a frame in the eyetracking data
		/// </summary>
		private double eyetrackingFrameDuration = 0;

		/// <summary>
		/// Used to read basic video info
		/// </summary>
		private VideoCapture videoSource;

		/// <summary>
		/// For tracking where the mouse is moving the gaze location
		/// </summary>
		Point mouseMoveStartPoint;

		/// <summary>
		/// Are we in the middle of moving the gaze location?
		/// </summary>
		bool isMovingGaze = false;

		string gazeFileName = null;

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
				LoadVideoFile(openFileDialog.FileName);
			}
		}

		private void LoadVideoFile(string videoFileName)
		{
			VideoMediaElement.MediaOpened += VideoOpened;
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
			NextFrameButton.IsEnabled = true;
			PreviousFrameButton.IsEnabled = true;

			keyFrames = new List<int>();

			timer.Interval = new TimeSpan(10000000 / EyetrackingFPSPicker.Value.Value);
			stimulusFrameDuration = 1000.0 / EyetrackingFPSPicker.Value.Value;
			timer.Tick += UpdateDisplays;

			videoSource = new VideoCapture(videoFileName);

			// update status bar
			VideoNameStatus.Text = videoFileName;
			VideoDurationStatus.Text = MainWindow.FramesToDurationString(videoSource.FrameCount, (int)videoSource.Fps);
			FPSStatus.Text = string.Format("{0:##} fps", videoSource.Fps);
			VideoSizeStatus.Text = string.Format("{0}×{1}", videoSource.FrameWidth, videoSource.FrameHeight);
			stimulusFrameDuration = 1000.0 / videoSource.Fps;
		}

		private void VideoOpened(object sender, RoutedEventArgs e)
		{
			VideoSlider.Maximum = VideoMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
			UpdateDisplays(null, null);
		}

		private void FindDataStart()
		{

		}

		private void UpdateDisplays(object sender, EventArgs e)
		{
			VideoSlider.Value = VideoMediaElement.Position.TotalMilliseconds;

			VideoTimeLabel.Content = string.Format("{0:00}:{1:00}:{2:00};{3:#00}", VideoMediaElement.Position.Hours,
																				   VideoMediaElement.Position.Minutes,
																				   VideoMediaElement.Position.Seconds,
																				   (int)(VideoMediaElement.Position.Milliseconds / stimulusFrameDuration));

			if (isGazeLoaded)
				if (VideoMediaElement.Position.TotalMilliseconds >= dataStartTime)
				{
					int frameIndex = (int) ((VideoMediaElement.Position.TotalMilliseconds - dataStartTime) / eyetrackingFrameDuration);
					if (frameIndex < gazeLocations.shape[0])
					{
						gazeX = gazeLocations[frameIndex, 0];
						gazeY = gazeLocations[frameIndex, 1];
					}
				}
		}

		private void LoadGazeMenuItem_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy",
				Title = "Load gaze locations..."
			};
			if (openFileDialog.ShowDialog() == true)
			{
				gazeLocations = Num.load(openFileDialog.FileName);
				isGazeLoaded = true;
				SetCurrentAsDataStartButton.IsEnabled = true;
				AutoFindDataStartButton.IsEnabled = true;
				EyetrackingFPSPicker_ValueChanged(null, null);
				gazeFileName = openFileDialog.FileName;
			}
		}

		private void SaveGazeMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (gazeFileName == null)
				SaveGazeAsMenuItem_Click(sender, e);
			else
				Num.save(gazeFileName, gazeLocations);
		}

		private void MoveGazeButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (moveGazeButton.IsChecked.Value)
			{
				mouseMoveStartPoint = e.GetPosition(canvas);
				gazeX = mouseMoveStartPoint.X;
				gazeY = mouseMoveStartPoint.Y;
				isMovingGaze = true;
			}
		}

		private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (isMovingGaze)
			{
				isMovingGaze = false;
				UpdateGazePositionData();
			}
		}

		private void Canvas_MouseMove(object sender, MouseEventArgs e)
		{
			if (isMovingGaze)
			{
				Point mouse = e.GetPosition(canvas);
				mouse.X = mouse.X > 0 ? mouse.X : 0;
				mouse.X = mouse.X < canvas.Width ? mouse.X : canvas.Width;
				mouse.Y = mouse.Y > 0 ? mouse.Y : 0;
				mouse.Y = mouse.Y < canvas.Height ? mouse.Y : canvas.Height;

				gazeX = mouse.X;
				gazeY = mouse.Y;
			}
		}


		private void UpdateGazePositionData()
		{

		}


		private void VideoMediaElement_MediaEnded(object sender, RoutedEventArgs e)
		{

		}

		private void VideoSlider_MouseDown(object sender, MouseButtonEventArgs e)
		{
			IsPlaying = false;
		}

		private void VideoSlider_MouseUp(object sender, MouseButtonEventArgs e)
		{
			VideoMediaElement.Position = new TimeSpan((long)(10000 * VideoSlider.Value));
			UpdateDisplays(null, null);
		}

		private void VideoSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{ 
			VideoMediaElement.Position = new TimeSpan((long)(10000 * VideoSlider.Value));
			UpdateDisplays(null, null);
		}

		private void VideoSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
		{
			IsPlaying = false;
		}

		private void SetCurrentAsDataStartButton_Click(object sender, RoutedEventArgs e)
		{
			dataStartTime = VideoMediaElement.Position.TotalMilliseconds;
			UpdateDisplays(null, null);
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
			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy",
				Title = "Save timestamps...",
				FileName = Path.GetFileNameWithoutExtension(gazeFileName ?? "gaze locations")
			};
			if (saveFileDialog.ShowDialog() == true)
			{
				Num.save(gazeFileName, gazeLocations);
			}
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
			eyetrackingFrameDuration = 1000.0 / EyetrackingFPSPicker.Value.Value;
		}

		private void GazeMarkerDiameterPicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
		}

		private void PreviousFrameButton_Click(object sender, RoutedEventArgs e)
		{
			SetVideoPosition(VideoSlider.Value - stimulusFrameDuration);
		}

		private void NextFrameButton_Click(object sender, RoutedEventArgs e)
		{
			SetVideoPosition(VideoSlider.Value + stimulusFrameDuration);
		}

		private void SetVideoPosition(double milliseconds)
		{
			if ((milliseconds < 0) || (milliseconds > VideoMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds))
				return;

			VideoMediaElement.Position = new TimeSpan((long)(10000 * milliseconds));
			UpdateDisplays(null, null);
		}

		private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (VideoMediaElement.IsLoaded)
			{
				IsPlaying = false;
				if (e.Delta < 0)
					NextFrameButton_Click(null, null);
				else
					PreviousFrameButton_Click(null, null);
			}
		}
	}
}
