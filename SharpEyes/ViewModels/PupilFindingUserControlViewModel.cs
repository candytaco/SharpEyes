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
		private EditingState editingState = EditingState.None;
		public EditingState EditingState
		{
			get => editingState;
			set
			{
				editingState = value;
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
			get => editingState == EditingState.DrawWindow;
			set
			{
				this.RaiseAndSetIfChanged(ref editingState, value ? EditingState.DrawWindow : EditingState.None);
				this.RaisePropertyChanged("IsMovingPupil");
			}
		}
		public bool IsMovingPupil
		{
			get => editingState == EditingState.MovePupil;
			set
			{
				this.RaiseAndSetIfChanged(ref editingState, value ? EditingState.MovePupil : EditingState.None);
				this.RaisePropertyChanged("IsDrawingWindow");
			}
		}

		// progress bar
		public string StatusText => "Idle";
		public bool IsProgressBarVisible => false;
		public bool IsProgressBarIndeterminate => false;
		public double ProgressBarValue => 0;

		// video playback
		public string CurrentVideoTime => "0:00:00;00";
		public string TotalVideoTime => "0:00:00;00";
		public string PlayPauseButtonText => IsVideoPlaying ? "Pause" : "Play";
		public bool IsVideoPlaying { get; private set; } = false;
		public double CurrentVideoPercentage { get; set; } = 0.0;
		public Bitmap? VideoFrame => null;

		// pupil overlay info
		private double pupilX = 0;
		private double pupilY = 0;
		public double PupilX
		{
			get => pupilX;
			set
			{
				this.RaiseAndSetIfChanged(ref pupilX, value);
				this.RaisePropertyChanged("PupilCircleLeft");
				this.RaisePropertyChanged("PupilXText");
			}
		}
		public double PupilY
		{
			get => pupilY;
			set
			{
				this.RaiseAndSetIfChanged(ref pupilY, value);
				this.RaisePropertyChanged("PupilCircleTop");
				this.RaisePropertyChanged("PupilYText");
			}
		}
		public double PupilCircleLeft => pupilX - PupilRadius;
		public double PupilCircleTop => pupilY - PupilRadius;
		private double pupilDiameter = 64;
		public double PupilRadius => pupilDiameter / 2;
		public double PupilDiameter
		{
			get => pupilDiameter;
			set
			{
				// pupil diameter is bounded
				pupilDiameter = value;
				if (pupilDiameter > MaxPupilDiameter)
					pupilDiameter = MaxPupilDiameter;
				else if (pupilDiameter < MinPupilDiameter)
					pupilDiameter = MinPupilDiameter;

				this.RaisePropertyChanged("PupilDiameter");
				this.RaisePropertyChanged("PupilRadius");
				this.RaisePropertyChanged("PupilRadiusText");
				// because the circle is set by its left/top corner
				this.RaisePropertyChanged("PupilCircleLeft");
				this.RaisePropertyChanged("PupilCircleTop");
			}
		}
		private double pupilConfidence = Double.NaN;
		public double PupilConfidence
		{
			get => pupilConfidence;
			set
			{
				this.RaiseAndSetIfChanged(ref pupilConfidence, value);
				this.RaisePropertyChanged("PupilConfidenceText");
			}
		}
		private double pupilWindowLeft = 0;
		public double PupilWindowLeft
		{
			get => pupilWindowLeft;
			set => this.RaiseAndSetIfChanged(ref pupilWindowLeft, value);
		}
		private double pupilWindowTop = 0;
		public double PupilWindowTop
		{
			get => pupilWindowTop;
			set => this.RaiseAndSetIfChanged(ref pupilWindowTop, value);
		}
		private double pupilWindowWidth = 0;
		public double PupilWindowWidth
		{
			get => pupilWindowWidth;
			set => this.RaiseAndSetIfChanged(ref pupilWindowWidth, value);
		}
		private double pupilWindowHeight = 0;
		public double PupilWindowHeight
		{
			get => pupilWindowHeight;
			set => this.RaiseAndSetIfChanged(ref pupilWindowHeight, value);
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

		// timestamps
		public bool AutoReadTimestamps => true;
		public bool IsTimestampsRead { get; private set; } = false;

		// image pre-filtering
		public bool UseBilateralBlur { get; set; } = false;
		public int BilateralBlurSize { get; set; } = 1;
		public int BilateralBlurSigmaColor { get; set; } = 30;
		public int BilateralBlueSigmaSpace { get; set; } = 10;
		public bool UseMedianBlur { get; set; } = false;
		public int MedianBlurSize { get; set; } = 1;

		// manual adjustment
		public bool AutoEnterPupilEditmode { get; set; } = true;
		public bool UseLinearDecay { get; set; } = true;
		public bool UseExponentialDecay { get; set; } = false;
		public bool UseNoDecay { get; set; } = false;
		public int LinearDecayFrames { get; set; } = 180;
		public int ExponentialDecayTimeConstant { get; set; } = 30;

		// children view models
		public TemplatePupilFinderConfigUserControlViewModel templatePupilFinderConfigUserControlViewModel { get; }

		public PupilFindingUserControlViewModel()
		{
			templatePupilFinderConfigUserControlViewModel = new TemplatePupilFinderConfigUserControlViewModel(this);
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
