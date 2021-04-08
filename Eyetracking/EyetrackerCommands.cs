using System.Windows.Input;

namespace Eyetracking
{
	public static class EyetrackerCommands
	{
		public static readonly RoutedUICommand OpenEyetrackingData = new RoutedUICommand("Open Eyetracking Data", "Open Eyetracking data", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Control) });

		public static readonly RoutedUICommand Right = new RoutedUICommand("Right", "Right", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.Right) });

		public static readonly RoutedUICommand Left = new RoutedUICommand("Left", "Left", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.Left) });

		public static readonly RoutedUICommand BackConfidenceFrames = new RoutedUICommand("BackConfidenceFrames", "BackConfidenceFrames", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.Left, ModifierKeys.Alt) });

		public static readonly RoutedUICommand BackFindPupilFrames = new RoutedUICommand("BackFindPupilFrames", "BackFindPupilFrames", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.Left, ModifierKeys.Control) });

		public static readonly RoutedUICommand ForwardConfidenceFrames = new RoutedUICommand("ForwardConfidenceFrames", "ForwardConfidenceFrames", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.Right, ModifierKeys.Alt) });

		public static readonly RoutedUICommand ForwardFindPupilFrames = new RoutedUICommand("ForwardFindPupilFrames", "ForwardFindPupilFrames", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.Right, ModifierKeys.Control) });

		public static readonly RoutedUICommand Up = new RoutedUICommand("Up", "Up", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.Up) });

		public static readonly RoutedUICommand Down = new RoutedUICommand("Down", "Down", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.Down) });

		public static readonly RoutedUICommand PlayPause = new RoutedUICommand("Play/Pause", "Play/Pause", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.Space) });

		public static readonly RoutedUICommand IncreasePupilSize = new RoutedUICommand("Increase Pupil Size", "Increase Pupil Size", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.OemCloseBrackets) });

		public static readonly RoutedUICommand DecreasePupilSize = new RoutedUICommand("Decrease Pupil Size", "Decrease Pupil Size", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.OemOpenBrackets) });

		public static readonly RoutedUICommand DrawWindow = new RoutedUICommand("Draw window", "Draw window", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.M, ModifierKeys.Alt) });

		public static readonly RoutedUICommand MovePupil = new RoutedUICommand("Move pupil", "Move pupil", typeof(EyetrackerCommands),
			new InputGestureCollection() { new KeyGesture(Key.V, ModifierKeys.Alt) });
	};
}