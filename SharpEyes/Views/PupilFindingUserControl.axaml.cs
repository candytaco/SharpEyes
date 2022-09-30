using Avalonia.Controls;
using System;
using SharpEyes.ViewModels;
using Avalonia.Input;
using Avalonia;
using Avalonia.Interactivity;
using Eyetracking;

namespace SharpEyes.Views
{
	public partial class PupilFindingUserControl : UserControl
	{
		// for moving the pupil and window
		private bool isMouseDownOnVideoCanvas = false;
		private Point? windowInitialPoint = null;
		private PupilFindingUserControlViewModel? viewModel => (PupilFindingUserControlViewModel)this.DataContext;

		public PupilFinder? pupilFinder = null;

		public PupilFindingUserControl()
		{
			InitializeComponent();
		}

		private void SetCanvasChildElementPosition(Point point)
		{
			switch (viewModel.EditingState)
			{
				case EditingState.MovePupil:
					viewModel.PupilX = point.X;
					viewModel.PupilY = point.Y;
					break;
				case EditingState.DrawWindow:
					if (windowInitialPoint == null)
						windowInitialPoint = point;
					double left, right, top, bottom;
					if (windowInitialPoint.Value.X < point.X)
					{
						left = windowInitialPoint.Value.X;
						right = point.X;
					}
					else
					{
						left = point.X;
						right = windowInitialPoint.Value.X;
					}
					if (windowInitialPoint.Value.Y < point.Y)
					{
						top = windowInitialPoint.Value.Y;
						bottom = point.Y;
					}
					else
					{
						top = point.Y;
						bottom = windowInitialPoint.Value.Y;
					}
					left = left >= 0 ? left : 0;
					top = top >= 0 ? top : 0;
					viewModel.PupilWindowLeft = (int)Math.Round(left);
					viewModel.PupilWindowTop = (int)Math.Round(top);
					viewModel.PupilWindowWidth = right <= VideoCanvas.Width ? (int)Math.Round(right - left) : (int)Math.Round(VideoCanvas.Width - left);
					viewModel.PupilWindowHeight = bottom <= VideoCanvas.Height ? (int)Math.Round(bottom - top) : (int)Math.Round(VideoCanvas.Height - top);

					break;
				default:
					break;
			}
		}

		public void VideoCanvasMouseDown(object sender, PointerPressedEventArgs e)
		{
			isMouseDownOnVideoCanvas = true;
			windowInitialPoint = null;
			SetCanvasChildElementPosition(e.GetPosition(VideoCanvas));
		}

		public void VideoCanvasMouseMove(object sender, PointerEventArgs e)
		{
			if (isMouseDownOnVideoCanvas)
				SetCanvasChildElementPosition(e.GetPosition(VideoCanvas));
		}

		public void VideoCanvasMouseUp(object sender, PointerReleasedEventArgs e)
		{
			isMouseDownOnVideoCanvas = false;
		}

		public void VideoCanvasScroll(object sender, PointerWheelEventArgs e)
		{
			// e.Delta.Y encodes the number of clicks of the wheel, with + being up, - being down
			if (viewModel.EditingState == EditingState.MovePupil)
				viewModel.PupilDiameter += e.Delta.Y;
		}

		public async void LoadVideo(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog()
			{
				Title = "Load eyetracking video"
			};
			string[] fileName = await openFileDialog.ShowAsync((Window)this.VisualRoot);

			if (fileName == null || fileName.Length == 0)
				return;

			switch (viewModel.PupilFinderType)
			{
				// TODO: implement delegates, either here, or in the PupilFinder class
				case PupilFinderType.Template:
					pupilFinder = new TemplatePupilFinder(fileName[0], viewModel);
					break;
				case PupilFinderType.HoughCircles:
					pupilFinder = new HoughPupilFinder(fileName[0], viewModel);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			pupilFinder.ReadFrame();
			viewModel.VideoFrame = pupilFinder.GetFrameForDisplay(false);

			if (!pupilFinder.isTimestampParsed)
			{
				viewModel.ShowTimestampParsing = true;
				if (viewModel.AutoReadTimestamps)
					pupilFinder.ParseTimeStamps();
			}

			if (pupilFinder is TemplatePupilFinder templatePupilFinder)
			{
				viewModel.TemplatePupilFinderConfigUserControlViewModel.CurrentTemplateIndex = 0;
				viewModel.TemplatePupilFinderConfigUserControlViewModel.TemplatePreviewImage =
					templatePupilFinder.GetTemplateImage(0);
			}
		}

		public async void ReadTimestamps(object sender, RoutedEventArgs e)
		{
			if (pupilFinder != null)
				pupilFinder.ParseTimeStamps();
		}

		public async void LoadTimestamps(object sender, RoutedEventArgs e)
		{
			if (pupilFinder != null)
			{
				OpenFileDialog openFileDialog = new OpenFileDialog()
				{
					Title = "Load timestamps...",
					Filters = {new FileDialogFilter(){Name = "Numpy File (*.npy)", Extensions = {"npy"}}}
				};
				string[] fileName = await openFileDialog.ShowAsync((Window)this.VisualRoot);

				if (fileName == null || fileName.Length == 0)
					return;
				pupilFinder.LoadTimestamps(fileName[0]);
			}
		}

		public async void FindPupilsButton_Click(object sender, RoutedEventArgs e)
		{
			if (viewModel.IsFindingPupils)
				pupilFinder.CancelPupilFindingDelegate();
			else
			{
				pupilFinder.FindPupils();
			}
		}
	}
}
