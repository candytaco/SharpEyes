using System.Windows;
using System.Windows.Input;

namespace Eyetracking
{
	public partial class MainWindow
	{
		private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
		{
			IsPlaying = !IsPlaying;
		}

		private void PreviousFrameButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder.CurrentFrameNumber <= 0)
			{
				return;
			}

			pupilFinder.CancelPupilFinding?.Invoke();
			UpdateVideoTime(pupilFinder.CurrentFrameNumber - 1);
		}

		private void NextFrameButton_Click(object sender, RoutedEventArgs e)
		{
			if (pupilFinder.CurrentFrameNumber >= pupilFinder.frameCount - 1)
			{
				return;
			}

			pupilFinder.CancelPupilFinding?.Invoke();
			UpdateVideoTime(pupilFinder.CurrentFrameNumber + 1);
		}

		private void VideoSlider_MouseDown(object sender, MouseButtonEventArgs e)
		{
			IsPlaying = false;
		}

		private void VideoSlider_MouseUp(object sender, MouseButtonEventArgs e)
		{
			UpdateVideoTime((int) VideoSlider.Value);
		}

		private void VideoSlider_DragCompleted(object sender,
			System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			UpdateVideoTime((int) VideoSlider.Value);
		}

		private void VideoSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
		{
			IsPlaying = false;
		}
	}
}