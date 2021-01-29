using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

using OpenCvSharp;

namespace Eyetracking
{

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
		private string videoFileName = null;

		private VideoCapture videoSource = null;

		private int width = -1;
		private int height = -1;
		private int fps = -1;
		private int frameCount = -1;
		private double duration = -1.0;

		private bool isPlaying = false;
		private DispatcherTimer timer;


		// eyetracking setup related stuff
		bool drawWindowMode = false;
		bool isDrawingWindow = false;
		private System.Windows.Point mouseMoveStartPoint;
		bool isMovingSearchWindow = false;
		bool isMovingPupilEllipse = false;

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
				return !(videoFileName == null);
			}
		}

		public MainWindow()
		{
			InitializeComponent();
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
				videoFileName = openFileDialog.FileName;
				LoadFile();
			}
		}

		private void SaveFileMenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void LoadFile()
		{
			videoSource = new VideoCapture(videoFileName);
			width = (int)videoSource.Get(VideoCaptureProperties.FrameWidth);
			height = (int)videoSource.Get(VideoCaptureProperties.FrameHeight);
			fps = (int)videoSource.Get(VideoCaptureProperties.Fps);
			frameCount = (int)videoSource.Get(VideoCaptureProperties.FrameCount);
			duration = frameCount / fps;

			VideoNameStatus.Text = videoFileName;
			VideoDurationStatus.Text = FramesToDurationString(frameCount, fps);
			VideoSizeStatus.Text = string.Format("{0}x{1}", width, height);
			FPSStatus.Text = string.Format("{0:##} fps", fps);

			MillisecondsPerFrame = 1000 / fps;
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

		}
	}


	// Commands
	public static class EyetrackerCommands
	{
		public static readonly RoutedUICommand OpenEyetrackingData = new RoutedUICommand("Open Eyetracking Data", "Open Eyetracking data", typeof(EyetrackerCommands),
																						new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Control) });
	}
}
