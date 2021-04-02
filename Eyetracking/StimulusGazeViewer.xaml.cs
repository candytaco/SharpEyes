using Microsoft.Win32;
using NumSharp;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Num = NumSharp.np;
using Point = System.Windows.Point;
using Window = System.Windows.Window;
using Sentry;
using System.Windows.Shapes;
using System.Windows.Media;

namespace Eyetracking
{
	/// <summary>
	/// Interaction logic for StimulusGazeViewer.xaml
	/// </summary>
	public partial class StimulusGazeViewer : Window, INotifyPropertyChanged
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

		public int GazeMarkerDiameter => GazeMarkerDiameterPicker.Value.Value;

		/// <summary>
		/// Start time of data in the stimulus video, in milliseconds
		/// </summary>
		private double? dataStartTime = null;

		/// <summary>
		/// End time of the gaze data in stimulus video time in milliseconds
		/// Can be beyond end of stimulus video.
		/// </summary>
		private double? dataEndTime = null;

		/// <summary>
		/// Timings of keyframes, i.e. frames on which the gaze location has been manually edited.
		/// Values are milliseconds from start.
		/// </summary>
		private List<VideoKeyFrame> videoKeyFrames;
		public List<VideoKeyFrame> VideoKeyFrames
		{
			get => videoKeyFrames;
		}

		private NDArray gazeLocations = null;

		/// <summary>
		/// Converts the current time into the video into an index into the gaze locations data array
		/// </summary>
		private int? frameIndex
		{
			get => VideoTimeToGazeDataIndex(VideoMediaElement.Position.TotalMilliseconds);
		}

		private bool isGazeLoaded = false; // extra because checking gazeLocations != null throws a NullReferenceException....

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

		private bool hasGaze2 = false;
		private NDArray gaze2Locations = null;
		private double _gaze2X = 0;
		private double _gaze2Y = 0;

		public double Gaze2X
		{
			get => _gaze2X;
			set
			{
				_gaze2X = value;
				Canvas.SetLeft(GazeEllipse2, value - GazeMarkerDiameterPicker.Value.Value / 2); // replaced by binding
			}
		}

		public double Gaze2Y
		{
			get => _gaze2Y;
			set
			{
				_gaze2Y = value;
				Canvas.SetTop(GazeEllipse2, value - GazeMarkerDiameterPicker.Value.Value / 2); // replaced by binding
			}
		}

		private List<Ellipse> trailEllipses;

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
		private Point mouseMoveStartPoint;

		/// <summary>
		/// Are we in the middle of moving the gaze location?
		/// </summary>
		private bool isMovingGaze = false;
		private string gazeFileName = null;

		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Default to not overwrite original file
		/// </summary>
		private string defaultSaveName => gazeFileName == null
			? "gaze locations"
			: System.IO.Path.GetFileNameWithoutExtension(gazeFileName) + " corrected.npy";

		public StimulusGazeViewer()
		{
			timer = new DispatcherTimer();
			videoKeyFrames = new List<VideoKeyFrame>();
			trailEllipses = new List<Ellipse>();
			InitializeComponent();
			SentrySdk.Init("https://4aa216608a894bd99da3daa7424c995d@o553633.ingest.sentry.io/5689896");
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

			// reset gaze information
			dataStartTime = null;
			dataEndTime = null;
			isGazeLoaded = false;
			gazeLocations = null;
			gaze2Locations = null;
			hasGaze2 = false;
			videoKeyFrames.Clear();
			KeyframesDataGrid.Items.Refresh();
			GazeEllipse.Visibility = Visibility.Hidden;
			GazeEllipse2.Visibility = Visibility.Hidden;
			foreach (Ellipse ellipse in trailEllipses)
				ellipse.Visibility = Visibility.Hidden;
			StatusText.Text = "Data not loaded";
			gazeX = 0;
			gazeY = 0;
			Gaze2X = 0;
			Gaze2Y = 0;
		}

		private void VideoOpened(object sender, RoutedEventArgs e)
		{
			VideoSlider.Maximum = VideoMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
			UpdateDisplays(null, null);
		}

		private void FindDataStart()
		{

		}

		/// <summary>
		/// Converts a video time in ms to an index into the gaze data array
		/// </summary>
		/// <param name="milliseconds"></param>
		/// <returns></returns>
		private int? VideoTimeToGazeDataIndex(double? milliseconds)
		{
			if (!isGazeLoaded) return null;
			if (milliseconds.HasValue)
				return (int) ((milliseconds.Value - dataStartTime) / eyetrackingFrameDuration);
			return null;
		}

