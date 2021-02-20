﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Eyetracking
{
	/// <summary>
	/// A struct that stores information about the eyetracking calibration sequence
	/// </summary>
	public class CalibrationParameters
	{
		// video information

		/// <summary>
		/// List of calibration points in screen space
		/// </summary>
		public List<Point> calibrationPoints;

		/// <summary>
		/// List of the points in the order that they are presented. Indentified by index in <see cref="calibrationPoints"/>
		/// </summary>
		public List<int> calibrationSequence;

		/// <summary>
		/// How long is the fixation at each point
		/// </summary>
		public double calibrationDuration = 2.0;

		/// <summary>
		/// DPI Scaling factor. See Unreal Engine
		/// </summary>
		public double DPIUnscaleFactor = 1.0;

		/// <summary>
		/// Default amount of time to discard from beginning of each point
		/// to account for saccade time
		/// </summary>
		public double calibrationPointStartDelaySeconds = 1 / 6.0;

		/// <summary>
		/// Framerate of eyetracking videos
		/// </summary>
		public int eyetrackingFPS = 60;

		/// <summary>
		/// Conversion from delay in seconds to frames
		/// </summary>
		public int calibrationPointStartDelayFrames
		{
			get
			{
				return (int)(calibrationPointStartDelaySeconds * eyetrackingFPS);
			}
		}

		// Calibration hyperparameters

		/// <summary>
		/// Minimum base radius for RBF
		/// </summary>
		public double minBaseRadius = 1.0;

		public double maxBaseRadius = 100.0;

		public Tuple<double, double> baseRadiusRange => new Tuple<double, double>(minBaseRadius, maxBaseRadius);

		public int numBaseRadii = 10;

		public int minNumLayers = 1;

		public int maxNumlayers = 10;

		public Tuple<int, int> numLayersRange => new Tuple<int, int>(minNumLayers, maxNumlayers);

		public double minRegularizer = 0;

		public double maxRegularizer = 10;

		public Tuple<double, double> regularizerRange => new Tuple<double, double>(minRegularizer, maxRegularizer);

		public int numRegularizers = 10;

		public bool logSpaceRegularizers = true;

		public CalibrationParameters()
		{
			calibrationPoints = new List<Point>();
			calibrationSequence = new List<int>();
		}

		public CalibrationParameters(List<Point> calibrationPoints, List<int> calibrationSequence)
		{
			this.calibrationPoints = calibrationPoints;
			this.calibrationSequence = calibrationSequence;
		}

		public static CalibrationParameters GetDefault35PointCalibrationParameters()
		{
			return new CalibrationParameters(GeneratePoints(1024, 768, 7, 5, 1.0), new List<int>(CalibrationSequence35));
		}

		public static List<Point> GeneratePoints(int width, int height, int numHorizontal, int numVertical, double DPIUnscaleFactor)
		{
			List<Point> points = new List<Point>();

			int ySpace = (int)(height / numVertical / DPIUnscaleFactor);
			int xSpace = (int)(width / numHorizontal / DPIUnscaleFactor);
			int xStart = -numHorizontal / 2;
			int yStart = -numVertical / 2;

			int x, y;
			for (int i = 0; i < numHorizontal / 2 + 1; i++)
			{
				x = width / 2 + i * xSpace;
				for (int j = 0; j < numVertical / 2 + 1; j++)
				{
					y = height / 2 + j * ySpace;
					points.Add(new Point(x, y));
				}
			}

			return points;
		}

		public static readonly List<int> CalibrationSequence35 = new List<int>(new int[]{13, 30, 17, 16, 2, 27, 1, 28, 25, 10, 26, 9, 14, 5, 34, 32,
																						 31, 12, 8, 33, 18, 19, 3, 23, 29, 20, 7, 0, 4, 24, 22, 11, 15,
																						 21, 6});
	}
}
