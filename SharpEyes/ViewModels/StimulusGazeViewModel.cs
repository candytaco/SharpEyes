﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Eyetracking;
using NumSharp;
using ReactiveUI;
using SharpEyes.Models;
using Num = NumSharp.np;

namespace SharpEyes.ViewModels
{
	public class StimulusGazeViewModel : ViewModelBase
	{
		// == Commands ==
		public ReactiveCommand<Unit, Unit> LoadVideoCommand { get; set; }
		public ReactiveCommand<Unit, Unit>? PlayPauseCommand { get; set; } = null;
		public ReactiveCommand<Unit, Unit>? PreviousFrameCommand { get; set; } = null;
		public ReactiveCommand<Unit, Unit>? NextFrameCommand { get; set; } = null;
		public ReactiveCommand<Unit, Unit> LoadGazeCommand { get; set; }
		public ReactiveCommand<Unit, Unit>? SaveGazeCommand { get; set; } = null;
		public ReactiveCommand<Unit, Unit> SetCurrentAsDataStartCommand { get; set; }
		public ReactiveCommand<Unit, Unit>? FindDataStartCommand { get; set; } = null;

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
		
		// == video stuff ==
		private VideoReader? videoReader = null;
		private DispatcherTimer videoPlaybackTimer;

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
		private NDArray? gazeLocations = null;

		private bool _isGazeLoaded = false;
		public bool IsGazeLoaded
		{
			get => _isGazeLoaded;
			set => this.RaiseAndSetIfChanged(ref _isGazeLoaded, value);
		}
		private int? dataStartFrame = null;

		private int? dataEndFrame
		{
			get
			{
				if ((object)gazeLocations == null || dataStartFrame == null)
					return null;
				return DataIndexToVideoTime(gazeLocations.Shape[0]);
			}
		}

		private int? dataFrame // used to index into the gaze matrix. Updated by UpdateDisplay
		{
			get
			{
				if (dataStartFrame == null) return null;
				return VideoTimeToDataIndex(CurrentVideoFrame);
			}
		}

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

		private ObservableCollection<VideoKeyFrame> _videoKeyFrames = new ObservableCollection<VideoKeyFrame>(new List<VideoKeyFrame>());
		public ObservableCollection<VideoKeyFrame> VideoKeyFrames
		{
			get => _videoKeyFrames;
			set => this.RaiseAndSetIfChanged(ref _videoKeyFrames, value);
		}

		// set some default keyframes when the data start is set?
		private bool _setDefaultKeyFrames = true;
		public bool SetDefaultKeyFrames
		{
			get => _setDefaultKeyFrames;
			set => this.RaiseAndSetIfChanged(ref _setDefaultKeyFrames, value);
		}

		private string gazeFileName = null;

		private string defaultSaveName => gazeFileName == null
			? "gaze locations"
			: System.IO.Path.GetFileNameWithoutExtension(gazeFileName) + " corrected.npy";

		public StimulusGazeViewModel()
		{
			LoadVideoCommand = ReactiveCommand.Create(LoadVideo);
			PlayPauseCommand = ReactiveCommand.Create(PlayPause);
			LoadGazeCommand = ReactiveCommand.Create(LoadGaze);
			SaveGazeCommand = ReactiveCommand.Create(SaveGaze);
			SetCurrentAsDataStartCommand = ReactiveCommand.Create(SetCurrentAsDataStart);
			videoPlaybackTimer = new DispatcherTimer();
			videoPlaybackTimer.Tick += this.VideoTimerTick;

			PreviousFrameCommand = ReactiveCommand.Create(() => { ChangeFrame(-1); });
			NextFrameCommand = ReactiveCommand.Create(() => { ChangeFrame(1); });
		}

		public async void LoadVideo()
		{
			// reset gaze stuff
			dataStartFrame = null;
			IsGazeLoaded = false;
			gazeLocations = null;
			VideoKeyFrames.Clear();
			GazeX = 0;
			GazeY = 0;

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

			videoReader = new VideoReader(fileName[0]);
			videoReader.ReadFrame();
			VideoFrame = videoReader.GetFrameForDisplay();
			videoPlaybackTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / (double)videoReader.fps);
			TotalVideoFrames = videoReader.frameCount;
			SaveGazeCommand = null;
		}

