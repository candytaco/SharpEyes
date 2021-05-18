using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Eyetracking
{
	partial class MainWindow
	{
		/// <summary>
		/// Frame number at which the calibration sequence starts
		/// </summary>
		private int calibrationStartFrame = 0;
		
		/// <summary>
		/// Frame number at which the calibration
		/// </summary>
		private int calibrationEndFrame = 0;

		private void OpenCalibrationParametersButton_Click(object sender, RoutedEventArgs e)
		{
			if (calibrator == null)
				calibrator = new Calibrator()
				{
					OnCalibrationFinished = this.OnCalibrationFinished
				};

			CalibrationParametersWindow calibrationParametersWindow =
				new CalibrationParametersWindow(calibrator.calibrationParameters);

			calibrationParametersWindow.ShowDialog();
		}

		private void CalibrationStartTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			e.Handled = Regex.Match(e.Text, "[0-9]{1,2}:[0-9]{1,2}:[0-9]{1,2}.[0-9]{1,3}").Success;
		}

		private async void CalibrateButton_Click(object sender, RoutedEventArgs e)
		{
			if (calibrator == null)
				calibrator = new Calibrator()
				{
					OnCalibrationFinished = this.OnCalibrationFinished
				};

			CalibrateButton.IsEnabled = false;
			calibrator.Calibrate(GetEyetrackingCalibrationPositions(calibrator.calibrationParameters));
		}

		private List<Point> GetEyetrackingCalibrationPositions(CalibrationParameters parameters)
		{
			List<Point> gazePositions = new List<Point>();

			// TODO: get positions
			for (int i = 0; i < parameters.calibrationPoints.Count; i++)
			{
				int startFrame = calibrationStartFrame + i * parameters.calibrationDurationFrames + parameters.calibrationStartDelayFrames;
				int endFrame = startFrame + parameters.calibrationDurationFrames;
				startFrame += parameters.calibrationPointStartDelayFrames;

				gazePositions.Add(pupilFinder.GetMedianPupilLocation(startFrame, endFrame));
			}

			return gazePositions;
		}

		private void OnCalibrationFinished()
		{
			CalibrateButton.IsEnabled = true;
			CalibrationErrorTextBlock.Text = String.Format("Min RMS error {0}", calibrator.MinRMSError);
		}

		/// <summary>
		/// Set the current frame as the start of the calibration sequence
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void MarkInButton_Click(object sender, RoutedEventArgs e)
		{
			calibrationStartFrame = pupilFinder.CurrentFrameNumber;
			Tuple<int, int, int, int> timestamp = pupilFinder.GetTimestampForFrame(calibrationStartFrame);
			CalibrationStartTextBox.Text = String.Format("{0:00}:{1:00}:{2:00}.{3:000}",
														 timestamp.Item1,
														 timestamp.Item2,
														 timestamp.Item3,
														 timestamp.Item4);
		}

		/// <summary>
		/// Set tge current frame as the end of the calibration sequence
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void MarkOutButton_Click(object sender, RoutedEventArgs e)
		{
			calibrationEndFrame = pupilFinder.CurrentFrameNumber;
		}
	}
}
