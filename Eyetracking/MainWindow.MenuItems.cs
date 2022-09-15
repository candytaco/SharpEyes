using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Eyetracking
{
	public partial class MainWindow
	{
		private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				DefaultExt = ".avi",
				Filter = "Eyetracking AVIs (.avi)|*.avi",
				Title = "Open eyetracking video..."
			};
			if (openFileDialog.ShowDialog() == true)
			{
				LoadFile(openFileDialog.FileName);
			}
		}

		private void SaveTimeStampsMenuItemClick(object sender, RoutedEventArgs e)
		{
			SaveTimestamps();
		}

		private void LoadSavedDataMenuItem_Click(object sender, RoutedEventArgs e)
		{
			LoadTimestamps();
		}

		private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
		{
			ShowAboutDialogue();
		}

		private void SaveEyetrackingMenuItem_Click(object sender, RoutedEventArgs e)
		{
			//if (!pupilFinder.AreAllFramesProcessed)
			//{
			//	if (MessageBox.Show("Not all frames processed. Save data?", "Incomplete processing", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
			//	{
			//		return;
			//	}
			//}

			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy",
				Title = "Save pupils...",
				FileName = Path.GetFileNameWithoutExtension(pupilFinder.autoPupilsFileName)
			};
			if (saveFileDialog.ShowDialog() == true)
			{
				pupilFinder.SavePupilLocations(saveFileDialog.FileName);
			}
		}

		private void LoadSavedEyetrackingMenuItem_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "Numpy file (*.npy)|*.npy",
				Title = "Load pupils..."
			};
			if (openFileDialog.ShowDialog() == true)
			{
				pupilFinder.LoadPupilLocations(openFileDialog.FileName);
				FindPupilsButton.IsEnabled = true;
				UpdateDisplays();
			}
		}

		private void LoadSavedTemplatesMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				OpenFileDialog openFileDialog = new OpenFileDialog
				{
					Filter = "Data file (*.dat)|*.dat",
					Title = "Load templates..."
				};
				if (openFileDialog.ShowDialog() == true)
				{
					templatePupilFinder.LoadTemplates(openFileDialog.FileName);
					TemplatePreviewIndex = templatePupilFinder.NumTemplates;
					SetStatus(String.Format("Idle; Loaded {0} templates", templatePupilFinder.NumTemplates));

					if (templatePupilFinder.NumTemplates > 1)
						DeleteTemplateButton.Visibility = Visibility.Visible;
				}
			}
		}

		private void SaveTemplatesMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				if (!templatePupilFinder.IsUsingCustomTemplates)
				{
					MessageBox.Show("Pupil finder not using custom templates", "No templates to save",
						MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				SaveFileDialog saveFileDialog = new SaveFileDialog
				{
					Filter = "Data file (*.dat)|*.dat",
					Title = "Save templates...",
					FileName = Path.GetFileNameWithoutExtension(templatePupilFinder.autoTemplatesFileName)
				};
				if (saveFileDialog.ShowDialog() == true)
				{
					templatePupilFinder.SaveTemplates(saveFileDialog.FileName);
				}
			}
			else
			{
				MessageBox.Show("Not a template pupil finder", "Wrong pupil finder", MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}

		private void HelpMenuItem_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://github.com/candytaco/SharpEyes/wiki/Stimulus-Gaze-Viewer");
		}

		private void ReportBugmenuItem_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://github.com/candytaco/SharpEyes/issues/new/choose");
		}

		private void saveGazeTraceMenuItem_Click(object sender, RoutedEventArgs e)
		{
			SaveGazeTrace();
		}

	}
}