		public void PlayPause()
		{
			if (IsVideoPlaying)
				videoPlaybackTimer.Stop();
			else
				videoPlaybackTimer.Start();
			IsVideoPlaying = !IsVideoPlaying;
		}

		public void ChangeFrame(int delta)
		{
			if (videoReader != null)
				ShowFrame(videoReader.CurrentFrameNumber + delta);
		}

		/// <summary>
		/// Called by the dispatcher timer to play the video. this is called once every frame to read
		/// in a video frame and update the display
		/// </summary>
		public void VideoTimerTick(object? sender, EventArgs e)
		{
			if (videoReader.CurrentFrameNumber >= videoReader.frameCount - 1)
				PlayPause();
			videoReader.ReadFrame();
			UpdateDisplay();
		}

		public void ShowFrame()
		{
			ShowFrame(CurrentVideoFrame);
		}

		public void ShowFrame(int frame)
		{
			videoReader.CurrentFrameNumber = frame;
			UpdateDisplay();
		}

		/// <summary>
		/// Given a video frame index, gets the first corresponding index in the gaze locations
		/// </summary>
		/// <param name="videoFrame"></param>
		/// <returns></returns>
		private int VideoTimeToDataIndex(int videoFrame)
		{
			int videoFramesElapsed = videoFrame - dataStartFrame.Value;
			if (videoFramesElapsed < 0)
				return 0;
			double videoElapsedTime = (double)videoFramesElapsed / videoReader.fps;
			return (int)(videoElapsedTime * EyetrackingFPS);
		}

		/// <summary>
		/// For a given index in the data, get the corresponding video frame
		/// </summary>
		/// <param name="dataIndex">index in eyetracking data</param>
		/// <returns>video frame number</returns>
		private int DataIndexToVideoTime(int dataIndex)
		{
			double dataElapsedTime = (double)dataIndex / EyetrackingFPS; // in seconds
			int dataElapsedFrames = (int)Math.Round(dataElapsedTime * videoReader.fps);
			return dataStartFrame.Value + dataElapsedFrames;
		}

		public void UpdateDisplay()
		{
			VideoFrame = videoReader.GetFrameForDisplay();
			CurrentVideoFrame = videoReader.CurrentFrameNumber;
			// TODO: set gaze circle location
			if (dataStartFrame != null)
			{
				GazeX = gazeLocations[dataFrame, 0];
				GazeY = gazeLocations[dataFrame, 1];
			}
		}

		// after gaze is manually edited, updates it.
		public void UpdateGaze()
		{
			double deltaX = GazeX - gazeLocations[dataFrame, 0];
			double deltaY = GazeY - gazeLocations[dataFrame, 1];

			AddKeyFrame();

			// if there is a keyframe before, ramp the delta from the previous keyframe 
			// to this new keyframe
			if (PreviousDataKeyFrame.HasValue)
			{
				int numFrames = dataFrame.Value - PreviousDataKeyFrame.Value;
				double multiplier;
				for (int i = PreviousDataKeyFrame.Value; i < dataFrame; i++)
				{
					multiplier = (double)(i - PreviousDataKeyFrame.Value) / numFrames;
					gazeLocations[i, 0] += multiplier * deltaX;
					gazeLocations[i, 1] += multiplier * deltaY;
				}
			}
			// update all following data frames with this delta
			gazeLocations[new Slice(dataFrame.Value, null), 0] += deltaX;
			gazeLocations[new Slice(dataFrame.Value, null), 1] += deltaY;
		}

		public void AddKeyFrame()
		{
			AddKeyFrame(CurrentVideoFrame);
		}

		public void AddKeyFrame(int frame)
		{
			if (dataStartFrame.HasValue && (frame >= dataStartFrame.Value) &&
			    (frame <= dataEndFrame))
			{
				int index = VideoTimeToDataIndex(frame);
				for (int i = 0; i < VideoKeyFrames.Count; i++)
				{
					// if this frame already exists as a keyframe, remove it
					if (VideoKeyFrames[i].DataIndex == index)
					{
						VideoKeyFrames.RemoveAt(i);
						break;
					}
				}

				VideoKeyFrames.Add(new VideoKeyFrame(frame, index, videoReader.FramesToTimecode(frame),
															gazeLocations[index, 0], gazeLocations[index, 1]));
				VideoKeyFrames = new ObservableCollection<VideoKeyFrame>(VideoKeyFrames.OrderBy((keyframe) => keyframe.VideoFrame));
			}
		}

