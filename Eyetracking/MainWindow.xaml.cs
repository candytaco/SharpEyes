using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Eyetracking
{
	public enum EditingState
	{
		None,
		DrawingWindow,
		MovingPupil
	}

	// Commands
	public static class EyetrackerCommands
	{
		public static readonly RoutedUICommand OpenEyetrackingData = new RoutedUICommand("Open Eyetracking Data", "Open Eyetracking data", typeof(EyetrackerCommands),
																						new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Control) });

		public static readonly RoutedUICommand Right = new RoutedUICommand("Right", "Right", typeof(EyetrackerCommands),
																			new InputGestureCollection() { new KeyGesture(Key.Right) });

		public static readonly RoutedUICommand Left = new RoutedUICommand("Left", "Left", typeof(EyetrackerCommands),
																		  new InputGestureCollection() { new KeyGesture(Key.Left) });

		public static readonly RoutedUICommand Up = new RoutedUICommand("Up", "Up", typeof(EyetrackerCommands),
																		  new InputGestureCollection() { new KeyGesture(Key.Up) });

		public static readonly RoutedUICommand Down = new RoutedUICommand("Down", "Down", typeof(EyetrackerCommands),
																		  new InputGestureCollection() { new KeyGesture(Key.Down) });

		public static readonly RoutedUICommand PlayPause = new RoutedUICommand("Play/Pause", "Play/Pause", typeof(EyetrackerCommands),
																			   new InputGestureCollection() { new KeyGesture(Key.Space) });

		public static readonly RoutedUICommand IncreasePupilSize = new RoutedUICommand("Increase Pupil Size", "Increase Pupil Size", typeof(EyetrackerCommands),
																					   new InputGestureCollection() { new KeyGesture(Key.OemCloseBrackets) });

		public static readonly RoutedUICommand DecreasePupilSize = new RoutedUICommand("Decrease Pupil Size", "Decrease Pupil Size", typeof(EyetrackerCommands),
																					   new InputGestureCollection() { new KeyGesture(Key.OemOpenBrackets) });

		public static readonly RoutedUICommand DrawWindow = new RoutedUICommand("Draw window", "Draw window", typeof(EyetrackerCommands),
																				new InputGestureCollection() { new KeyGesture(Key.M, ModifierKeys.Alt) });

		public static readonly RoutedUICommand MovePupil = new RoutedUICommand("Move pupil", "Move pupil", typeof(EyetrackerCommands),
																			   new InputGestureCollection() { new KeyGesture(Key.V, ModifierKeys.Alt) });
	};

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
			int hours = seconds / 3600;
			seconds -= hours * 3600;
			int minutes = seconds / 60;
			seconds -= minutes * 60;
			int frames = frameCount % fps;
			return String.Format("{0:00}:{1:00}:{2:00};{3:00}", hours, minutes, seconds, frames);
		}

		// video play related stuff
		private PupilFinder pupilFinder = null;
		private bool isPlaying = false;
		private DispatcherTimer timer;
		public double videoScaleFactor { get; private set; }    // scaling fractor from video size to display size
		private int framesPerHour, framesPerMinute;


		// eyetracking setup related stuff
		private bool isEditingStarted = false;  // generic flag for indicating whether the window/pupil edit has begun with a mouseclick
		private System.Windows.Point mouseMoveStartPoint;
		private bool isMovingSearchWindow = false;

		// data related stuff
		private EditingState _editingState = EditingState.None;
		private EditingState editingState
		{
			get { return _editingState; }
			set
			{
				_editingState = value;
				// the switching is disable for the while being until I can debug this issue.
				//if (_editingState == EditingState.MovingPupil)
				//{
				//	RightCommand.Executed -= NextFrameCommand_Executed;
				//	RightCommand.Executed += MovePupilRight;

				//	LeftCommand.Executed -= PrevFrameCommand_Executed;
				//	LeftCommand.Executed += MovePupilLeft;

				//	UpCommand.Executed += MovePupilUp;
				//	DownCommand.Executed += MovePupilDown;
				//}
				//else
				//{
				//	RightCommand.Executed -= MovePupilRight;
				//	RightCommand.Executed += NextFrameCommand_Executed;

				//	LeftCommand.Executed -= MovePupilLeft;
				//	LeftCommand.Executed += PrevFrameCommand_Executed;

				//	UpCommand.Executed -= MovePupilUp;
				//	DownCommand.Executed -= MovePupilDown;
				//}
			}
		}
		private bool isPupilManullySetOnThisFrame = false;

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
				if (value < 0 && PupilEllipse.IsVisible)
					PupilEllipse.Visibility = Visibility.Hidden;
				else if (value > 0 && !PupilEllipse.IsVisible)
					PupilEllipse.Visibility = Visibility.Visible;
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
				{
					return;
				}

				double X = PupilX;
				double Y = PupilY;
				PupilEllipse.Width = PupilEllipse.Height = value * 2 * videoScaleFactor;
				PupilX = X;
				PupilY = Y;
				RadiusText.Text = string.Format("Radius: {0:####.#}", value);
			}
		}
		private double _pupilConfidence = 1;
		private double PupilConfidence
		{
			get { return _pupilConfidence; }
			set
			{
				_pupilConfidence = value;
				ConfidenceText.Text = String.Format("Confidence: {0:#.####}", value);
				double transparency = value;
				if (transparency < 0.2)
				{
					transparency = 0.2;
				}
				else if (transparency > 1)
				{
					transparency = 1;
				}
				// during moving pupil, we deep the drawn pupil at 50% transparency
				if (editingState != EditingState.MovingPupil)
					PupilEllipse.Stroke.Opacity = transparency;
			}
		}

		private int _templatePreviewIndex = -1;
		/// <summary>
		/// Template preview
		/// </summary>
		private int TemplatePreviewIndex
		{
			get
			{ return _templatePreviewIndex; }
			set
			{
				if (pupilFinder is TemplatePupilFinder templatePupilFinder)
				{
					_templatePreviewIndex = value;
					if (_templatePreviewIndex < 0)
					{
						_templatePreviewIndex = 0;
					}
					else if (_templatePreviewIndex >= templatePupilFinder.NumTemplates)
					{
						_templatePreviewIndex = templatePupilFinder.NumTemplates - 1;
					}

					TemplatePreviewNumberLabel.Content = String.Format("{0}/{1}", _templatePreviewIndex + 1, templatePupilFinder.NumTemplates);
					TemplatePreviewImage.Source = templatePupilFinder.GetTemplateImage(_templatePreviewIndex);
				}
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
					timer.Start();
				}
				else
				{
					PlayPauseButton.Content = "Play";
					timer.Stop();
				}
			}
		}

		public double MillisecondsPerFrame { get; private set; } = 0;
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

		private void SaveTimeStampsMenuItemClick(object sender, RoutedEventArgs e)
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
			pupilFinder = TemplatePupilFindingRadioButton.IsChecked.Value ? new TemplatePupilFinder(videoFileName, progressBar, taskbarItemInfo, SetStatus, UpdateDisplays, OnFramesProcessed)
																		  : (PupilFinder)new HoughPupilFinder(videoFileName, progressBar, taskbarItemInfo, SetStatus, UpdateDisplays, OnFramesProcessed);
			RadiusPickerValuesChanged(null, null);

			VideoNameStatus.Text = videoFileName;
			VideoDurationStatus.Text = FramesToDurationString(pupilFinder.frameCount, pupilFinder.fps);
			VideoSizeStatus.Text = string.Format("{0}x{1}", pupilFinder.width, pupilFinder.height);
			FPSStatus.Text = string.Format("{0:##} fps", pupilFinder.fps);

			videoScaleFactor = VideoFrameImage.Width / pupilFinder.width;

			MillisecondsPerFrame = 1000.0 / pupilFinder.fps;
			timePerFrame = TimeSpan.FromMilliseconds(MillisecondsPerFrame);
			framesPerHour = pupilFinder.fps * 60 * 60;
			framesPerMinute = pupilFinder.fps * 60;

			VideoSlider.Maximum = pupilFinder.frameCount - 1;

			timer = new DispatcherTimer();
			timer.Interval = timePerFrame;
			timer.Tick += VideoPlayTimerTick;

			isPlaying = false;
			FindPupilsButton.IsEnabled = false;
			ReadTimestampButton.IsEnabled = true;
			LoadSavedTimeStampsButton.IsEnabled = true;
			saveEyetrackingMenuItem.IsEnabled = true;

			// force update displays because
			PupilX = PupilX;
			PupilY = PupilY;
			PupilRadius = PupilRadius;

			// enable scrubbing buttongs
			PreviousFrameButton.IsEnabled = true;
			PlayPauseButton.IsEnabled = true;
			NextFrameButton.IsEnabled = true;
			StepBackButton.IsEnabled = true;

			// if the pupil finder found auto save files
			// we immediately enable pupil finding
			List<string> autoloads = new List<string>(); ;
			if (pupilFinder.isTimestampParsed)
			{
				FindPupilsButton.IsEnabled = true;
				autoloads.Add("timestamps");
			}

			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				TemplatePreviewIndex = 0;
				loadSavedTemplatesMenuItem.IsEnabled = true;
				if (templatePupilFinder.IsUsingCustomTemplates)
				{
					saveTemplatesMenuItem.IsEnabled = true;
					ResetTemplatesButton.IsEnabled = true;
					autoloads.Add("templates");

					if (templatePupilFinder.NumTemplates > 1)
						DeleteTemplateButton.Visibility = Visibility.Visible;
				}
			}

			if (pupilFinder.isAnyFrameProcessed)
			{
				autoloads.Add("pupils");
			}

			switch (autoloads.Count)
			{
				case 1:
					SetStatus(String.Format("Autoloaded {0}", autoloads[0]));
					break;
				case 2:
					SetStatus(String.Format("Autoloaded {0} & {1}", autoloads[0], autoloads[1]));
					break;
				case 3:
					SetStatus("Autoloaded timestamps, templates, & pupils");
					break;
				default:
					SetStatus();
					break;
			}

			pupilFinder.ReadGrayscaleFrame();
			UpdateDisplays();
			pupilFinder.Seek(0);
			saveAllMenuItem.IsEnabled = true;

			UpdateFramesProcessedPreviewImage();
		}

		private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
		{
			IsPlaying = !IsPlaying;
		}

		private void PreviousFrameButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder.CurrentFrameNumber <= 0)
			{
				return;
			}

			UpdateVideoTime(pupilFinder.CurrentFrameNumber - 1);
		}

		private void NextFrameButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder.CurrentFrameNumber >= pupilFinder.frameCount - 1)
			{
				return;
			}

			UpdateVideoTime(pupilFinder.CurrentFrameNumber + 1);
		}

		/// <summary>
		/// Aggregate handle for the multitude of UI controls that scrub through the video
		/// </summary>
		/// <param name="frame">Frame to set the video to</param>
		private void UpdateVideoTime(int frame)
		{
			pupilFinder.CurrentFrameNumber = frame;
			UpdateDisplays();
		}

		private void VideoPlayTimerTick(object sender, EventArgs e)
		{
			pupilFinder.CurrentFrameNumber++;
			UpdateDisplays();
		}

		/// <summary>
		/// Updates the displays pupil and video information
		/// </summary>
		private void UpdateDisplays()
		{
			try
			{
				int frames = pupilFinder.CurrentFrameNumber;
				VideoSlider.Value = frames;

				int hours = frames / framesPerHour;
				frames -= hours * framesPerHour;
				int minutes = frames / framesPerMinute;
				frames -= minutes * framesPerMinute;
				int seconds = frames / pupilFinder.fps;
				frames -= seconds * pupilFinder.fps;
				VideoTimeLabel.Content = String.Format("{0:00}:{1:00}:{2:00};{3:#00}", hours, minutes, seconds, frames + 1);
				VideoFrameImage.Source = pupilFinder.GetFrameForDisplay(ShowFilteredVideoButton.IsChecked.Value);

				PupilX = pupilFinder.pupilLocations[pupilFinder.CurrentFrameNumber, 0];
				PupilY = pupilFinder.pupilLocations[pupilFinder.CurrentFrameNumber, 1];
				PupilRadius = pupilFinder.pupilLocations[pupilFinder.CurrentFrameNumber, 2];
				PupilConfidence = pupilFinder.pupilLocations[pupilFinder.CurrentFrameNumber, 3];

				// at some point check if this could be moved into an arg and passed in from the pupil finders,
				// which would need some change in the delegate signature
				isPupilManullySetOnThisFrame = false;
			}
			catch (Exception e)
			{
				MessageBox.Show(e.ToString(), "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void VideoSlider_MouseDown(object sender, MouseButtonEventArgs e)
		{
			IsPlaying = false;
		}

		private void VideoSlider_MouseUp(object sender, MouseButtonEventArgs e)
		{
			UpdateVideoTime((int)VideoSlider.Value);
		}

		private void VideoSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			UpdateVideoTime((int)VideoSlider.Value);
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
					{
						return;
					}

					Canvas.SetLeft(SearchWindowRectangle, mouseMoveStartPoint.X);
					Canvas.SetTop(SearchWindowRectangle, mouseMoveStartPoint.Y);

					isEditingStarted = true;
					break;
				case EditingState.MovingPupil:

					PupilX = mouseMoveStartPoint.X / videoScaleFactor;
					PupilY = mouseMoveStartPoint.Y / videoScaleFactor;
					if (Double.IsNaN(PupilRadius))
						PupilRadius = 16;

					// make transparent so we can see better
					PupilEllipse.Stroke.Opacity = 0.5;

					isEditingStarted = true;
					break;
				default:
					return;
			}
		}

		private void Canvas_MouseMove(object sender, MouseEventArgs e)
		{
			if (!isEditingStarted)
			{
				return;
			}

			switch (editingState)
			{
				case EditingState.DrawingWindow:
					// confine to canvas
					Point mouse = e.GetPosition(canvas);
					mouse.X = mouse.X > 0 ? mouse.X : 0;
					mouse.X = mouse.X < canvas.Width ? mouse.X : canvas.Width;
					mouse.Y = mouse.Y > 0 ? mouse.Y : 0;
					mouse.Y = mouse.Y < canvas.Height ? mouse.Y : canvas.Height;

					Canvas.SetLeft(SearchWindowRectangle, mouse.X < mouseMoveStartPoint.X ? mouse.X : mouseMoveStartPoint.X);
					Canvas.SetTop(SearchWindowRectangle, mouse.Y < mouseMoveStartPoint.Y ? mouse.Y : mouseMoveStartPoint.Y);
					SearchWindowRectangle.Width = Math.Abs(e.GetPosition(canvas).X - mouseMoveStartPoint.X);
					SearchWindowRectangle.Height = Math.Abs(e.GetPosition(canvas).Y - mouseMoveStartPoint.Y);
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
					drawWindowButton.IsChecked = false;
					editingState = EditingState.None;
					break;
				case EditingState.MovingPupil:  // do not auto turn off pupil editing
					// undo the temporary transparency
					PupilEllipse.Stroke.Opacity = 1;
					UpdatePupilPositionData();
					break;
				default:    // EditingState.None
					return;
			}
		}

		private void SearchWindowRectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			isMovingSearchWindow = true;
			mouseMoveStartPoint = e.GetPosition(canvas);
		}

		private void SearchWindowRectangle_MouseMove(object sender, MouseEventArgs e)
		{
			if (!isMovingSearchWindow)
			{
				return;
			}

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
			LoadSavedDataMenuItem_Click(null, null);
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
				ReadTimestampButton.IsEnabled = false;
				LoadSavedTimeStampsButton.IsEnabled = false;
				pupilFinder.OnTimeStampsFound += delegate ()
												{
													ReadTimestampButton.IsEnabled = true;
													LoadSavedTimeStampsButton.IsEnabled = true;
												};
				pupilFinder.Seek(0);
				pupilFinder.ParseTimeStamps();
				FindPupilsButton.IsEnabled = true;
				saveTimestampsMenuItem.IsEnabled = true;
			}
		}

		private void FindPupils(int frames, object sender)
		{
			// disable pupil editing if needed
			if (editingState == EditingState.MovingPupil)
			{
				editingState = EditingState.None;
				movePupilEllipseButton.IsChecked = false;
				drawWindowButton.IsChecked = false;
				isEditingStarted = false;
			}

			if (AutoAddCustomTemplateCheckBox.IsChecked.Value && isPupilManullySetOnThisFrame)
			{
				UseImageAsTemplateButton_Click(null, null);
			}

			double l = Canvas.GetLeft(SearchWindowRectangle);
			pupilFinder.left = (int)(Canvas.GetLeft(SearchWindowRectangle) / canvas.Width * pupilFinder.width);
			pupilFinder.right = (int)(SearchWindowRectangle.Width / canvas.Width * pupilFinder.width) + pupilFinder.left;
			pupilFinder.top = (int)(Canvas.GetTop(SearchWindowRectangle) / canvas.Height * pupilFinder.height);
			pupilFinder.bottom = (int)(SearchWindowRectangle.Height / canvas.Height * pupilFinder.height) + pupilFinder.top;

			UpdatePupilFindingButtons(true);
			
			if (frames + pupilFinder.CurrentFrameNumber >= pupilFinder.frameCount)
			{
				frames = pupilFinder.frameCount - pupilFinder.CurrentFrameNumber - 1;
			}

			pupilFinder.FindPupils(frames);
		}

		private void FindPupilsButton_Click(object sender, RoutedEventArgs e)
		{
			if (FindPupilsAllFramesCheckBox.IsChecked.Value)
				FindPupils(pupilFinder.frameCount - pupilFinder.CurrentFrameNumber, sender);
			else
				FindPupils(FramesToProcessPicker.Value.Value, sender);
		}

		private void LoadTimestamps()
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy",
				Title = "Load timestamps..."
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
				Filter = "Numpy file (*.npy)|*.npy",
				Title = "Save timestamps...",
				FileName = Path.GetFileNameWithoutExtension(pupilFinder.autoTimestampFileName)
			};
			if (saveFileDialog.ShowDialog() == true)
			{
				pupilFinder.SaveTimestamps(saveFileDialog.FileName);
			}
		}

		private void ImageFilterValuesChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (pupilFinder == null)
			{
				return;
			}

			pupilFinder.bilateralBlurSize = BilateralBlurSizePicker.Value.Value;
			pupilFinder.bilateralSigmaColor = BilateralSigmaColorPicker.Value.Value;
			pupilFinder.bilateralSigmaSpace = BilateralSigmaSpacePicker.Value.Value;
			pupilFinder.medianBlurSize = MedianBlurSizePicker.Value.Value;
			pupilFinder.FilterCurrentFrame();

			VideoFrameImage.Source = pupilFinder.GetFrameForDisplay(true);
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (SettingsTabs.SelectedIndex == 1)
			{
				ShowFilteredVideoButton.IsChecked = true;
				ShowFilteredVideoButton.IsEnabled = false;
				ImageFilterValuesChanged(null, null);
			}
			else
			{
				try
				{
					ShowFilteredVideoButton.IsChecked = false;
					ShowFilteredVideoButton.IsEnabled = true;
					VideoFrameImage.Source = pupilFinder.GetFrameForDisplay(false);
				}
				catch (Exception) { }
			}; // null reference during initialization
		}

		private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (editingState == EditingState.MovingPupil)
			{
				double deltaRadius = (e.Delta > 0 ? 1 : -1);
				double newSize = PupilRadius + deltaRadius;
				if (((newSize < pupilFinder.minRadius) && e.Delta < 0) || ((newSize > pupilFinder.maxRadius) && e.Delta > 0))
				{
					return;
				}

				PupilRadius = newSize;
				UpdatePupilPositionData();
			}
			else
			{
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
			if (pupilFinder != null)
			{
				PupilConfidence = 1;
				int frameDecay;
				ManualUpdateMode mode;
				if (LinearDecayRadioButton.IsChecked.Value)
				{
					frameDecay = LinearFadeFramesPicker.Value.Value;
					mode = ManualUpdateMode.Linear;
				}
				else
				{
					frameDecay = ExponentialFadeFramePicker.Value.Value;
					mode = ManualUpdateMode.Exponential;
				}
				pupilFinder.ManuallyUpdatePupilLocations(pupilFinder.CurrentFrameNumber, PupilX, PupilY, PupilRadius, frameDecay, mode);
			}
			isPupilManullySetOnThisFrame = true;
		}

		private void SetTemplatePreviewImage()
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				TemplatePreviewImage.Source = templatePupilFinder.GetTemplateImage();
			}
		}

		private void UseImageAsTemplateButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				int top = (int)(PupilY - PupilRadius * 1.5);
				int bottom = (int)(PupilY + PupilRadius * 1.5 + 2);
				int left = (int)(PupilX - PupilRadius * 1.5);
				int right = (int)(PupilX + PupilRadius * 1.5 + 2);
				templatePupilFinder.AddImageSegmentAsTemplate(top, bottom, left, right, PupilRadius);
				TemplatePreviewIndex = templatePupilFinder.NumTemplates;
				if (!saveTemplatesMenuItem.IsEnabled)
				{
					saveTemplatesMenuItem.IsEnabled = true;
					ResetTemplatesButton.IsEnabled = true;
				}
				if (templatePupilFinder.NumTemplates > 1)
					DeleteTemplateButton.Visibility = Visibility.Visible;
			}
		}

		private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
		{
			((TemplatePupilFinder)pupilFinder).MakeTemplates();
			TemplatePreviewIndex = 0;
			saveTemplatesMenuItem.IsEnabled = false;
			ResetTemplatesButton.IsEnabled = false;
			DeleteTemplateButton.Visibility = Visibility.Hidden;
		}

		/// <summary>
		/// Called when a chunk of frames is processed
		/// </summary>
		public void OnFramesProcessed()
		{
			UpdatePupilFindingButtons(false);
			UpdateFramesProcessedPreviewImage();
		}

		private void ExponentialFadeFramePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (pupilFinder == null)
			{
				return;
			}

			double oldX = pupilFinder.pupilLocations[pupilFinder.CurrentFrameNumber, 0];
			double oldY = pupilFinder.pupilLocations[pupilFinder.CurrentFrameNumber, 1];
			double dD = Math.Sqrt((oldX - PupilX) * (oldX - PupilX) + (oldY - PupilY) * (oldY - PupilY));
			if (dD < 1)
			{
				ExponentialTotalFadeFrameLabel.Content = String.Format("Position unchanged");
			}
			else
			{
				int frames = (int)(ExponentialFadeFramePicker.Value.Value * Math.Log(dD));
				ExponentialTotalFadeFrameLabel.Content = String.Format("{0} frames total", frames);
			}
		}

		private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
		{
			Version version = Assembly.GetEntryAssembly().GetName().Version;
			Assembly assembly = Assembly.GetExecutingAssembly();
			string build = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
			MessageBox.Show(String.Format("SharpEyes\nVersion {0} {1} \nIcon from Icons8\nLibraries: Numsharp Lite & OpenCVSharp\nt.zhang\nThis is a work in progress and a lot of things don't work.", version, build),
							"About SharpEyes", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void TabControl_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
		{
			if (ManualAdjustmentDecayOptionsTabs.SelectedIndex == 1)
			{
				ExponentialFadeFramePicker_ValueChanged(null, null);
			}
		}

		private void AdjustmentModeSelected(object sender, RoutedEventArgs e)
		{
			if (ManualAdjustmentDecayOptionsTabs == null)
			{
				return; // preconstruction references not set
			}

			if (LinearDecayRadioButton.IsChecked.Value)
			{
				ManualAdjustmentDecayOptionsTabs.SelectedIndex = 0;
			}
			else
			{
				ManualAdjustmentDecayOptionsTabs.SelectedIndex = 1;
			}
		}

		private void SaveEyetrackingMenuItem_Click(object sender, RoutedEventArgs e)
		{
			//if (!pupilFinder.AreAllFramesProcessed)
			//{
			//	if (MessageBox.Show("Not all frames processed. Save data?", "Incomplete processing", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
			//	{
			//		return;
			//	}
			//}

			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy",
				Title = "Save pupils...",
				FileName = Path.GetFileNameWithoutExtension(pupilFinder.autoPupilsFileName)
			};
			if (saveFileDialog.ShowDialog() == true)
			{
				pupilFinder.SavePupilLocations(saveFileDialog.FileName);
			}
		}

		private void LoadSavedEyetrackingMenuItem_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy",
				Title = "Load pupils..."
			};
			if (openFileDialog.ShowDialog() == true)
			{
				pupilFinder.LoadPupilLocations(openFileDialog.FileName);
				FindPupilsButton.IsEnabled = true;
				UpdateDisplays();
			}
		}

		/// <summary>
		/// Changes the appearance of Find Pupils/Find Pupils All Frames depending on state
		/// </summary>
		/// <param name="isPupilFinding">Are we entering pupil finding mode?</param>
		private void UpdatePupilFindingButtons(bool isPupilFinding)
		{
			FindPupilsButton.Content = isPupilFinding ? "Cancel" : "Find Pupils";
			if (isPupilFinding)
			{
				FindPupilsButton.Click -= FindPupilsButton_Click;
				FindPupilsButton.Click += CancelPupilFindingButton_Click;
			}
			else
			{
				FindPupilsButton.Click += FindPupilsButton_Click;
				FindPupilsButton.Click -= CancelPupilFindingButton_Click;
				if (AutoStartPupilEditModeCheckBox.IsChecked.Value)
				{
					editingState = EditingState.MovingPupil;
					movePupilEllipseButton.IsChecked = true;
				}
			}
			StepBackButton.IsEnabled = !isPupilFinding;
		}

		private void CancelPupilFindingButton_Click(object sender, RoutedEventArgs e)
		{
			pupilFinder.CancelPupilFinding();
		}

		private void PreviousTemplateButton_Click(object sender, RoutedEventArgs e)
		{
			TemplatePreviewIndex--;
		}

		private void LoadSavedTemplatesMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				OpenFileDialog openFileDialog = new OpenFileDialog
				{
					Filter = "Data file (*.dat)|*.dat",
					Title = "Load templates..."
				};
				if (openFileDialog.ShowDialog() == true)
				{
					templatePupilFinder.LoadTemplates(openFileDialog.FileName);
					TemplatePreviewIndex = templatePupilFinder.NumTemplates;
					SetStatus(String.Format("Idle; Loaded {0} templates", templatePupilFinder.NumTemplates));

					if (templatePupilFinder.NumTemplates > 1)
						DeleteTemplateButton.Visibility = Visibility.Visible;
				}
			}
		}

		private void SaveTemplatesMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				if (!templatePupilFinder.IsUsingCustomTemplates)
				{
					MessageBox.Show("Pupil finder not using custom templates", "No templates to save", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
				SaveFileDialog saveFileDialog = new SaveFileDialog
				{
					Filter = "Data file (*.dat)|*.dat",
					Title = "Save templates...",
					FileName = Path.GetFileNameWithoutExtension(templatePupilFinder.autoTemplatesFileName)
				};
				if (saveFileDialog.ShowDialog() == true)
				{
					templatePupilFinder.SaveTemplates(saveFileDialog.FileName);
				}
			}
			else
			{
				MessageBox.Show("Not a template pupil finder", "Wrong pupil finder", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void NextFrameCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			NextFrameButton_Click(null, null);
		}

		private void PrevFrameCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			PreviousFrameButton_Click(null, null);
		}

		private void PlayPauseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			IsPlaying = !IsPlaying;
		}

		private void SaveAllMenuItem_Click(object sender, RoutedEventArgs e)
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Filter = "Directory | directory",
				Title = "Save all into folder...",
			};
			if (saveFileDialog.ShowDialog() == true)
			{
				pupilFinder.SaveTimestamps(Path.Combine(Path.GetDirectoryName(saveFileDialog.FileName),
														String.Format("{0} timestamps.npy", Path.GetFileNameWithoutExtension(pupilFinder.videoFileName))));
				pupilFinder.SavePupilLocations(Path.Combine(Path.GetDirectoryName(saveFileDialog.FileName),
															String.Format("{0} pupils.npy", Path.GetFileNameWithoutExtension(pupilFinder.videoFileName))));
				if (pupilFinder is TemplatePupilFinder templatePupilFinder)
				{
					templatePupilFinder.SaveTemplates(Path.Combine(Path.GetDirectoryName(saveFileDialog.FileName),
																   String.Format("{0} templates.dat", Path.GetFileNameWithoutExtension(pupilFinder.videoFileName))));
				}
			}
		}

		private void MovePupilUp(object sender, ExecutedRoutedEventArgs e)
		{
			if (PupilY >= 1)
			{
				PupilY--;
				UpdatePupilPositionData();
			}
		}

		private void MovePupilDown(object sender, ExecutedRoutedEventArgs e)
		{
			if (PupilY <= pupilFinder.height - 2)
			{
				PupilY++;
				UpdatePupilPositionData();
			}
		}

		private void MovePupilLeft(object sender, ExecutedRoutedEventArgs e)
		{
			if (PupilX >= 1)
			{
				PupilX--;
				UpdatePupilPositionData();
			}
		}

		private void MovePupilRight(object sender, ExecutedRoutedEventArgs e)
		{
			if (PupilX <= pupilFinder.width - 2)
			{
				PupilX++;
				UpdatePupilPositionData();
			}
		}

		private void IncreasePupilSizeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (editingState == EditingState.MovingPupil)
			{
				double newSize = PupilRadius + 1;
				if (newSize > pupilFinder.maxRadius)
				{
					return;
				}

				PupilRadius = newSize;
				UpdatePupilPositionData();
			}
		}

		private void DecreasePupilSizeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (editingState == EditingState.MovingPupil)
			{
				double newSize = PupilRadius - 1;
				if (newSize < pupilFinder.minRadius)
				{
					return;
				}

				PupilRadius = newSize;
				UpdatePupilPositionData();
			}
		}

		private void FindPupilsAllFramesCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			FramesToProcessPicker.IsEnabled = !FindPupilsAllFramesCheckBox.IsChecked.Value;
		}

		private void StepBackButton_Click(object sender, RoutedEventArgs e)
		{
			UpdateVideoTime(pupilFinder.CurrentFrameNumber - FramesToProcessPicker.Value.Value);
		}

		private void ShowFilteredVideoButton_Checked(object sender, RoutedEventArgs e)
		{
			UpdateDisplays();
		}

		private void DrawWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			DrawWindowButton_Click(null, null);
		}

		private void MovePupilCommand_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			MovePupilEllipseButton_Click(null, null);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (AutoSaveOnExitMenuItem.IsChecked && pupilFinder != null)
			{
				pupilFinder.SaveTimestamps();
				pupilFinder.SavePupilLocations();
				if (pupilFinder is TemplatePupilFinder templatePupilFinder)
					templatePupilFinder.SaveTemplates();
			}
		}

		private void DeleteTemplateButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				templatePupilFinder.RemoveTemplate(TemplatePreviewIndex--);
				if (templatePupilFinder.NumTemplates < 2)	// no deleting last one!
					DeleteTemplateButton.Visibility = Visibility.Hidden;
			}
		}

		private void NRecentTemplatesPicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
				templatePupilFinder.NumActiveTemplates = NRecentTemplatesPicker.Value.Value;
		}

		private void TemplateCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				if (TemplateCountComboBox.SelectedIndex == 0)
				{
					templatePupilFinder.NumActiveTemplates = 0;
					NRecentTemplatesPicker.Visibility = Visibility.Hidden;
				}
				else
				{
					NRecentTemplatesPicker.Visibility = Visibility.Visible;
					templatePupilFinder.NumActiveTemplates = NRecentTemplatesPicker.Value.Value;
				}
			}
		}

		private void NextTemplateButton_Click(object sender, RoutedEventArgs e)
		{
			TemplatePreviewIndex++;
		}

		private void ResetButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder != null)
			{
				pupilFinder.ResetPupilLocations();
				UpdateDisplays();
				UpdateFramesProcessedPreviewImage();
			}
		}

		private void UpdateFramesProcessedPreviewImage()
		{
			FramesProcessedPreviewImage.Source = (pupilFinder == null) ? null : pupilFinder.GetFramesProcessedPreviewImage((int)PreviewImageGrid.ActualWidth);
		}
	}
}
