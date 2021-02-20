using OpenCvSharp;
using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Eyetracking
{
	internal class HoughPupilFinder : PupilFinder
	{
		// Hough circle parameters
		public double DP = 1;
		public double minCircleDistance = 600;
		public double param1 = 80;
		public double param2 = 20;

		public HoughPupilFinder(string videoFileName, System.Windows.Controls.ProgressBar progressBar, System.Windows.Shell.TaskbarItemInfo taskbar,
								SetStatusDelegate setStatus, FrameProcessedDelegate updateFrame, FramesProcessedDelegate framesProcessed)
			: base(videoFileName, progressBar, taskbar, setStatus, updateFrame, framesProcessed)
		{
			
		}

		public override void FindPupils(int Frames)
		{
			base.FindPupils(Frames);
			DateTime start = DateTime.Now;
			SetStatus("Finding pupils 0/100%");
			BackgroundWorker worker = new BackgroundWorker
			{
				WorkerReportsProgress = true,
				WorkerSupportsCancellation = true
			};
			worker.DoWork += delegate (object sender, DoWorkEventArgs args)
			{
				for (int i = 0; i < Frames; i++)
				{
					ReadGrayscaleFrame();
					CircleSegment circle = filteredFrame[top, bottom, left, right].HoughCircles(HoughMethods.Gradient, DP, minCircleDistance, param1, param2, minRadius, maxRadius)[0];
					pupilLocations[CurrentFrameNumber, 0] = circle.Center.X + left;
					pupilLocations[CurrentFrameNumber, 1] = circle.Center.Y + top;
					pupilLocations[CurrentFrameNumber, 2] = circle.Radius;
					isAnyFrameProcessed = true;

					isFrameProcessed[CurrentFrameNumber] = true;

					this.Dispatcher.Invoke(() =>
					{
						UpdateFrame();
					});
					((BackgroundWorker)sender).ReportProgress((i + 1) * 100 / Frames);
					if (worker.CancellationPending)
					{
						args.Cancel = true;
						break;
					}
				}
			};

			worker.ProgressChanged += delegate (object sender, ProgressChangedEventArgs e)
			{
				SetStatus(string.Format("Finding pupils {0}%", e.ProgressPercentage));
				progressBar.Value = e.ProgressPercentage;
			};

			worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
			{
				progressBar.Value = 0;
				if (e.Cancelled)
					SetStatus(string.Format("Idle. Pupil finding was cancelled."));
				else
					SetStatus(string.Format("Idle. {0} frames processed in {1:c}", Frames, DateTime.Now - start));
				this.Dispatcher.Invoke(OnFramesPupilsProcessed);
				CancelPupilFinding -= worker.CancelAsync;
			};

			CancelPupilFinding += worker.CancelAsync;
			worker.RunWorkerAsync();
		}
	}
}
