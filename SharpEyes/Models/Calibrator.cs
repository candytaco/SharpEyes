﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Avalonia;
using NumSharp;
using Num = NumSharp.np;

// Note: try not to use NumSharp data structures in these classes
// Because they're interally contained

namespace Eyetracking
{
	public delegate void OnCalibrationFinishedDelegate();
	/// <summary>
	/// Performs the actual math for mapping between eyetracking video space to stimulus frames space
	/// </summary>
	class Calibrator
	{
		/// <summary>
		/// Description of the calibration sequence
		/// </summary>
		public CalibrationParameters calibrationParameters;
		
		/// <summary>
		/// Averaged positions of the pupil at the calibration points
		/// in order that they are presented in.
		/// </summary>
		private List<Point> calibrationPositions = null;

		private RBF2D xInterpolator = null, yInterpolator = null;

		public OnCalibrationFinishedDelegate OnCalibrationFinished;

		public double MinRMSError
		{
			get
			{
				if (xInterpolator == null) return 0;
				return Math.Sqrt(xInterpolator.RMSError * xInterpolator.RMSError +
				                 yInterpolator.RMSError * yInterpolator.RMSError);
			}
		}

		public Calibrator()
		{
			calibrationParameters = CalibrationParameters.GetDefault35PointCalibrationParameters();
		}

		public Calibrator(CalibrationParameters parameters)
		{
			calibrationParameters = parameters;
		}

		public Calibrator(CalibrationParameters parameters, List<Point> positions)
		{
			calibrationParameters = parameters;
			calibrationPositions = positions;
		}

		public async void Calibrate()
		{ 
			Calibrate(this.calibrationPositions);
		}

		/// <summary>
		/// Performs the calibration
		/// </summary>
		/// <param name="positions">list of gaze positions in of calibration points in order in which they were presented</param>
		public async void Calibrate(List<Point> positions)
		{
			calibrationPositions = positions;
			double[] pupilX = new double[positions.Count];
			double[] pupilY = new double[positions.Count];
			double[] screenX = new double[positions.Count];
			double[] screenY = new double[positions.Count];

			for (int i = 0; i < positions.Count; i++)
			{
				pupilX[i] = positions[i].X;
				pupilY[i] = positions[i].Y;
				// remember this indexing exists because the order in which the points are presented is
				// not the same as they are generated by the loop
				screenX[i] = calibrationParameters.calibrationPoints[calibrationParameters.calibrationSequence[i].Index].X;
				screenY[i] = calibrationParameters.calibrationPoints[calibrationParameters.calibrationSequence[i].Index].Y;
			}

			//
			await Task.Run(() =>
			{
				xInterpolator = RBF2D.SearchHyperparameters(pupilX, pupilY, screenX,
					calibrationParameters.baseRadiusRange, calibrationParameters.numBaseRadii,
					calibrationParameters.numLayersRange, calibrationParameters.regularizerRange,
					calibrationParameters.numRegularizers,
					calibrationParameters.logSpaceRegularizers);
				yInterpolator = RBF2D.SearchHyperparameters(pupilX, pupilY, screenY,
					calibrationParameters.baseRadiusRange, calibrationParameters.numBaseRadii,
					calibrationParameters.numLayersRange, calibrationParameters.regularizerRange,
					calibrationParameters.numRegularizers,
					calibrationParameters.logSpaceRegularizers);

			});

			OnCalibrationFinished();
		}

		public List<Point> MapPupilPositionToGazePosition(List<Point> pupilPositions)
		{
			if (xInterpolator == null)	// not yet initialized
				throw new InvalidOperationException();

			List<Point> gazePositions = new List<Point>();

			for (int i = 0; i < pupilPositions.Count; i++)
			{
				gazePositions.Add(new Point(xInterpolator.Evaluate(pupilPositions[i].X, pupilPositions[i].Y),
												 yInterpolator.Evaluate(pupilPositions[i].X, pupilPositions[i].Y)));
			}
			return gazePositions;
		}

		/// <summary>
		/// overloaded to take arrays
		/// </summary>
		/// <param name="pupilPositions">double[,] of shape [2, samples]</param>
		/// <returns></returns>
		public double[,] MapPupilPositionToGazePosition(double[,] pupilPositions)
		{
			double[,] gazePositions = new double[2, pupilPositions.GetLength(1)];

			Parallel.For(0, pupilPositions.GetLength(1), i =>
			{
				gazePositions[0, i] = xInterpolator.Evaluate(pupilPositions[0, i], pupilPositions[1, i]);
				gazePositions[1, i] = yInterpolator.Evaluate(pupilPositions[0, i], pupilPositions[1, i]);
			});

			return gazePositions;
		}

		/// <summary>
		/// Overloaded to take numpy-compatiable arrays
		/// </summary>
		/// <param name="pupilPositions">NDarray of size [samples, coords]</param>
		/// <returns></returns>
		public NDArray MapPupilPositionToGazePosition(NDArray pupilPositions)
		{
			NDArray gazePositions = Num.zeros((pupilPositions.shape[0], 2));

			Parallel.For(0, pupilPositions.shape[0], i =>
			{
				gazePositions[i, 0] = xInterpolator.Evaluate(pupilPositions[i, 0], pupilPositions[i, 1]);
				gazePositions[i, 1] = yInterpolator.Evaluate(pupilPositions[i, 0], pupilPositions[i, 1]);
			}); 

			return gazePositions;
		}
	}
}
