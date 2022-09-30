using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ReactiveUI;

namespace SharpEyes.ViewModels
{
	public enum EditingState
	{
		None,
		DrawWindow,
		MovePupil
	}

	// note the ordering here should match the combobox items
	public enum PupilFinderType
	{
		Template,
		HoughCircles,
	}

	public class PupilFindingUserControlViewModel : ViewModelBase
	{
		// Commands
		public ReactiveCommand<Unit, Unit>? LoadVideoCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? FindPupilsCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? PlayPauseCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? PreviousFrameCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? NextFrameCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? VideoSliderUpdatedCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? ReadTimestampsCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? LoadTimestampsCommand { get; } = null;

		// == UI elements ==
		private EditingState _editingState = EditingState.None;
		public EditingState EditingState
		{
			get => _editingState;
			set
			{
				_editingState = value;
				// apparently this is not the ReactiveUI way to do things, but their documentation is nonexistent.
				// There is literally no page describing the syntax for the WhenAny method, only a poorly written set of
				// examples. Remember kids, examples are not documentation.
				// Imo this is also clearer.
				this.RaisePropertyChanged("IsMovingPupil");
				this.RaisePropertyChanged("IsDrawingWindow");
			}
		}
		public bool IsDrawingWindow
		{
			get => _editingState == EditingState.DrawWindow;
			set
			{
				this.RaiseAndSetIfChanged(ref _editingState, value ? EditingState.DrawWindow : EditingState.None);
				this.RaisePropertyChanged("IsMovingPupil");
			}
		}
		public bool IsMovingPupil
		{
			get => _editingState == EditingState.MovePupil;
			set
			{
				this.RaiseAndSetIfChanged(ref _editingState, value ? EditingState.MovePupil : EditingState.None);
				this.RaisePropertyChanged("IsDrawingWindow");
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

		// video playback
		private int _videoWidth = 400;
		public int VideoWidth
		{
			get => _videoWidth;
			set => this.RaiseAndSetIfChanged(ref _videoWidth, value);
		}

		private int _videoHeight = 300;
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
		public bool IsVideoPlaying { get; private set; } = false;
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

		private Bitmap _framesProcessedPreviewImage = null;
		public Bitmap FramesProcessedPreviewImage
		{
			get => _framesProcessedPreviewImage;
			set => this.RaiseAndSetIfChanged(ref _framesProcessedPreviewImage, value);
		}

		// pupil overlay info
		private double _pupilX = 0;
		private double _pupilY = 0;
		public double PupilX
		{
			get => _pupilX;
			set
			{
				this.RaiseAndSetIfChanged(ref _pupilX, value);
				this.RaisePropertyChanged("PupilCircleLeft");
				this.RaisePropertyChanged("PupilXText");
			}
		}
		public double PupilY
		{
			get => _pupilY;
			set
			{
				this.RaiseAndSetIfChanged(ref _pupilY, value);
				this.RaisePropertyChanged("PupilCircleTop");
				this.RaisePropertyChanged("PupilYText");
			}
		}
		public double PupilCircleLeft => _pupilX - PupilRadius;
		public double PupilCircleTop => _pupilY - PupilRadius;
		private double _pupilDiameter = 64;
		public double PupilRadius => _pupilDiameter / 2;
		public double PupilDiameter
		{
			get => _pupilDiameter;
			set
			{
				// pupil diameter is bounded
				_pupilDiameter = value;
				if (_pupilDiameter > MaxPupilDiameter)
					_pupilDiameter = MaxPupilDiameter;
				else if (_pupilDiameter < MinPupilDiameter)
					_pupilDiameter = MinPupilDiameter;

				this.RaisePropertyChanged("PupilDiameter");
				this.RaisePropertyChanged("PupilRadius");
				this.RaisePropertyChanged("PupilRadiusText");
				// because the circle is set by its left/top corner
				this.RaisePropertyChanged("PupilCircleLeft");
				this.RaisePropertyChanged("PupilCircleTop");
			}
		}
		private double _pupilConfidence = Double.NaN;
		public double PupilConfidence
		{
			get => _pupilConfidence;
			set
			{
				this.RaiseAndSetIfChanged(ref _pupilConfidence, value);
				this.RaisePropertyChanged("PupilConfidenceText");
			}
		}
		private int _pupilWindowLeft = 0;
		public int PupilWindowLeft
		{
			get => _pupilWindowLeft;
			set => this.RaiseAndSetIfChanged(ref _pupilWindowLeft, value);
		}
		private int _pupilWindowTop = 0;
		public int PupilWindowTop
		{
			get => _pupilWindowTop;
			set => this.RaiseAndSetIfChanged(ref _pupilWindowTop, value);
		}
		private int _pupilWindowWidth = 0;
		public int PupilWindowWidth
		{
			get => _pupilWindowWidth;
			set => this.RaiseAndSetIfChanged(ref _pupilWindowWidth, value);
		}
		private int _pupilWindowHeight = 0;
		public int PupilWindowHeight
		{
			get => _pupilWindowHeight;
			set => this.RaiseAndSetIfChanged(ref _pupilWindowHeight, value);
		}

		// pupil finding info
		public string PupilXText => String.Format("X: {0:F1}", PupilX);
		public string PupilYText => String.Format("Y: {0:F1}", PupilY);
		public string PupilRadiusText => String.Format("Radius: {0:F1}", PupilRadius);
		public string PupilConfidenceText => String.Format("Confidence: {0:F4}", PupilConfidence);
		public bool ProcessAllFrames { get; set; } = false;
		public int FramesToProcess { get; set; } = 120;
		public int MinPupilDiameter { get; set; } = 10;
		public int MaxPupilDiameter { get; set; } = 75;
		public int PupilFinderTypeIndex { get; set; } = 0;
		public PupilFinderType PupilFinderType => (PupilFinderType)PupilFinderTypeIndex;

		private bool _isFindingPupils = false;

		public bool IsFindingPupils
		{
			get => _isFindingPupils;
			set
			{
				this.RaiseAndSetIfChanged(ref _isFindingPupils, value);
				this.RaisePropertyChanged("PupilFindingButtonText");
			}
		}
		public string PupilFindingButtonText => IsFindingPupils ? "Cancel" : "Find Pupils";

		// confidence options - here because they can apply to all pupil finders
		public bool StopOnLowConfidence { get; set; } = true;
		public double LowConfidenceThreshold { get; set; } = 0.985;
		public int LowConfidenceFrameCountThreshold { get; set; } = 12;
		public bool EnableBlinkRejection { get; set; } = true;
		public double BlinkRejectionBlinkSigma { get; set; } = 2.0;
		public double BlinkRejectionPupilSigma { get; set; } = 2.0;

		// timestamps
		private bool _showTimestampParsing = false;

		public bool ShowTimestampParsing
		{
			get => _showTimestampParsing;
			set => this.RaiseAndSetIfChanged(ref _showTimestampParsing, value);
		}
		public bool AutoReadTimestamps => true;
		public bool IsTimestampsRead { get; private set; } = false;

		// image pre-filtering
		public bool UseBilateralBlur { get; set; } = false;
		public int BilateralBlurSize { get; set; } = 1;
		public int BilateralBlurSigmaColor { get; set; } = 30;
		public int BilateralBlurSigmaSpace { get; set; } = 10;
		public bool UseMedianBlur { get; set; } = false;
		public int MedianBlurSize { get; set; } = 1;

		// manual adjustment
		public bool AutoEnterPupilEditMode { get; set; } = true;
		public bool UseLinearDecay { get; set; } = true;
		public bool UseExponentialDecay { get; set; } = false;
		public bool UseNoDecay { get; set; } = false;
		public int LinearDecayFrames { get; set; } = 180;
		public int ExponentialDecayTimeConstant { get; set; } = 30;

		// children view models
		public TemplatePupilFinderConfigUserControlViewModel TemplatePupilFinderConfigUserControlViewModel { get; }

		public PupilFindingUserControlViewModel()
		{
			TemplatePupilFinderConfigUserControlViewModel = new TemplatePupilFinderConfigUserControlViewModel(this);
			LoadVideoCommand = ReactiveCommand.Create(LoadVideo);
		}

		// command backings
		public void LoadVideo()
		{

		}

		public void FindPupils()
		{

		}

		public void PlayPause()
		{

		}

		public void PreviousFrame()
		{

		}

		public void NextFrame()
		{

		}

	}
}
