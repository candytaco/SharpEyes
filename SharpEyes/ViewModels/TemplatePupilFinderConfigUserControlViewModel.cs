using Avalonia.Media.Imaging;
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
		public ReactiveCommand<int, Unit>? ChangeTemplatePreviewIndexCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? ResetTemplatesCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? AddCurrentAsAntiTemplateCommand { get; } = null;
		public ReactiveCommand<Unit, Unit>? RemoveCurrentAntiTemplateCommand { get; } = null;
		public ReactiveCommand<int, Unit>? ChangeAntiTemplatePreviewIndexCommand { get; } = null;

		// templates section
		public bool AutoAddNewTemplate { get; set; } = true;
		private int _currentTemplateIndex = 0;
		public int CurrentTemplateIndex
		{
			get => _currentTemplateIndex;
			set
			{
				this.RaiseAndSetIfChanged(ref _currentTemplateIndex, value);
				this.RaisePropertyChanged("TemplateIndexText");
			}
		}
		public int TotalTemplateCount { get; set; } = 0;
		public string TemplateIndexText => String.Format("{0}/{1}", CurrentTemplateIndex, TotalTemplateCount);
		private Bitmap? templatePreviewImage = null;
		public Bitmap? TemplatePreviewImage
		{
			get => templatePreviewImage;
			set => this.RaiseAndSetIfChanged(ref templatePreviewImage, value);
		}

		// anti-templates section
		public int CurrentAntiTemplateIndex { get; set; } = 0;
		public int TotalAntiTemplateCount { get; private set; } = 0;
		public string AntiTemplateIndexText => String.Format("{0}/{1}", CurrentAntiTemplateIndex, TotalAntiTemplateCount);
		private Bitmap? antiTemplatePreviewImage = null;
		public Bitmap? AntiTemplatePreviewImage
		{
			get => antiTemplatePreviewImage;
			set => this.RaiseAndSetIfChanged(ref antiTemplatePreviewImage, value);
		}

		// match options
		public int SelectedMetricIndex { get; set; } = 3;
		public bool UseAllTemplates { get; set; } = true;
		public int NumTemplatesToUse { get; set; } = 128;
		public bool UseEveryTemplate { get; set; } = true;
		public int FractionOfTemplatesToUse { get; set; } = 75;

		// view model hierarchy
		private PupilFindingUserControlViewModel? parent = null;

		public TemplatePupilFinderConfigUserControlViewModel()
			:this(null)
		{
		}

		public TemplatePupilFinderConfigUserControlViewModel(PupilFindingUserControlViewModel parent)
		{
			AddCurrentAsTemplateCommand = ReactiveCommand.Create(AddCurrentAsTemplate);
			RemoveCurrentTemplateCommand = ReactiveCommand.Create(RemoveCurrentTemplate);
			ChangeTemplatePreviewIndexCommand = ReactiveCommand.Create<int>(ChangeTemplatePreviewIndex);
			ResetTemplatesCommand = ReactiveCommand.Create(ResetTemplates);
			AddCurrentAsAntiTemplateCommand = ReactiveCommand.Create(AddCurrentAsAntiTemplate);
			RemoveCurrentAntiTemplateCommand = ReactiveCommand.Create(RemoveCurrentAntiTemplate);
			ChangeAntiTemplatePreviewIndexCommand = ReactiveCommand.Create<int>(ChangeAntiTemplatePreviewIndex);
			this.parent = parent;
		}

		// Command implementations

		public void AddCurrentAsTemplate()
		{

		}

		public void RemoveCurrentTemplate()
		{

		}

		/// <summary>
		/// Changes the template displayed
		/// </summary>
		/// <param name="delta">-1 to show previous, +1 to show next</param>
		public void ChangeTemplatePreviewIndex(int delta)
		{
			return;
		}

		public void ResetTemplates()
		{

		}

		public void AddCurrentAsAntiTemplate()
		{

		}

		public void RemoveCurrentAntiTemplate()
		{

		}

		public void ChangeAntiTemplatePreviewIndex(int delta)
		{

		}
	}
}
