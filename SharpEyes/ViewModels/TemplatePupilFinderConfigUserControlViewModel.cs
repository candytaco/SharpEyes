using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;

namespace SharpEyes.ViewModels
{
	public class TemplatePupilFinderConfigUserControlViewModel : ViewModelBase
	{
		// Commands
		public ReactiveCommand<Unit, Unit>? AddCurrentAsTemplateCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? RemoveCurrentTemplateCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? PreviousTemplateCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? NextTemplateCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? ResetTemplatesCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? AddCurrentAsAntiTemplateCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? RemoveCurrentAntiTemplateCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? PreviousAntiTemplateCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? NextAntiTemplateCommand { get; } = null;

		// templates section
		public bool AutoAddNewTemplates => true;
		public int CurrentTemplateIndex => 0;
		public int TotalTemplateCount { get; private set; } = 0;
		public string TemplateIndexText => String.Format("{0}/{1}", CurrentTemplateIndex, TotalTemplateCount);

		// anti-templates section
		public int CurrentAntiTemplateIndex => 0;
		public int TotalAntiTemplateCount { get; private set; } = 0;
		public string AntiTemplateIndexText => String.Format("{0}/{1}", CurrentAntiTemplateIndex, TotalAntiTemplateCount);

		// match options
		public int SelectedMetricIndex => 3;
		public bool UseAllTemplates => true;
		public int NumTemplatesToUse => 128;
		public bool UseEveryTemplate => true;
		public int FractionOfTemplatesToUse => 75;

		// confidence options
		public bool StopOnLowConfidence => true;
		public double LowConfidenceThreshold => 0.985;
		public int LowConfidenceFrameCountThreshold => 12;
		public bool EnableBlinkRejection => true;
		public double BlinkRejectionBlinkSigma => 2.0;
		public double BlinkRejectionPupilSigma => 2.0;

		// view model hierarchey
		private PupilFindingUserControlViewModel? parent = null;

		public TemplatePupilFinderConfigUserControlViewModel()
		{
		}

		public TemplatePupilFinderConfigUserControlViewModel(PupilFindingUserControlViewModel parent)
		{
			this.parent = parent;
		}
	}
}