		private void UpdateDisplays(object sender, EventArgs e)
		{
			VideoSlider.Value = VideoMediaElement.Position.TotalMilliseconds;

			VideoTimeLabel.Content = string.Format("{0:00}:{1:00}:{2:00};{3:#00}", VideoMediaElement.Position.Hours,
				VideoMediaElement.Position.Minutes,
				VideoMediaElement.Position.Seconds,
				(int)(VideoMediaElement.Position.Milliseconds / stimulusFrameDuration));

			if (isGazeLoaded)
			{
				if (VideoMediaElement.Position.TotalMilliseconds >= dataStartTime)
				{
					if (frameIndex < gazeLocations.shape[0])
					{
						gazeX = gazeLocations[frameIndex, 0];
						gazeY = gazeLocations[frameIndex, 1];

						if (hasGaze2)
						{

							Gaze2X = gaze2Locations[frameIndex, 0];
							Gaze2Y = gaze2Locations[frameIndex, 1];
						}

						if (trailEllipses.Count > 0)
						{
							for (int i = -1 * trailEllipses.Count; i < 0; i++)
							{
								if (frameIndex + i >= 0)
								{
									if (trailEllipses[trailEllipses.Count + i].Visibility != Visibility.Visible)
										trailEllipses[trailEllipses.Count + i].Visibility = Visibility.Visible;
									Canvas.SetLeft(trailEllipses[trailEllipses.Count + i], gazeLocations[frameIndex + i, 0] - GazeMarkerDiameterPicker.Value.Value / 2);
									Canvas.SetTop(trailEllipses[trailEllipses.Count + i], gazeLocations[frameIndex + i, 1] - GazeMarkerDiameterPicker.Value.Value / 2);
								}
							}
						}
					}
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
				GazeEllipse.Visibility = Visibility.Hidden;
				StatusText.Text = "Data start not set";
				dataStartTime = null;
				dataEndTime = null;
				videoKeyFrames.Clear();
				AddKeyFrameButton.IsEnabled = false;
				PreviousKeyFrameButton.IsEnabled = false;
				NextKeyFrameButton.IsEnabled = false;
				loadGaze2MenuItem.IsEnabled = true;
			}
		}

		private void SaveGazeMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (defaultSaveName == null)
			{
				SaveGazeAsMenuItem_Click(sender, e);
			}
			else
			{
				Num.save(defaultSaveName, gazeLocations);
			}
		}

