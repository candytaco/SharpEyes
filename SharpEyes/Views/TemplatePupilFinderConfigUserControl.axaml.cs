using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Eyetracking;

namespace SharpEyes.Views
{
	public partial class TemplatePupilFinderConfigUserControl : UserControl
	{
		public TemplatePupilFinder? TemplatePupilFinder 
		{
			get
			{
				if (Parent is PupilFindingUserControl { pupilFinder: TemplatePupilFinder finder }) return finder;
				return null;
			}
		}

		public TemplatePupilFinderConfigUserControl()
		{
			InitializeComponent();
		}

		private void ChangeTemplatePreviewIndex(int delta)
		{
			if (delta == -1 || delta == 1)
			{

			}
		}

		private void PreviousTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void NextTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void AddNewTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void RemoveTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void ResetTemplatesButton_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void PreviousAntiTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void NextAntiTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void AddNewAntiTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void RemoveAntiTemplateButton_OnClick(object? sender, RoutedEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void TemplatePreviewImage_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private void AntiTemplatePreviewImage_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			throw new System.NotImplementedException();
		}
	}
}
