﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Eyetracking
{
	public enum EditingState
	{
		None,
		DrawingWindow,
		MovingPupil
	}

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : System.Windows.Window
	{
		public static string SecondsToDurationString(double seconds)
		{
			int hours = (int)(seconds / 3600);
			seconds -= hours * 3600;
			int minutes = (int)(seconds / 60);
			seconds -= minutes;
			return String.Format("{0:00}:{1:00}:{2:00.000}", hours, minutes, seconds);
		}

		public static string FramesToDurationString(int frameCount, int fps)
		{
			int seconds = frameCount / fps;
			int hours = (int)(seconds / 3600);
			seconds -= hours * 3600;
			int minutes = (int)(seconds / 60);
			seconds -= minutes;
			int frames = frameCount % fps;
			return String.Format("{0:00}:{1:00}:{2:00};{3:00}", hours, minutes, seconds, frames);
		}

		// video play related stuff
		private PupilFinder pupilFinder = null;

		private bool isPlaying = false;
		private DispatcherTimer timer;
		public double videoScaleFactor { get; private set; }    // scaling fractor from video size to display size


		// eyetracking setup related stuff
		private bool isEditingStarted = false;	// generic flag for indicating whether the window/pupil edit has begun with a mouseclick
		private System.Windows.Point mouseMoveStartPoint;
		private bool isMovingSearchWindow = false;
		private bool isMovingPupilEllipse = false;
		private bool isWindowManuallySet = false;

		// data related stuff
		private EditingState editingState = EditingState.None;

		// Pupil X and Y set the _center_ of the ellipse
		// The outward-facing values, both returned and in the text lables,
		// Reflect the position in video frame space
		// PupilEllipse's values reflect values in screen space
		private double PupilX
		{
			get { return Canvas.GetLeft(PupilEllipse) / videoScaleFactor + PupilRadius; }
			set
			{
				Canvas.SetLeft(PupilEllipse, value * videoScaleFactor - PupilEllipse.Width / 2);
				XPositionText.Text = string.Format("X: {0:####.#}", value);
			}
		}
		private double PupilY
		{
			get { return Canvas.GetTop(PupilEllipse) / videoScaleFactor + PupilRadius; }
			set
			{
				Canvas.SetTop(PupilEllipse, value * videoScaleFactor - PupilEllipse.Height / 2);
				YPositionText.Text = string.Format("Y: {0:####.#}", value);
			}
		}
		private double PupilRadius
		{
			get { return PupilEllipse.Height / 2 / videoScaleFactor; }
			set
			{
				if (value < pupilFinder.minRadius) // pupil hasn't been found yet so we just break
					return;
				double X = PupilX;
				double Y = PupilY;
				PupilEllipse.Width = PupilEllipse.Height = value * 2 * videoScaleFactor;
				PupilX = X;
				PupilY = Y;
				RadiusText.Text = string.Format("Radius: {0:####.#}", value);
			}
		}

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

		public int MillisecondsPerFrame { get; private set; } = 0;
		private TimeSpan timePerFrame;

		public bool HasFile
		{
			get
			{
				return !(pupilFinder.videoFileName == null);
			}
		}

		public MainWindow()
		{
			InitializeComponent();

			Canvas.SetLeft(SearchWindowRectangle, 0);
			Canvas.SetTop(SearchWindowRectangle, 0);
		}

		private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				DefaultExt = ".avi",
				Filter = "Eyetracking AVIs (.avi)|*.avi",
				Title = "Open eyetracking video..."
			};
			if (openFileDialog.ShowDialog() == true)
			{
				LoadFile(openFileDialog.FileName);
			}
		}

		private void SaveFileMenuItem_Click(object sender, RoutedEventArgs e)
		{
			SaveTimestamps();
		}

		public void SetStatus(string status = null)
		{
			StatusText.Text = status ?? "Idle";
		}

		private void LoadFile(string videoFileName)
		{
			SetStatus("Loading");
			pupilFinder = TemplatePupilFindingRadioButton.IsChecked.Value ? (PupilFinder)new TemplatePupilFinder(videoFileName, progressBar, SetStatus, this.UpdateFrameWithPupil, this.OnFramesProcessed)
																		  : (PupilFinder)new HoughPupilFinder(videoFileName, progressBar, SetStatus, this.UpdateFrameWithPupil, this.OnFramesProcessed);
			RadiusPickerValuesChanged(null, null);

			VideoNameStatus.Text = videoFileName;
			VideoDurationStatus.Text = FramesToDurationString(pupilFinder.frameCount, pupilFinder.fps);
			VideoSizeStatus.Text = string.Format("{0}x{1}", pupilFinder.width, pupilFinder.height);
			FPSStatus.Text = string.Format("{0:##} fps", pupilFinder.fps);

			videoScaleFactor = VideoMediaElement.Width / pupilFinder.width;

			MillisecondsPerFrame = 1000 / pupilFinder.fps;
			timePerFrame = TimeSpan.FromMilliseconds(MillisecondsPerFrame);

			timer = new DispatcherTimer();
			timer.Interval = timePerFrame;
			timer.Tick += UpdateTimeDisplay;

			VideoMediaElement.Source = new Uri(videoFileName);
			VideoMediaElement.LoadedBehavior = MediaState.Manual;
			VideoMediaElement.Play();
			VideoMediaElement.Position = TimeSpan.Zero;
			VideoMediaElement.Pause();
			isPlaying = false;
			FindPupilsButton.IsEnabled = false;
			ReadTimestampButton.IsEnabled = true;
			LoadSavedTimeStampsButton.IsEnabled = true;

			// force update displays because
			PupilX = PupilX;
			PupilY = PupilY;
			PupilRadius = PupilRadius;
			SetStatus();
		}

		private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
		{
			if (VideoMediaElement.Source == null)
				return;
			IsPlaying = !IsPlaying;
		}

		private void VideoMediaElement_MediaEnded(object sender, RoutedEventArgs e)
		{
			VideoMediaElement.Position = TimeSpan.Zero;
			IsPlaying = false;
		}

		private void PreviousFrameButton_Click(object sender, RoutedEventArgs e)
		{
			if (VideoMediaElement.Position <= timePerFrame) return;

			VideoMediaElement.Position = VideoMediaElement.Position - timePerFrame;
			UpdateTimeDisplay(null, null);
			UpdateDisplayedPupilPosition();
		}

		private void NextFrameButton_Click(object sender, RoutedEventArgs e)
		{
			if (VideoMediaElement.Position >= VideoMediaElement.NaturalDuration - timePerFrame) return;

			VideoMediaElement.Position = VideoMediaElement.Position + timePerFrame;
			UpdateTimeDisplay(null, null);
			UpdateDisplayedPupilPosition();
		}

		private void UpdateTimeDisplay(object sender, EventArgs e)
		{
			VideoTimeLabel.Content = String.Format("{0:00}:{1:00}:{2:00}.{3:000}",
													VideoMediaElement.Position.Hours,
													VideoMediaElement.Position.Minutes,
													VideoMediaElement.Position.Seconds,
													VideoMediaElement.Position.Milliseconds);
			VideoSlider.Value = (VideoMediaElement.Position.TotalMilliseconds / VideoMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds) * 100;
		}

		/// <summary>
		/// Updates the displayed pupil after seeking through the video
		/// </summary>
		private void UpdateDisplayedPupilPosition()
		{
			int frameIndex = (int)((VideoMediaElement.Position.TotalMilliseconds / VideoMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds) * pupilFinder.frameCount);
			PupilX = pupilFinder.pupilLocations[frameIndex, 0];
			PupilY = pupilFinder.pupilLocations[frameIndex, 1];
			PupilRadius = pupilFinder.pupilLocations[frameIndex, 2];
		}

		private void VideoSlider_MouseDown(object sender, MouseButtonEventArgs e)
		{
			IsPlaying = false;
		}

		private void VideoSlider_MouseUp(object sender, MouseButtonEventArgs e)
		{
			VideoMediaElement.Position = TimeSpan.FromSeconds(VideoSlider.Value / VideoSlider.Maximum * VideoMediaElement.NaturalDuration.TimeSpan.TotalSeconds);
			UpdateTimeDisplay(null, null);
		}

		private void VideoSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			VideoMediaElement.Position = TimeSpan.FromSeconds(VideoSlider.Value / VideoSlider.Maximum * VideoMediaElement.NaturalDuration.TimeSpan.TotalSeconds);
			UpdateTimeDisplay(null, null);
		}

		private void VideoSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
		{
			IsPlaying = false;
		}

		private void DrawWindowButton_Click(object sender, RoutedEventArgs e)
		{
			if (editingState == EditingState.DrawingWindow)
			{
				editingState = EditingState.None;
				movePupilEllipseButton.IsChecked = false;
				drawWindowButton.IsChecked = false;
				isEditingStarted = false;
			}
			else
			{
				editingState = EditingState.DrawingWindow;
				movePupilEllipseButton.IsChecked = false;
				drawWindowButton.IsChecked = true;
			}
		}

		private void MovePupilEllipseButton_Click(object sender, RoutedEventArgs e)
		{
			if (editingState == EditingState.MovingPupil)
			{
				editingState = EditingState.None;
				movePupilEllipseButton.IsChecked = false;
				drawWindowButton.IsChecked = false;
				isEditingStarted = false;
			}
			else
			{
				editingState = EditingState.MovingPupil;
				movePupilEllipseButton.IsChecked = true;
				drawWindowButton.IsChecked = false;
			}
		}

		private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			mouseMoveStartPoint = e.GetPosition(canvas);
			switch (editingState)
			{
				case EditingState.DrawingWindow:
					if (mouseMoveStartPoint.X < 0 || mouseMoveStartPoint.Y < 0 || mouseMoveStartPoint.X > canvas.Width || mouseMoveStartPoint.Y > canvas.Height)
						return;
					
					Canvas.SetLeft(SearchWindowRectangle, mouseMoveStartPoint.X);
					Canvas.SetTop(SearchWindowRectangle, mouseMoveStartPoint.Y);

					isEditingStarted = true;
					break;
				case EditingState.MovingPupil:

					PupilX = mouseMoveStartPoint.X / videoScaleFactor;
					PupilY = mouseMoveStartPoint.Y / videoScaleFactor;
					isMovingPupilEllipse = true;

					isEditingStarted = true;
					break;
				default:
					return;
			}
		}

		private void Canvas_MouseMove(object sender, MouseEventArgs e)
		{
			if (!isEditingStarted) return;
			switch (editingState)
			{
				case EditingState.DrawingWindow:
					// TODO: allow r->l,b->t movement
					SearchWindowRectangle.Width = e.GetPosition(canvas).X - mouseMoveStartPoint.X;
					SearchWindowRectangle.Height = e.GetPosition(canvas).Y - mouseMoveStartPoint.Y;
					break;
				case EditingState.MovingPupil:
					PupilX = e.GetPosition(canvas).X / videoScaleFactor;
					PupilY = e.GetPosition(canvas).Y / videoScaleFactor;
					UpdatePupilPositionData();
					break;
				default:
					return;
			}
		}

		private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			isEditingStarted = false;
			switch (editingState)
			{
				case EditingState.DrawingWindow:
					isWindowManuallySet = true;
					drawWindowButton.IsChecked = false;
					break;
				case EditingState.MovingPupil:
					movePupilEllipseButton.IsChecked = false;
					break;
				default:
					return;
			}

			editingState = EditingState.None;
		}

		private void SearchWindowRectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			isMovingSearchWindow = true;
			mouseMoveStartPoint = e.GetPosition(canvas);
		}

		private void SearchWindowRectangle_MouseMove(object sender, MouseEventArgs e)
		{
			if (!isMovingSearchWindow) return;
			Canvas.SetLeft(SearchWindowRectangle, Canvas.GetLeft(SearchWindowRectangle) + e.GetPosition(canvas).X - mouseMoveStartPoint.X);
			Canvas.SetTop(SearchWindowRectangle, Canvas.GetTop(SearchWindowRectangle) + e.GetPosition(canvas).Y - mouseMoveStartPoint.Y);
		}

		private void SearchWindowRectangle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			isMovingSearchWindow = false;
		}

		private void OpenVideoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			OpenFileMenuItem_Click(sender, e);
		}

		private void OpenEyetrackingDataCommand_Executed(object sender, ExecutedRoutedEventArgs e)
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

		private void LoadSavedDataMenuItem_Click(object sender, RoutedEventArgs e)
		{
			LoadTimestamps();
		}

		private void ReadTimestampButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder != null)
			{
				pupilFinder.ParseTimeStamps();
				FindPupilsButton.IsEnabled = true;
			}
		}

		private void FindPupilsButton_Click(object sender, RoutedEventArgs e)
		{
			double l = Canvas.GetLeft(SearchWindowRectangle);
			pupilFinder.left = (int)(Canvas.GetLeft(SearchWindowRectangle) / canvas.Width * pupilFinder.width);
			pupilFinder.right = (int)(SearchWindowRectangle.Width / canvas.Width * pupilFinder.width) + pupilFinder.left;
			pupilFinder.top = (int)(Canvas.GetTop(SearchWindowRectangle) / canvas.Height * pupilFinder.height);
			pupilFinder.bottom = (int)(SearchWindowRectangle.Height / canvas.Height * pupilFinder.height) + pupilFinder.top;
			FindPupilsButton.IsEnabled = false;
			pupilFinder.FindPupils(FramesToProcessPicker.Value.Value);
		}

		private void LoadTimestamps()
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy"
			};
			if (openFileDialog.ShowDialog() == true)
			{
				pupilFinder.LoadTimestamps(openFileDialog.FileName);
				FindPupilsButton.IsEnabled = true;
			}
		}

		private void SaveTimestamps()
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy"
			};
			if (saveFileDialog.ShowDialog() == true)
			{
				pupilFinder.SaveTimestamps(saveFileDialog.FileName);
			}
		}

		public void UpdateFrameWithPupil(double time, double X, double Y, double radius)
		{
			VideoMediaElement.Position = TimeSpan.FromSeconds(time * VideoMediaElement.NaturalDuration.TimeSpan.TotalSeconds);
			PupilRadius = radius;
			PupilX = X;
			PupilY = Y;

			UpdateTimeDisplay(null, null);
		}

		private void ImageFilterValuesChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (pupilFinder == null) return;
			pupilFinder.bilateralBlurSize = BilateralBlurSizePicker.Value.Value;
			pupilFinder.bilateralSigmaColor = BilateralSigmaColorPicker.Value.Value;
			pupilFinder.bilateralSigmaSpace = BilateralSigmaSpacePicker.Value.Value;
			pupilFinder.medianBlurSize = MedianBlurSizePicker.Value.Value;

			FilterPreviewImage.Source = pupilFinder.GetFrameForDisplay();
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (SettingsTabs.SelectedIndex == 0)
				FilterPreviewImage.Visibility = Visibility.Hidden;
			else
			{
				FilterPreviewImage.Visibility = Visibility.Visible;
				ImageFilterValuesChanged(null, null);
			}
		}

		private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (editingState == EditingState.MovingPupil)
			{
				double deltaRadius = (e.Delta > 0 ? 1 : -1);
				double newSize = PupilRadius + deltaRadius;
				if (((newSize < pupilFinder.minRadius) && e.Delta < 0) || ((newSize > pupilFinder.maxRadius) && e.Delta > 0))
					return;
				PupilRadius = newSize;
				UpdatePupilPositionData();
			}
			else
			{
				if (e.Delta > 0)
					NextFrameButton_Click(null, null);
				else
					PreviousFrameButton_Click(null, null);
			}
		}

		private void RadiusPickerValuesChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (pupilFinder != null)
			{
				pupilFinder.minRadius = MinRadiusPicker.Value.Value;
				pupilFinder.maxRadius = MaxRadiusPicker.Value.Value;
			}
		}

		/// <summary>
		/// Update found pupil positions after the values are manually adjusted
		/// </summary>
		private void UpdatePupilPositionData()
		{
			// TODO: send values to pupil finder and update
		}

		private void UseImageAsTemplateButton_Click(object sender, RoutedEventArgs e)
		{
			int top = (int)(PupilY - (double)PupilRadius * 1.25);
			int bottom = (int)(PupilY + (double)PupilRadius * 1.25);
			int left = (int)(PupilX - (double)PupilRadius * 1.25);
			int right = (int)(PupilX + (double)PupilRadius * 1.25);
			((TemplatePupilFinder)pupilFinder).UseImageSegmentAsTemplate(top, bottom, left, right, PupilRadius);
		}

		private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
		{
			((TemplatePupilFinder)pupilFinder).MakeTemplates();
		}

		private void LoadDebugDataMenuItem_Click(object sender, RoutedEventArgs e)
		{
			LoadFile("D:\\run01.avi");
			pupilFinder.LoadTimestamps("D:\\test timestamps.npy");
			FindPupilsButton.IsEnabled = true;
		}

		/// <summary>
		/// Called when a chunk of frames is processed
		/// </summary>
		public void OnFramesProcessed()
		{
			FindPupilsButton.IsEnabled = true;
		}

		private void ExponentialFadeFramePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			ExponentialTotalFadeFrameLabel.Content = String.Format("{0} frames total", );
		}
	}


	// Commands
	public static class EyetrackerCommands
	{
		public static readonly RoutedUICommand OpenEyetrackingData = new RoutedUICommand("Open Eyetracking Data", "Open Eyetracking data", typeof(EyetrackerCommands),
																						new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Control) });
	}
}
