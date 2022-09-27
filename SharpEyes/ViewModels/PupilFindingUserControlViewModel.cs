using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using ReactiveUI;

namespace SharpEyes.ViewModels
{
	public enum EditingState
	{
		None,
		DrawWindow,
		MovePupil
	}

	public class PupilFindingUserControlViewModel : ViewModelBase
	{
		// TODO: setters and raising Notify property changed

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
		public double CurrentVideoPercentage => 0.0;

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
			}
		}
		public double PupilY
		{
			get => pupilY;
			set
			{
				this.RaiseAndSetIfChanged(ref pupilY, value);
				this.RaisePropertyChanged("PupilCircleTop");
			}
		}
		public double PupilRadius => PupilDiameter / 2;
		public double PupilDiameter => 64;
		public double PupilCircleLeft => pupilX - PupilRadius;
		public double PupilCircleTop => pupilY - PupilRadius;
		public double PupilConfidence => 0;
		public double PupilWindowLeft => 0;
		public double PupilWindowTop => 0;
		public double PupilWindowWidth => 0;
		public double PupilWindowHeight => 0;

		// pupil finding info
		public string PupilXText => String.Format("X: {0}", PupilX);
		public string PupilYText => String.Format("Y: {0}", PupilY);
		public string PupilRadiusText => String.Format("Radius: {0:F1}", PupilRadius);
		public string PupilConfidenceText => String.Format("X: {0:F4}", PupilConfidence);
		public bool ProcessAllFrames => false;
		public int FramesToProcess => 120;
		public int MinPupilDiameter => 10;
		public int MaxPupilDiameter => 75;
		public int PupilFinderTypeIndex => 0;

		// timestamps
		public bool AutoReadTimestamps => true;
		public bool IsTimestampsRead { get; private set; } = false;

		// image pre-filtering
		public bool UseBilateralBlur => false;
		public int BilateralBlurSize => 1;
		public int BilateralBlurSigmaColor => 30;
		public int BilateralBlueSigmaSpace => 10;
		public bool UseMedianBlur => false;
		public int MedianBlurSize => 1;

		// manual adjustment
		public bool AutoEnterPupilEditmode => true;
		public bool UseLinearDecay => true;
		public bool UseExponentialDecay => false;
		public bool UseNoDecay => false;
		public int LinearDecayFrames => 180;
		public int ExponentialDecayTimeConstant => 30;

		// children view models
		public TemplatePupilFinderConfigUserControlViewModel templatePupilFinderConfigUserControlViewModel { get; }

		public PupilFindingUserControlViewModel()
		{
			templatePupilFinderConfigUserControlViewModel = new TemplatePupilFinderConfigUserControlViewModel(this);
		}
	}
}
