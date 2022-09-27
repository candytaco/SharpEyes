using Avalonia.Controls;
using System;
using SharpEyes.ViewModels;
using Avalonia.Input;
using Avalonia;

namespace SharpEyes.Views
{
	public partial class PupilFindingUserControl : UserControl
	{
		private bool isMouseDownOnVideoCanvas = false;
		private PupilFindingUserControlViewModel? viewModel => (PupilFindingUserControlViewModel)this.DataContext;
		private Canvas videoCanvas;
		public PupilFindingUserControl()
		{
			InitializeComponent();
			FindControls();
		}

		private void FindControls()
		{
			// set static values
			videoCanvas = this.FindControl<Canvas>("VideoCanvas");
		}

		private void SetPupilPosition(Point point)
		{
			viewModel.PupilX = point.X;
			viewModel.PupilY = point.Y;
		}

		public void VideoCanvasMouseDown(object sender, PointerPressedEventArgs e)
		{
			isMouseDownOnVideoCanvas = true;
			SetPupilPosition(e.GetPosition(videoCanvas));
		}

		public void VideoCanvasMouseMove(object sender, PointerEventArgs e)
		{
			if (isMouseDownOnVideoCanvas)
				SetPupilPosition(e.GetPosition(videoCanvas));
		}

		public void VideoCanvasMouseUp(object sender, PointerReleasedEventArgs e)
		{
			isMouseDownOnVideoCanvas = false;
		}
	}
}
