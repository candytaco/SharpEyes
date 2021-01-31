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

		public HoughPupilFinder(string videoFileName, System.Windows.Controls.ProgressBar progressBar, 
								SetStatus setStatus, FrameProcessed updateFrame)
			: base(videoFileName, progressBar, setStatus, updateFrame)
		{
			
		}

		public override void FindPupils(int Frames)
		{
			base.FindPupils(Frames);
			setStatus("Finding pupils 0/100%");
			BackgroundWorker worker = new BackgroundWorker
			{
				WorkerReportsProgress = true
			};
			worker.DoWork += delegate (object sender, DoWorkEventArgs args)
			{
				for (int i = 0; i < Frames; i++)
				{
					ReadGrayscaleFrame();
					CircleSegment circle = grayFrame[top, bottom, left, right].HoughCircles(HoughMethods.Gradient, DP, minCircleDistance, param1, param2, minRadius, maxRadius)[0];
					pupilLocations[CurrentFrameNumber, 0] = circle.Center.X + left;
					pupilLocations[CurrentFrameNumber, 1] = circle.Center.Y + top;
					pupilLocations[CurrentFrameNumber, 2] = circle.Radius;

					this.Dispatcher.Invoke(() =>
					{
						updateFrame((double)CurrentFrameNumber / (double)frameCount, circle.Center.X + left, circle.Center.Y + top, circle.Radius);
					});
					((BackgroundWorker)sender).ReportProgress((i + 1) * 100 / Frames);
				}
			};

			worker.ProgressChanged += delegate (object sender, ProgressChangedEventArgs e)
			{
				setStatus(string.Format("Finding pupils {0}/100%", e.ProgressPercentage));
				progressBar.Value = e.ProgressPercentage;
			};

			worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
			{
				progressBar.Value = 0;
				setStatus();
			};

			worker.RunWorkerAsync();
		}
	}
}
