using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Eyetracking;
using SharpEyes.ViewModels;

namespace SharpEyes.Views
{
	public partial class TemplatePupilFinderConfigUserControl : UserControl
	{
		private TemplatePupilFinderConfigUserControlViewModel viewModel
		{
			get => (TemplatePupilFinderConfigUserControlViewModel)this.DataContext;
		}

		private PupilFindingUserControlViewModel PupilViewModel => viewModel?.Parent;
		public TemplatePupilFinder? TemplatePupilFinder 
		{
			get
			{
				if (PupilFinder is TemplatePupilFinder templateFinder)
					return templateFinder;
				return null;
			}
		}

		public PupilFinder? PupilFinder { get; set; } = null;

		public TemplatePupilFinderConfigUserControl()
		{
			InitializeComponent();
		}

		private void SetTemplatePreviewIndex(int index)
		{
			if (TemplatePupilFinder != null)
			{
				if (index < 0) index = 0;
				else if (index >= TemplatePupilFinder.NumTemplates) index = TemplatePupilFinder.NumTemplates - 1;

				viewModel.CurrentTemplateIndex = index;
				viewModel.TemplatePreviewImage = TemplatePupilFinder.GetTemplateImage(index);
			}
		}

		private void SetAntiTemplatePreviewIndex(int index)
		{
			if (TemplatePupilFinder != null)
			{
				if (index < 0) index = 0;
				else if (index >= TemplatePupilFinder.NumAntiTemplates) index = TemplatePupilFinder.NumAntiTemplates - 1;

				viewModel.CurrentAntiTemplateIndex = index;
				viewModel.AntiTemplatePreviewImage = TemplatePupilFinder.GetAntiTemplateImage(index);
			}
		}

		private void PreviousTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			SetTemplatePreviewIndex(viewModel.CurrentTemplateIndex - 1);
		}

		private void NextTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			SetTemplatePreviewIndex(viewModel.CurrentTemplateIndex + 1);
		}

		private void AddNewTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			if (TemplatePupilFinder != null)
			{
				int top = (int)(PupilViewModel.PupilY - PupilViewModel.PupilRadius * 1.5);
				int bottom = (int)(PupilViewModel.PupilY + PupilViewModel.PupilRadius * 1.5 + 2);
				int left = (int)(PupilViewModel.PupilX - PupilViewModel.PupilRadius * 1.5);
				int right = (int)(PupilViewModel.PupilX + PupilViewModel.PupilRadius * 1.5 + 2);
				TemplatePupilFinder.AddImageSegmentAsTemplate(top, bottom, left, right, PupilViewModel.PupilRadius);
				SetTemplatePreviewIndex(TemplatePupilFinder.NumTemplates);

				/* TODO: enabling buttons
				if (!saveTemplatesMenuItem.IsEnabled)
				{
					saveTemplatesMenuItem.IsEnabled = true;
				*/
				ResetTemplatesButton.IsEnabled = true;
				

				if (TemplatePupilFinder.NumTemplates > 1)
					RemoveTemplateButton.IsEnabled = true;
			}
		}

		private void RemoveTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			if (TemplatePupilFinder != null)
			{
				TemplatePupilFinder.RemoveTemplate(viewModel.CurrentTemplateIndex);
				SetTemplatePreviewIndex(viewModel.CurrentTemplateIndex - 1);
				if (TemplatePupilFinder.NumTemplates < 2)
					RemoveTemplateButton.IsEnabled = false;
			}
		}

		private void ResetTemplatesButton_OnClick(object? sender, RoutedEventArgs e)
		{
			if (TemplatePupilFinder != null)
			{
				TemplatePupilFinder.MakeTemplates();
				SetTemplatePreviewIndex(0);
				ResetTemplatesButton.IsEnabled = false;
				RemoveTemplateButton.IsEnabled = false;
			}
		}

		private void PreviousAntiTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			SetAntiTemplatePreviewIndex(viewModel.CurrentAntiTemplateIndex - 1);
		}

		private void NextAntiTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			SetAntiTemplatePreviewIndex(viewModel.CurrentAntiTemplateIndex + 1);
		}

		private void AddNewAntiTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			if (TemplatePupilFinder != null)
			{
				int top = (int)(PupilViewModel.PupilY - PupilViewModel.PupilRadius * 1.5);
				int bottom = (int)(PupilViewModel.PupilY + PupilViewModel.PupilRadius * 1.5 + 2);
				int left = (int)(PupilViewModel.PupilX - PupilViewModel.PupilRadius * 1.5);
				int right = (int)(PupilViewModel.PupilX + PupilViewModel.PupilRadius * 1.5 + 2);
				TemplatePupilFinder.AddImageSegmentAsAntiTemplate(top, bottom, left, right);
				SetAntiTemplatePreviewIndex(TemplatePupilFinder.NumAntiTemplates);

				/* TODO: enabling buttons
				if (!saveTemplatesMenuItem.IsEnabled)
				{
					saveTemplatesMenuItem.IsEnabled = true;
				*/
				ResetTemplatesButton.IsEnabled = true;


				if (TemplatePupilFinder.NumAntiTemplates > 0)
					RemoveAntiTemplateButton.IsEnabled = true;
			}
		}

		private void RemoveAntiTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			if (TemplatePupilFinder != null)
			{
				TemplatePupilFinder.RemoveAntiTemplate(viewModel.CurrentAntiTemplateIndex);
				SetTemplatePreviewIndex(viewModel.CurrentAntiTemplateIndex - 1);
				if (TemplatePupilFinder.NumAntiTemplates < 1)
					RemoveAntiTemplateButton.IsEnabled = false;
			}
		}

		private void TemplatePreviewImage_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			SetTemplatePreviewIndex(viewModel.CurrentTemplateIndex + (int)e.Delta.Y);
		}

		private void AntiTemplatePreviewImage_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			SetAntiTemplatePreviewIndex(viewModel.CurrentAntiTemplateIndex + (int)e.Delta.Y);
		}
	}
}
