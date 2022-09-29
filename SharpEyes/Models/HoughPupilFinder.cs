using OpenCvSharp;
using System;
using System.ComponentModel;

namespace Eyetracking
{
	internal class HoughPupilFinder : PupilFinder
	{
		// Hough circle parameters
		public double DP = 1;
		public double minCircleDistance = 600;
		public double param1 = 80;
		public double param2 = 20;

		public HoughPupilFinder(string videoFileName)
			: base(videoFileName)
		{
			
		}

		public override void FindPupils(int Frames, double threshold = 0, int thresholdFrames = 0, bool doNotStopForBlink = false)
		{
			base.FindPupils(Frames);
			DateTime start = DateTime.Now;
			SetStatusDelegate("Finding pupils 0/100%");
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
					CircleSegment circle = filteredFrame[top, bottom, left, right].HoughCircles(HoughModes.Gradient, DP, minCircleDistance, param1, param2, minRadius, maxRadius)[0];
					pupilLocations[CurrentFrameNumber, 0] = circle.Center.X + left;
					pupilLocations[CurrentFrameNumber, 1] = circle.Center.Y + top;
					pupilLocations[CurrentFrameNumber, 2] = circle.Radius;
					isAnyFrameProcessed = true;

					isFrameProcessed[CurrentFrameNumber] = true;
					
						UpdateFrameDelegate();
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
				SetStatusDelegate(string.Format("Finding pupils {0}%", e.ProgressPercentage));
			};

			worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
			{
				if (e.Cancelled)
					SetStatusDelegate(string.Format("Idle. Pupil finding was cancelled."));
				else
					SetStatusDelegate(string.Format("Idle. {0} frames processed in {1:c}", Frames, DateTime.Now - start));
				CancelPupilFindingDelegate -= worker.CancelAsync;
			};

			CancelPupilFindingDelegate += worker.CancelAsync;
			worker.RunWorkerAsync();
		}
	}
}
