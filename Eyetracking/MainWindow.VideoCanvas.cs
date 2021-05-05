using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Eyetracking
{
	public partial class MainWindow
	{
		private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			mouseMoveStartPoint = e.GetPosition(canvas);
			switch (editingState)
			{
				case EditingState.DrawingWindow:
					if (mouseMoveStartPoint.X < 0 || mouseMoveStartPoint.Y < 0 ||
					    mouseMoveStartPoint.X > canvas.Width || mouseMoveStartPoint.Y > canvas.Height)
					{
						return;
					}

					Canvas.SetLeft(SearchWindowRectangle, mouseMoveStartPoint.X);
					Canvas.SetTop(SearchWindowRectangle, mouseMoveStartPoint.Y);

					isEditingStarted = true;
					break;
				case EditingState.MovingPupil:

					PupilX = mouseMoveStartPoint.X / videoScaleFactor;
					PupilY = mouseMoveStartPoint.Y / videoScaleFactor;
					if (Double.IsNaN(PupilRadius))
						PupilRadius = 16;

					// make transparent so we can see better
					PupilEllipse.Stroke.Opacity = 0.5;

					isEditingStarted = true;
					break;
				default:
					return;
			}
		}

		private void Canvas_MouseMove(object sender, MouseEventArgs e)
		{
			if (!isEditingStarted)
			{
				return;
			}

			switch (editingState)
			{
				case EditingState.DrawingWindow:
					// confine to canvas
					Point mouse = e.GetPosition(canvas);
					mouse.X = mouse.X > 0 ? mouse.X : 0;
					mouse.X = mouse.X < canvas.Width ? mouse.X : canvas.Width;
					mouse.Y = mouse.Y > 0 ? mouse.Y : 0;
					mouse.Y = mouse.Y < canvas.Height ? mouse.Y : canvas.Height;

					Canvas.SetLeft(SearchWindowRectangle, mouse.X < mouseMoveStartPoint.X ? mouse.X : mouseMoveStartPoint.X);
					Canvas.SetTop(SearchWindowRectangle, mouse.Y < mouseMoveStartPoint.Y ? mouse.Y : mouseMoveStartPoint.Y);
					SearchWindowRectangle.Width = Math.Abs(e.GetPosition(canvas).X - mouseMoveStartPoint.X);
					SearchWindowRectangle.Height = Math.Abs(e.GetPosition(canvas).Y - mouseMoveStartPoint.Y);
					break;
				case EditingState.MovingPupil:
					PupilX = e.GetPosition(canvas).X / videoScaleFactor;
					PupilY = e.GetPosition(canvas).Y / videoScaleFactor;
					break;
				default:
					return;
			}
		}

		private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			isEditingStarted = false;
			switch (editingState)
			{
				case EditingState.DrawingWindow:
					drawWindowButton.IsChecked = false;
					editingState = EditingState.None;
					break;
				case EditingState.MovingPupil:  // do not auto turn off pupil editing
					// undo the temporary transparency
					PupilEllipse.Stroke.Opacity = 1;
					UpdatePupilPositionData();
					break;
				default:    // EditingState.None
					return;
			}
		}

		private void SearchWindowRectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			isMovingSearchWindow = true;
			mouseMoveStartPoint = e.GetPosition(canvas);
		}

		private void SearchWindowRectangle_MouseMove(object sender, MouseEventArgs e)
		{
			if (!isMovingSearchWindow)
			{
				return;
			}

			Canvas.SetLeft(SearchWindowRectangle, Canvas.GetLeft(SearchWindowRectangle) + e.GetPosition(canvas).X - mouseMoveStartPoint.X);
			Canvas.SetTop(SearchWindowRectangle, Canvas.GetTop(SearchWindowRectangle) + e.GetPosition(canvas).Y - mouseMoveStartPoint.Y);
		}

		private void SearchWindowRectangle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			isMovingSearchWindow = false;
		}

		private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (editingState == EditingState.MovingPupil)
			{
				double deltaRadius = (e.Delta > 0 ? 1 : -1);
				double newSize = PupilRadius + deltaRadius;
				if (((newSize < pupilFinder.minRadius) && e.Delta < 0) || ((newSize > pupilFinder.maxRadius) && e.Delta > 0))
				{
					return;
				}

				PupilRadius = newSize;
				UpdatePupilPositionData();
			}
			else
			{
				if (e.Delta < 0)
				{
					NextFrameButton_Click(null, null);
				}
				else
				{
					PreviousFrameButton_Click(null, null);
				}
			}
		}
	}
}