using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Eyetracking;
using ReactiveUI;

namespace SharpEyes.ViewModels
{
	public class StimulusGazeViewModel : ViewModelBase
	{
		// == Commands ==
		public ReactiveCommand<Unit, Unit> LoadVideoCommand { get; set; }
		public ReactiveCommand<Unit, Unit>? PlayPauseCommand { get; set; } = null;

		// == window reference for showing dialogs
		public Window? MainWindow =>
			Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
				? desktop.MainWindow
				: null;

		// == UI elements ==
		private bool _isMovingGaze = false;
		public bool IsMovingGaze
		{
			get => _isMovingGaze;
			set
			{
				this.RaiseAndSetIfChanged(ref _isMovingGaze, value);
			}
		}

		// progress bar
		private string _statusText = "Idle";
		public string StatusText
		{
			get => _statusText;
			set => this.RaiseAndSetIfChanged(ref _statusText, value);
		}
		private bool _isProgressBarVisible = false;
		public bool IsProgressBarVisible
		{
			get => _isProgressBarVisible;
			set => this.RaiseAndSetIfChanged(ref _isProgressBarVisible, value);
		}
		private bool _isProgressBarIndeterminate = false;
		public bool IsProgressBarIndeterminate
		{
			get => _isProgressBarIndeterminate;
			set => this.RaiseAndSetIfChanged(ref _isProgressBarIndeterminate, value);
		}
		private double _progressBarValue = 0;
		public double ProgressBarValue
		{
			get => _progressBarValue;
			set => this.RaiseAndSetIfChanged(ref _progressBarValue, value);
		}
		

		private int _videoWidth = 1024;
		public int VideoWidth
		{
			get => _videoWidth;
			set => this.RaiseAndSetIfChanged(ref _videoWidth, value);
		}

		private int _videoHeight = 768;
		public int VideoHeight
		{
			get => _videoHeight;
			set => this.RaiseAndSetIfChanged(ref _videoHeight, value);
		}

		private string _currentVideoTime = "0:00:00;00";

		public string CurrentVideoTime
		{
			get => _currentVideoTime;
			set => this.RaiseAndSetIfChanged(ref _currentVideoTime, value);
		}
		private string _totalVideoTime = "0:00:00;00";

		public string TotalVideoTime
		{
			get => _totalVideoTime;
			set => this.RaiseAndSetIfChanged(ref _totalVideoTime, value);
		}
		public string PlayPauseButtonText => IsVideoPlaying ? "Pause" : "Play";
		private bool _isVideoPlaying = false;

		public bool IsVideoPlaying
		{
			get => _isVideoPlaying;
			set
			{
				this.RaiseAndSetIfChanged(ref _isVideoPlaying, value);
				this.RaisePropertyChanged("PlayPauseButtonText");
			}
		}
		private int _currentVideoFrame = 0;
		public int CurrentVideoFrame
		{
			get => _currentVideoFrame;
			set => this.RaiseAndSetIfChanged(ref _currentVideoFrame, value);
		}

		private int _totalVideoFrames = 0;
		public int TotalVideoFrames
		{
			get => _totalVideoFrames;
			set => this.RaiseAndSetIfChanged(ref _totalVideoFrames, value);
		}

		private Bitmap? _videoFrame = null;
		public Bitmap? VideoFrame
		{
			get => _videoFrame;
			set => this.RaiseAndSetIfChanged(ref _videoFrame, value);
		}

		// Gaze overlay info
		private double _gazeX = 0;
		private double _gazeY = 0;
		public double GazeX
		{
			get => _gazeX;
			set
			{
				this.RaiseAndSetIfChanged(ref _gazeX, value);
				this.RaisePropertyChanged("GazeCircleLeft");
				this.RaisePropertyChanged("GazeXText");
			}
		}
		public double GazeY
		{
			get => _gazeY;
			set
			{
				this.RaiseAndSetIfChanged(ref _gazeY, value);
				this.RaisePropertyChanged("GazeCircleTop");
				this.RaisePropertyChanged("GazeYText");
			}
		}
		public double GazeCircleLeft => _gazeX - GazeRadius;
		public double GazeCircleTop => _gazeY - GazeRadius;

		private double _gazeDiameter = 204;
		public double GazeRadius => _gazeDiameter / 2;
		public double GazeDiameter
		{
			get => _gazeDiameter;
			set
			{
				// gaze diameter is bounded
				_gazeDiameter = value;

				this.RaisePropertyChanged("GazeDiameter");
				this.RaisePropertyChanged("GazeRadius");
				this.RaisePropertyChanged("GazeRadiusText");
				// because the circle is set by its left/top corner
				this.RaisePropertyChanged("GazeCircleLeft");
				this.RaisePropertyChanged("GazeCircleTop");
			}
		}

		private double _gazeStrokeThickness = 4.0;
		public double GazeStrokeThickness
		{
			get => _gazeStrokeThickness;
			set => this.RaiseAndSetIfChanged(ref _gazeStrokeThickness, value);
		}

		private double _gazeStrokeOpacity = 0.75;
		public double GazeStrokeOpacity
		{
			get => _gazeStrokeOpacity;
			set => this.RaiseAndSetIfChanged(ref _gazeStrokeOpacity, value);
		}
		public Color GazeStrokeColor
		{
			get => GazeStrokeBrush.Color;
			set
			{
				GazeStrokeBrush.Color = value;
				this.RaisePropertyChanged("GazeStrokeBrush");
			}
		}

		public SolidColorBrush GazeStrokeBrush { get; set; } = new SolidColorBrush(Colors.LimeGreen);

		private int _eyetrackingFPS = 60;
		public int EyetrackingFPS
		{
			get => _eyetrackingFPS;
			set => this.RaiseAndSetIfChanged(ref _eyetrackingFPS, value);
		}

		private int _trailLength = 10;
		public int TrailLength
		{
			get => _trailLength;
			set => this.RaiseAndSetIfChanged(ref _trailLength, value);
		}

		private List<VideoKeyFrame> _videoKeyFrames = new List<VideoKeyFrame>();

		public List<VideoKeyFrame> VideoKeyFrames
		{
			get => _videoKeyFrames;
			set => this.RaiseAndSetIfChanged(ref _videoKeyFrames, value);
		}

		public StimulusGazeViewModel()
		{
			LoadVideoCommand = ReactiveCommand.Create(LoadVideo);
			PlayPauseCommand = ReactiveCommand.Create(PlayPause);
		}

		public async void LoadVideo()
		{
			OpenFileDialog openFileDialog = new OpenFileDialog()
			{
				Title = "Load stimulus video"
			};
			openFileDialog.Filters.Add(new FileDialogFilter()
			{
				Name = "Videos",
				Extensions = { "avi", "mkv", "mp4", "m4v" }
			});
			string[] fileName = await openFileDialog.ShowAsync(MainWindow);

			if (fileName == null || fileName.Length == 0)
				return;
			
		}

		public void PlayPause()
		{
		}
	}
}