		private int? PreviousVideoKeyFrame
		{
			get
			{
				for (int i = VideoKeyFrames.Count - 1; i > -1; i--)
				{
					if (VideoKeyFrames[i] < CurrentVideoFrame)
						return VideoKeyFrames[i].VideoFrame;
				}
				return null;
			}
		}

		private int? PreviousDataKeyFrame 
		{
			get
			{
				if (PreviousVideoKeyFrame.HasValue)
					return VideoTimeToDataIndex(PreviousVideoKeyFrame.Value);
				return null;
			}
		}


		private int? NextVideoKeyFrame
		{
			get
			{
				for (int i = 0; i < VideoKeyFrames.Count; i++)
				{
					if (VideoKeyFrames[i] > CurrentVideoFrame)
						return VideoKeyFrames[i].VideoFrame;
				}
				return null;
			}
		}

		private int? NextDataKeyFrame
		{
			get
			{
				if (NextVideoKeyFrame.HasValue)
					return VideoTimeToDataIndex(NextVideoKeyFrame.Value);
				return null;
			}
		}

		public async void LoadGaze()
		{
			OpenFileDialog openFileDialog = new OpenFileDialog()
			{
				Title = "Load gaze locations"
			};
			openFileDialog.Filters.Add(new FileDialogFilter()
			{
				Name = "Numpy file",
				Extensions = { "npy" }
			});
			openFileDialog.Filters.Add(new FileDialogFilter()
			{
				Name = "Comma-separated values",
				Extensions = { "csv" }
			});
			string[] fileName = await openFileDialog.ShowAsync(MainWindow);

			if (fileName == null || fileName.Length == 0)
				return;
			if (System.IO.Path.GetExtension(fileName[0]) == ".npy")
				gazeLocations = Num.load(fileName[0]);
			else // parse a csv file
			{
				using StreamReader csvFile = new StreamReader(fileName[0]);
				string line = csvFile.ReadLine();
				List<double[]> values = new List<double[]>();
				bool isFirstLine = true;
				while (line != null)
				{
					try
					{
						string[] tokens = line.Split(',');
						double x = Double.Parse(tokens[0]);
						double y = Double.Parse(tokens[1]);
						values.Add(new double[]{x, y});
						isFirstLine = false;
						line = csvFile.ReadLine();
					}
					catch (Exception e)
					{	// so if the first line is a header, we throw it away,
						// but if there's a parsing error anywhere else we raise it
						if (!isFirstLine)
							throw;
					}
				}

				gazeLocations = new NDArray(NPTypeCode.Double, Shape.Matrix(values.Count, 2));
				for (int i = 0; i < values.Count; i++)
				{
					gazeLocations[i, 0] = values[i][0];
					gazeLocations[i, 1] = values[i][1];
				}
			}
			IsGazeLoaded = true;
			gazeFileName = fileName[0];
		}

		public async void SaveGaze()
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog()
			{
				Title = "Load gaze locations",
				InitialFileName = defaultSaveName
			};
			saveFileDialog.Filters.Add(new FileDialogFilter()
			{
				Name = "Numpy file",
				Extensions = { "npy" },
			});
			string? fileName = await saveFileDialog.ShowAsync(MainWindow);

			if (fileName != null)
			{
				Num.save(fileName, gazeLocations);
			}
		}

		public void SetCurrentAsDataStart()
		{
			dataStartFrame = videoReader.CurrentFrameNumber;

			VideoKeyFrames.Clear();
			if (SetDefaultKeyFrames)
			{
				AddKeyFrame(dataStartFrame.Value);	// start of data
				AddKeyFrame(dataStartFrame.Value + videoReader.fps * 37 * 2);	// end of eyetracking calibration in driving
				if (dataEndFrame >= videoReader.frameCount)
					AddKeyFrame(videoReader.frameCount - 1);
				else AddKeyFrame(dataEndFrame.Value - 1);
			}
		}
	}
}
