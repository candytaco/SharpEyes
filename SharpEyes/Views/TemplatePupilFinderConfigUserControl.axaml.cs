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
		

		public TemplatePupilFinderConfigUserControl()
		{
			InitializeComponent();
		}


		private void TemplatePreviewImage_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			viewModel.ChangeTemplatePreviewIndex((int)e.Delta.Y);
		}

		private void AntiTemplatePreviewImage_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			viewModel.ChangeAntiTemplatePreviewIndex((int)e.Delta.Y);
		}
	}
}