		private void MoveGazeButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (moveGazeButton.IsChecked.Value)
			{
				IsPlaying = false;
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
				UpdateGazePositionData(e.GetPosition(canvas));
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

		/// <summary>
		/// Adds the current frame as a new keyframe
		/// </summary>
		private void AddKeyFrame()
		{
			AddKeyFrame(VideoMediaElement.Position.TotalMilliseconds);
		}

		/// <summary>
		/// Adds a particular time as a new keyframe
		/// </summary>
		/// <param name="time">Video time in milliseconds</param>
		private void AddKeyFrame(double time)
		{
			if (dataStartTime.HasValue && (time >= dataStartTime.Value) && (time <= dataEndTime.Value))
			{
				int? index = VideoTimeToGazeDataIndex(time);
				if (!index.HasValue) return;
				TimeSpan timeSpan = new TimeSpan((long)(10000 * time));
				for (int i = 0; i < videoKeyFrames.Count; i++)
				{
					// if this frame already exists as a keyframe, remove the old one
					if (videoKeyFrames[i].DataIndex == index.Value)
					{
						videoKeyFrames.RemoveAt(i);
						break;
					}
				}

				videoKeyFrames.Add(new VideoKeyFrame(time, index.Value, 
														 string.Format("{0:00}:{1:00}:{2:00};{3:#00}", 
																				timeSpan.Hours, timeSpan.Minutes,
																				timeSpan.Seconds, timeSpan.Milliseconds / stimulusFrameDuration), 
														 gazeLocations[index.Value, 0], gazeLocations[index.Value, 1]));
				videoKeyFrames.Sort((lhs, rhs) => lhs.VideoTime.CompareTo(rhs.VideoTime));
				KeyframesDataGrid.Items.Refresh();
			}
		}

		/// <summary>
		/// total millisecond value of the previous keyframe
		/// </summary>
		private double? PreviousVideoKeyFrame
		{
			get
			{
				for (int i = videoKeyFrames.Count - 1; i > -1; i--)
				{
					if (videoKeyFrames[i] < VideoMediaElement.Position.TotalMilliseconds)
						return videoKeyFrames[i].VideoTime;
				}
				return null;
			}
		}

		/// <summary>
		/// Convenience property that converts the previous key frame time to data index
		/// </summary>
		private int? PreviousGazeDataKeyFrame
		{
			get => VideoTimeToGazeDataIndex(PreviousVideoKeyFrame);
		}

		/// <summary>
		/// total millisecond value of the next keyframe
		/// </summary>
		private double? NextVideoKeyFrame
		{
			get
			{
				for (int i = 0; i < videoKeyFrames.Count; i++)
				{
					if (videoKeyFrames[i] > VideoMediaElement.Position.TotalMilliseconds)
						return videoKeyFrames[i].VideoTime;
				}
				return null;
			}
		}

		/// <summary>
		/// Convenience property that converts the next key frame time to data index
		/// </summary>
		private int? NextGazeDataKeyFrame
		{
			get => VideoTimeToGazeDataIndex(NextVideoKeyFrame);
		}

		/// <summary>
		/// Updates the stored gaze position after a manual edit. The update is as follows:
		/// The current frame will become a keyframe. The full delta will be applied to this frame
		/// and all successive frames. The delta will be interpolated to zero between the current
		/// frame and the previous keyframe.
		/// </summary>
		/// <param name="finalPositionOnMove">Final position when mouse is released</param>
		private void UpdateGazePositionData(Point finalPositionOnMove)
		{
			Vector delta = finalPositionOnMove - mouseMoveStartPoint;

			AddKeyFrame();

			double nFrames = (int)(frameIndex - PreviousGazeDataKeyFrame.Value);
			double multiplier;
			for (int i = PreviousGazeDataKeyFrame.Value; i < frameIndex; i++)
			{
				multiplier = (double) (i - PreviousGazeDataKeyFrame.Value) / nFrames;
				gazeLocations[i, 0] += multiplier * delta.X;
				gazeLocations[i, 1] += multiplier * delta.Y;
			}

			for (int i = frameIndex.Value; i < gazeLocations.shape[0]; i++)
			{
				gazeLocations[i, 0] += delta.X;
				gazeLocations[i, 1] += delta.Y;
			}
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
			dataEndTime = (gazeLocations.shape[0] - 1) * eyetrackingFrameDuration + dataStartTime.Value;

			videoKeyFrames.Clear();
			AddKeyFrame(dataStartTime.Value);
			AddKeyFrame(dataStartTime.Value + 1000 * 37 * 2);	// add the end of the eyetracking sequence as a keyframe, since that's still guaranteed to be good
			if (dataEndTime.Value >= VideoMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds)
				AddKeyFrame(VideoMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds);
			else AddKeyFrame(dataEndTime.Value);

			NextKeyFrameButton.IsEnabled = true;
			PreviousKeyFrameButton.IsEnabled = true;
			AddKeyFrameButton.IsEnabled = true;

			GazeEllipse.Visibility = Visibility.Visible;
			if (hasGaze2)
				GazeEllipse2.Visibility = Visibility.Visible;
			StatusText.Text = String.Format("Data start " + VideoTimeLabel.Content);

			UpdateDisplays(null, null);
		}

		private async void AutoFindDataStartButton_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Auto start only works with driving videos");

			progressBar.Visibility = Visibility.Visible;
			progressBar.IsIndeterminate = true;
			StatusText.Text = "Trying to find data start";
			videoSource.PosFrames = 0;
			int startTime = await DrivingVideoParser.FindStartTime(VideoNameStatus.Text);
			if (startTime < 0)
			{
				MessageBox.Show("Start finding failed");
				StatusText.Text = "Auto data start finding failed";
			}
			else
			{
				VideoMediaElement.Position = new TimeSpan(0, 0, 0, 0, startTime);
				UpdateDisplays(null, null);
				SetCurrentAsDataStartButton_Click(null, null);
			}
			progressBar.IsIndeterminate = false;
			progressBar.Visibility = Visibility.Collapsed;

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
			SetVideoPosition(PreviousVideoKeyFrame ?? 0);
		}

		private void NextKeyFrameButton_Click(object sender, RoutedEventArgs e)
		{
			SetVideoPosition(NextVideoKeyFrame ?? VideoMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds);
		}

