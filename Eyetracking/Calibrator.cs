﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

// Note: try not to use NumSharp data structures in these classes
// Because they're interally contained

namespace Eyetracking
{
	/// <summary>
	/// Performs the actual math for mapping between eyetracking video space to stimulus frames space
	/// </summary>
	class Calibrator
	{
		/// <summary>
		/// Description of the calibration sequence
		/// </summary>
		private CalibrationParameters calibrationParameters;
		
		/// <summary>
		/// Averaged positions of the pupil at the calibration points
		/// in order that they are presented in.
		/// </summary>
		private List<Point> calibrationPositions = null;

		private RBF2D xInterpolator = null, yInterpolator = null;

		public Calibrator(CalibrationParameters parameters)
		{
			calibrationParameters = parameters;
		}

		public Calibrator(CalibrationParameters parameters, List<Point> positions)
		{
			calibrationParameters = parameters;
			calibrationPositions = positions;
		}

		public void Calibrate()
		{
			Calibrate(this.calibrationPositions);
		}

		public void Calibrate(List<Point> positions)
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
				screenX[i] = calibrationParameters.calibrationPoints[calibrationParameters.calibrationSequence[i]].X;
				screenY[i] = calibrationParameters.calibrationPoints[calibrationParameters.calibrationSequence[i]].Y;
			}

			//
			xInterpolator = RBF2D.LeaveOneOutSelect(pupilX, pupilY, screenX, calibrationParameters.baseRadiusRange, calibrationParameters.numBaseRadii,
													calibrationParameters.numLayersRange, calibrationParameters.regularizerRange, calibrationParameters.numRegularizers,
													calibrationParameters.logSpaceRegularizers);
			yInterpolator = RBF2D.LeaveOneOutSelect(pupilX, pupilY, screenY, calibrationParameters.baseRadiusRange, calibrationParameters.numBaseRadii,
													calibrationParameters.numLayersRange, calibrationParameters.regularizerRange, calibrationParameters.numRegularizers,
													calibrationParameters.logSpaceRegularizers);
		}
	}
}
