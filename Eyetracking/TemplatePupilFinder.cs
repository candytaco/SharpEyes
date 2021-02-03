﻿using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace Eyetracking
{

	class TemplatePupilFinder : PupilFinder
	{
		/// <summary>
		/// Templates for a dark pupil on a light background
		/// </summary>
		private Mat[] templates;
		private Mat[] matchResults;
		private double bestCorrelationOnThisFrame = -1;

		/// <summary>
		/// Because there is an option to use a part of a frame as a template,
		/// there is no good way to determine pupil size if that is the case. So we store
		/// the value from that frame here and apply it onwards.
		/// The nullable-ness also doubles as an indicator of whether we are using
		/// the frame as a template. if null, we are using generated templates.
		/// </summary>
		private Nullable<double> storedPupilSize = null;

		public TemplatePupilFinder(string videoFileName, System.Windows.Controls.ProgressBar progressBar,
								   SetStatusDelegate setStatus, FrameProcessedDelegate updateFrame, FramesProcessedDelegate framesProcessed)
			: base(videoFileName, progressBar, setStatus, updateFrame, framesProcessed)
		{
			MakeTemplates();
		}

		public void MakeTemplates()
		{
			templates = new Mat[maxRadius - minRadius + 1];
			matchResults = new Mat[maxRadius - minRadius + 1];
			for (int i = 0; i < templates.Length; i++)
			{
				templates[i] = new Mat(maxRadius * 2 + 1, maxRadius * 2 + 1, MatType.CV_8UC1, 255);
				matchResults[i] = new Mat();
				Cv2.Circle(templates[i], maxRadius + 1, maxRadius + 1, i + minRadius, 0, -1);   // negative thickness == filled
			}
			storedPupilSize = null;
		}

		public void UseImageSegmentAsTemplate(int top, int bottom, int left, int right, double radius)
		{
			if (grayFrame.Width < 1)
			{
				int currentFrame = CurrentFrameNumber;
				ReadGrayscaleFrame();
				CurrentFrameNumber = currentFrame;
			}
			templates[0] = new Mat(bottom - top, right - left, MatType.CV_8UC1);
			matchResults[0] = new Mat();
			grayFrame[top, bottom, left, right].CopyTo(templates[0]);
			templates[0].SaveImage("debug template.png");
			storedPupilSize = radius;
		}

		/// <summary>
		/// Gets a bitmap of the template for display
		/// </summary>
		/// <returns>The first template, if using automated templates, or the template image picked from the video</returns>
		public BitmapImage GetTemplateImage()
		{
			MemoryStream memory = new MemoryStream();
			templates[0].ToBitmap().Save(memory, ImageFormat.Bmp);
			memory.Position = 0;
			BitmapImage image = new BitmapImage();
			image.BeginInit();
			image.StreamSource = memory;
			image.CacheOption = BitmapCacheOption.OnLoad;
			image.EndInit();
			return image;
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
				// TODO: perhaps parallelize over frames rather than templates
				object templateLock = new object();
				for (int f = 0; f < Frames; f++)
				{
					ReadGrayscaleFrame();
					if (storedPupilSize == null)
					{
						Parallel.For(0, templates.Length, i =>
						{
							Cv2.MatchTemplate(grayFrame[top, bottom, left, right], templates[i], matchResults[i], TemplateMatchModes.CCoeffNormed);
							double minVal, maxVal;
							Point minLocation, maxLocation;
							matchResults[i].MinMaxLoc(out minVal, out maxVal, out minLocation, out maxLocation);
							lock (templateLock)
							{
								if (maxVal > bestCorrelationOnThisFrame)
								{
									pupilLocations[CurrentFrameNumber, 0] = maxLocation.X + left + maxRadius;
									pupilLocations[CurrentFrameNumber, 1] = maxLocation.Y + top + maxRadius;
									pupilLocations[CurrentFrameNumber, 2] = i + minRadius;
									bestCorrelationOnThisFrame = maxVal;
								}
							}
						});
						pupilLocations[CurrentFrameNumber, 3] = bestCorrelationOnThisFrame;
						bestCorrelationOnThisFrame = -1;
					}
					else
					{
						Cv2.MatchTemplate(grayFrame[top, bottom, left, right], templates[0], matchResults[0], TemplateMatchModes.CCoeffNormed);
						double minVal, maxVal;
						Point minLocation, maxLocation;
						matchResults[0].MinMaxLoc(out minVal, out maxVal, out minLocation, out maxLocation);
						pupilLocations[CurrentFrameNumber, 0] = maxLocation.X + left + templates[0].Width / 2;
						pupilLocations[CurrentFrameNumber, 1] = maxLocation.Y + top + templates[0].Height / 2;
						pupilLocations[CurrentFrameNumber, 2] = storedPupilSize;
						pupilLocations[CurrentFrameNumber, 3] = maxVal;
					}

					isFrameProcessed[CurrentFrameNumber] = true;

					this.Dispatcher.Invoke(() =>
					{
						UpdateFrame((double)CurrentFrameNumber / (double)frameCount, 
									pupilLocations[CurrentFrameNumber, 0], 
									pupilLocations[CurrentFrameNumber, 1], 
									pupilLocations[CurrentFrameNumber, 2],
									pupilLocations[CurrentFrameNumber, 3]);
					});
					((BackgroundWorker)sender).ReportProgress((f + 1) * 100 / Frames);
					if (worker.CancellationPending)
					{
						args.Cancel = true;
						break;
					}
				}
			};

			worker.ProgressChanged += delegate (object sender, ProgressChangedEventArgs e)
			{
				SetStatus(string.Format("Finding pupils in {0} frames {1}/100%", Frames, e.ProgressPercentage));
				progressBar.Value = e.ProgressPercentage;
			};

			worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
			{
				progressBar.Value = 0;
				if (e.Cancelled)
					SetStatus(string.Format("Idle. Pupil finding was cancelled."));
				else
					SetStatus(string.Format("Idle. {0} frames processed in {1:c}", Frames, DateTime.Now - start));
				this.Dispatcher.Invoke(OnFramesProcessed);
				CancelPupilFinding -= worker.CancelAsync;
			};

			CancelPupilFinding += worker.CancelAsync;
			worker.RunWorkerAsync();
		}
	}
}
