using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using SharpEyes.ViewModels;

namespace SharpEyes.Views
{
	public partial class StimulusGazeUserControl : UserControl
	{
		private bool areThumbEventsAttached = false;
		private bool isDraggingVideoSlider = false;
		private StimulusGazeViewModel? viewModel => (StimulusGazeViewModel)this.DataContext;
		public StimulusGazeUserControl()
		{
			InitializeComponent();
			this.GotFocus += (sender, args) => { AttachThumbEvents(); };
		}

		private void VideoCanvas_OnPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			
		}

		private void VideoCanvas_OnPointerMoved(object? sender, PointerEventArgs e)
		{
			
		}

		private void VideoCanvas_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			
		}

		private void VideoCanvas_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			
		}

		/// <summary>
		/// Attaches event handlers on to the thumb in the video slider, because Avalonia XAML
		/// does not expose those properties
		/// </summary>
		private void AttachThumbEvents()
		{
			if (!areThumbEventsAttached)
			{
				Thumb thumb = VideoTimeSlider.FindDescendantOfType<Thumb>();
				thumb.DragStarted += VideoTimeSlider_DragStarted;
				thumb.DragDelta += VideoTimeSlider_Drag;
				thumb.DragCompleted += VideoTimeSlider_DragFinished;
				areThumbEventsAttached = true;
			}
		}

		private void VideoTimeSlider_DragStarted(object sender, VectorEventArgs e)
		{
			if (viewModel.IsVideoPlaying)
				viewModel.PlayPause();
			isDraggingVideoSlider = true;
		}

		private void VideoTimeSlider_Drag(object sender, VectorEventArgs e)
		{
			if (isDraggingVideoSlider)
				viewModel.ShowFrame();
		}

		private void VideoTimeSlider_DragFinished(object sender, VectorEventArgs e)
		{
			isDraggingVideoSlider = false;
		}
	}
}