		private void SaveGazeAsMenuItem_Click(object sender, RoutedEventArgs e)
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy",
				Title = "Save gaze as...",
				FileName = defaultSaveName
			};
			if (saveFileDialog.ShowDialog() == true)
			{
				Num.save(saveFileDialog.FileName, gazeLocations);
			}
		}

		private void OpenEyetrackingDataCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (loadGazeMenuItem.IsEnabled)
				LoadGazeMenuItem_Click(null, null);
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
			OpenVideoMenuItem_Click(null, null);
		}

		private void NewFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void SaveFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			SaveGazeMenuItem_Click(null, null);
		}

		private void SaveFileAsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			SaveGazeAsMenuItem_Click(null, null);
		}

		private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
		{
			MainWindow.ShowAboutDialogue();
		}

		private void EyetrackingFPSPicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			timer.Interval = new TimeSpan(10000000 / EyetrackingFPSPicker.Value.Value);
			eyetrackingFrameDuration = 1000.0 / EyetrackingFPSPicker.Value.Value;
		}

		private void GazeMarkerDiameterPicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			//foreach (UIElement child in canvas.Children)
			//{
			//	if (child is Ellipse ellipse)
			//	{
			//		if (ellipse.Name == "GazeEllipse" || ellipse.Name == "GazeEllipse2")
			//			break;	// these two already have their sizes bound to the picker value
			//		ellipse.Width = GazeMarkerDiameterPicker.Value.Value;
			//		ellipse.Height = GazeMarkerDiameterPicker.Value.Value;
			//	}
			//}
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
			{
				return;
			}

			VideoMediaElement.Position = new TimeSpan((long)(10000 * milliseconds));
			UpdateDisplays(null, null);
		}

		private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (VideoMediaElement.IsLoaded)
			{
				IsPlaying = false;
				if (e.Delta < 0)
				{
					NextFrameButton_Click(null, null);
				}
				else
				{
					PreviousFrameButton_Click(null, null);
				}
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (AutoSaveOnExitMenuItem.IsChecked && (gazeFileName != null))
			{
				Num.save(gazeFileName, gazeLocations);
			}
		}

		private void KeyframesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			VideoKeyFrame? selectedFrame = KeyframesDataGrid.SelectedItem as VideoKeyFrame?;
			if (selectedFrame.HasValue)
				SetVideoPosition(selectedFrame.Value.VideoTime);
		}

		private void AddKeyFrameButton_Click(object sender, RoutedEventArgs e)
		{
			AddKeyFrame();
		}

		private void LoadGaze2MenuItem_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy",
				Title = "Load second gaze locations..."
			};
			if (openFileDialog.ShowDialog() == true)
			{
				gaze2Locations = Num.load(openFileDialog.FileName);
				hasGaze2 = true;
			}
		}

		private void NumFramesToDisplayPicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			// if there are existing ones, remove them first
			if (trailEllipses.Count > 0)
			{
				foreach (Ellipse ellipse in trailEllipses)
					canvas.Children.Remove(ellipse);
				trailEllipses.Clear();
			}

			// num - 1 because we consider the current frame one to be part of the train
			int opacityStepSize = 64 / NumFramesToDisplayPicker.Value.Value;
			byte opacity = 64;
			for (byte i = 0; i < NumFramesToDisplayPicker.Value.Value - 1; i ++)
			{
				opacity -= (byte)opacityStepSize;
				Color color = new Color()
				{
					R = 0,
					G = 186,
					B = 255,
					A = opacity
				};
				Ellipse ellipse = new Ellipse()
				{
					Stroke = new SolidColorBrush(color),
					StrokeThickness = 10 + i * 4,
					Width = GazeMarkerDiameterPicker.Value.Value,
					Height = GazeMarkerDiameterPicker.Value.Value
				};
				ellipse.SetBinding(Ellipse.HeightProperty, "GazeMarkerDiameter");
				ellipse.SetBinding(Ellipse.WidthProperty, "GazeMarkerDiameter");
				canvas.Children.Add(ellipse);
				trailEllipses.Add(ellipse);
				if (!dataStartTime.HasValue)
					ellipse.Visibility = Visibility.Hidden;
			}
		}

		private void HelpMenuitem_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://github.com/candytaco/SharpEyes/wiki/Stimulus-Gaze-Viewer");
		}

		private void ReportBugmenuItem_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://github.com/candytaco/SharpEyes/issues/new/choose");
		}
	}
}
		