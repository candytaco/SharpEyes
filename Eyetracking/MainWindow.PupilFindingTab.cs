using System.Windows;
using System.Windows.Controls;

namespace Eyetracking
{
	public partial class MainWindow
	{
		private void FindPupilsButton_Click(object sender, RoutedEventArgs e)
		{
			if (FindPupilsAllFramesCheckBox.IsChecked.Value)
				FindPupils(pupilFinder.frameCount - pupilFinder.CurrentFrameNumber, sender);
			else
				FindPupils(FramesToProcessPicker.Value.Value, sender);
		}

		private void RadiusPickerValuesChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (pupilFinder != null)
			{
				pupilFinder.minRadius = MinRadiusPicker.Value.Value;
				pupilFinder.maxRadius = MaxRadiusPicker.Value.Value;
			}
		}

		private void UseImageAsTemplateButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				int top = (int)(PupilY - PupilRadius * 1.5);
				int bottom = (int)(PupilY + PupilRadius * 1.5 + 2);
				int left = (int)(PupilX - PupilRadius * 1.5);
				int right = (int)(PupilX + PupilRadius * 1.5 + 2);
				templatePupilFinder.AddImageSegmentAsTemplate(top, bottom, left, right, PupilRadius);
				TemplatePreviewIndex = templatePupilFinder.NumTemplates;
				if (!saveTemplatesMenuItem.IsEnabled)
				{
					saveTemplatesMenuItem.IsEnabled = true;
					ResetTemplatesButton.IsEnabled = true;
				}
				if (templatePupilFinder.NumTemplates > 1)
					DeleteTemplateButton.Visibility = Visibility.Visible;
			}
		}

		private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
		{
			((TemplatePupilFinder)pupilFinder).MakeTemplates();
			TemplatePreviewIndex = 0;
			saveTemplatesMenuItem.IsEnabled = false;
			ResetTemplatesButton.IsEnabled = false;
			DeleteTemplateButton.Visibility = Visibility.Hidden;
		}

		private void CancelPupilFindingButton_Click(object sender, RoutedEventArgs e)
		{
			pupilFinder.CancelPupilFinding();
		}

		private void PreviousTemplateButton_Click(object sender, RoutedEventArgs e)
		{
			TemplatePreviewIndex--;
		}

		private void FindPupilsAllFramesCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			FramesToProcessPicker.IsEnabled = !FindPupilsAllFramesCheckBox.IsChecked.Value;
		}

		private void StepBackButton_Click(object sender, RoutedEventArgs e)
		{
			UpdateVideoTime(pupilFinder.CurrentFrameNumber - FramesToProcessPicker.Value.Value);
		}

		private void DeleteTemplateButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				templatePupilFinder.RemoveTemplate(TemplatePreviewIndex--);
				if (templatePupilFinder.NumTemplates < 2)	// no deleting last one!
					DeleteTemplateButton.Visibility = Visibility.Hidden;
			}
		}

		private void NRecentTemplatesPicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
				templatePupilFinder.NumActiveTemplates = NRecentTemplatesPicker.Value.HasValue ? NRecentTemplatesPicker.Value.Value: 64;
		}

		private void TemplateCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				if (TemplateCountComboBox.SelectedIndex == 0)
				{
					templatePupilFinder.NumActiveTemplates = 0;
					NRecentTemplatesPicker.Visibility = Visibility.Hidden;
				}
				else
				{
					NRecentTemplatesPicker.Visibility = Visibility.Visible;
					templatePupilFinder.NumActiveTemplates = NRecentTemplatesPicker.Value.Value;
				}
			}
		}

		private void NextTemplateButton_Click(object sender, RoutedEventArgs e)
		{
			TemplatePreviewIndex++;
		}

		private void ResetButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder != null)
			{
				pupilFinder.ResetPupilLocations();
				UpdateDisplays();
				UpdateFramesProcessedPreviewImage();
			}
		}

		private void GoBackNumThresholdFramesButton_Click(object sender, RoutedEventArgs e)
		{
			UpdateVideoTime(pupilFinder.CurrentFrameNumber - ConfidenceThresholdFramesPicker.Value.Value);
		}

		private void UseImageAsAntiTemplateButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				int top = (int)(PupilY - PupilRadius * 1.5);
				int bottom = (int)(PupilY + PupilRadius * 1.5 + 2);
				int left = (int)(PupilX - PupilRadius * 1.5);
				int right = (int)(PupilX + PupilRadius * 1.5 + 2);
				templatePupilFinder.AddImageSegmentAsAntiTemplate(top, bottom, left, right);
			}
		}
	}
}