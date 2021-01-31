﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenCvSharp;

namespace Eyetracking
{
	class TemplatePupilFinder : PupilFinder
	{
		/// <summary>
		/// Templates for a dark pupil on a light background
		/// </summary>
		private Mat[] templates;
		private Mat[] matchResults;
		private double bestCorrelationOnThisFrame;

		public TemplatePupilFinder(string videoFileName, System.Windows.Controls.ProgressBar progressBar,
								   SetStatus setStatus, FrameProcessed updateFrame)
			: base(videoFileName, progressBar, setStatus, updateFrame)
		{
			templates = new Mat[maxRadius - minRadius + 1];
			matchResults = new Mat[maxRadius - minRadius + 1];
			for (int i = 0; i < templates.Length; i++)
			{
				templates[i] = new Mat(maxRadius * 2 + 1, maxRadius * 2 + 1, MatType.CV_8UC1, 255);
				matchResults[i] = new Mat();
				Cv2.Circle(templates[i], maxRadius + 1, maxRadius + 1, i + minRadius, 0, -1);   // negative thickness == filled
			}
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
				object templateLock = new object();
				for (int f = 0; f < Frames; f++)
				{
					ReadGrayscaleFrame();
					Parallel.For(0, templates.Length, i =>
					{
						Cv2.MatchTemplate(grayFrame[left, right, top, bottom], templates[i], matchResults[i], TemplateMatchModes.CCoeffNormed);
						double minVal, maxVal;
						Point minLocation, maxLocation;
						matchResults[i].MinMaxLoc(out minVal, out maxVal, out minLocation, out maxLocation);
						lock(templateLock)
						{
							if (maxVal > bestCorrelationOnThisFrame)
							{
								pupilLocations[CurrentFrameNumber, 0] = maxLocation.X + maxRadius + left;
								pupilLocations[CurrentFrameNumber, 1] = maxLocation.Y + maxRadius + top;
								pupilLocations[CurrentFrameNumber, 2] = i + minRadius;
							}
						}
					});

					this.Dispatcher.Invoke(() =>
					{
						updateFrame((double)CurrentFrameNumber / (double)frameCount, 
									pupilLocations[CurrentFrameNumber, 0], 
									pupilLocations[CurrentFrameNumber, 1], 
									pupilLocations[CurrentFrameNumber, 2]);
					});
					((BackgroundWorker)sender).ReportProgress((f + 1) * 100 / Frames);
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
