using System;
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
		public double videoScaleFactor { get; private set; }	// scaling fractor from video size to display size


		// eyetracking setup related stuff
		private bool drawWindowMode = false;
		private bool isDrawingWindow = false;
		private System.Windows.Point mouseMoveStartPoint;
		private bool isMovingSearchWindow = false;
		private bool isMovingPupilEllipse = false;
		private bool isWindowManuallySet = false;

		// data related stuff
		private EditingState editingState = EditingState.None;

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
			pupilFinder = new TemplatePupilFinder(videoFileName, progressBar, SetStatus, this.UpdateFrameWithPupil);

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
		}

		private void NextFrameButton_Click(object sender, RoutedEventArgs e)
		{
			if (VideoMediaElement.Position >= VideoMediaElement.NaturalDuration - timePerFrame) return;

			VideoMediaElement.Position = VideoMediaElement.Position + timePerFrame;
			UpdateTimeDisplay(null, null);
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
			drawWindowMode = true;
			drawWindowButton.IsChecked = true;
			// todo: something to change appearance of button
		}

		private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			mouseMoveStartPoint = e.GetPosition(canvas);
			if (drawWindowMode)
			{
				if (mouseMoveStartPoint.X < 0 || mouseMoveStartPoint.Y < 0 || mouseMoveStartPoint.X > canvas.Width || mouseMoveStartPoint.Y > canvas.Height)
					return;

				isDrawingWindow = true;
				Canvas.SetLeft(SearchWindowRectangle, mouseMoveStartPoint.X);
				Canvas.SetTop(SearchWindowRectangle, mouseMoveStartPoint.Y);
			}
			else
			{
				Canvas.SetLeft(PupilEllipse, mouseMoveStartPoint.X - PupilEllipse.Width / 2);
				Canvas.SetTop(PupilEllipse, mouseMoveStartPoint.Y - PupilEllipse.Height / 2);
				isMovingPupilEllipse = true;
			}
		}

		private void Canvas_MouseMove(object sender, MouseEventArgs e)
		{
			if (isDrawingWindow)
			{
				// TODO: allow r->l,b->t movement
				SearchWindowRectangle.Width = e.GetPosition(canvas).X - mouseMoveStartPoint.X;
				SearchWindowRectangle.Height = e.GetPosition(canvas).Y - mouseMoveStartPoint.Y;
			}
			else if (isMovingPupilEllipse)
			{
				Canvas.SetLeft(PupilEllipse, e.GetPosition(canvas).X - PupilEllipse.Width / 2);
				Canvas.SetTop(PupilEllipse, e.GetPosition(canvas).Y - PupilEllipse.Height / 2);
			}
		}

		private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (isDrawingWindow) isWindowManuallySet = true;

			isDrawingWindow = false;
			isMovingPupilEllipse = false;
			drawWindowMode = false;
			drawWindowButton.IsChecked = false;
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

		private void MovePupilEllipsedButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void ReadTimestampButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder != null)
				pupilFinder.ParseTimeStamps();
		}

		private void FindPupilsButton_Click(object sender, RoutedEventArgs e)
		{
			double l = Canvas.GetLeft(SearchWindowRectangle);
			pupilFinder.left = (int)(Canvas.GetLeft(SearchWindowRectangle) / canvas.Width * pupilFinder.width);
			pupilFinder.right = (int)(SearchWindowRectangle.Width / canvas.Width * pupilFinder.width) + pupilFinder.left;
			pupilFinder.top = (int)(Canvas.GetTop(SearchWindowRectangle) / canvas.Height * pupilFinder.height);
			pupilFinder.bottom = (int)(SearchWindowRectangle.Height / canvas.Height * pupilFinder.height) + pupilFinder.top;
			pupilFinder.FindPupils(100);
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
			PupilEllipse.Height = radius * 2 * videoScaleFactor;
			PupilEllipse.Width = PupilEllipse.Height;
			Canvas.SetLeft(PupilEllipse, X * videoScaleFactor);
			Canvas.SetTop(PupilEllipse, Y * videoScaleFactor);

			XPositionText.Text = string.Format("{0:.#}", X);
			YPositionText.Text = string.Format("{0:.#}", Y);
			RadiusText.Text = string.Format("{0:.#}", radius);

			UpdateTimeDisplay(null, null);
		}
	}


	// Commands
	public static class EyetrackerCommands
	{
		public static readonly RoutedUICommand OpenEyetrackingData = new RoutedUICommand("Open Eyetracking Data", "Open Eyetracking data", typeof(EyetrackerCommands),
																						new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Control) });
	}
}
