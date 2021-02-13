using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyetracking
{
	/// <summary>
	/// Performs the actual math for mapping between eyetracking video space to stimulus frames space
	/// </summary>
	class Calibrator
	{

	}


	/// <summary>
	/// Object-oriented wrapper to alglib's hierarchical RBF functions.
	/// Specifically maps a 2D values to a scalar
	/// </summary>
	class RBF2D
	{
		/// <summary>
		/// Performs leave-one-out selection of best hyperparameter values for the RBF
		/// </summary>
		/// <param name="x">data X</param>
		/// <param name="y">data Y</param>
		/// <param name="value">data values at (X, Y)</param>
		/// <param name="baseRadiusRange">range of values for the base radius, must > 0</param>
		/// <param name="numBaseRadii">number of values for the base radius</param>
		/// <param name="numLayersRange">number of layers, must be > 0</param>
		/// <param name="regularizerRange">range of regularizers to use, must be >= 0</param>
		/// <param name="numRegularizers">number of regularizers</param>
		/// <param name="logSpaceRegularizers">log space instead of linearly space regularizers?</param>
		/// <returns>RBF fit on all of the data using the parameter that generated the lowest sum of squares loss</returns>
		public static RBF2D LeaveOneOutSelect(double[] x, double[] y, double[] value, Tuple<double, double> baseRadiusRange, int numBaseRadii, 
											  Tuple<int, int> numLayersRange, Tuple<double, double> regularizerRange, int numRegularizers, bool logSpaceRegularizers)
		{
			return null;
		}

		private alglib.rbfmodel RBFModel;
		private alglib.rbfreport RBFReport;

		private double[] dataX;
		private double[] dataY;
		private double[] dataValue;

		public Nullable<double> baseRadius = null;
		public int numLayers = 5;
		public double regularizer = 0;

		public RBF2D()
		{
			alglib.rbfcreate(2, 1, out RBFModel);
		}

		public RBF2D(double[] x, double[] y, double[] value)
		{
			alglib.rbfcreate(2, 1, out RBFModel);
			dataX = x;
			dataY = y;
			dataValue = value;
		}

		public void Fit()
		{
			Fit(dataX, dataY, dataValue);
		}

		public void Fit(double[] x, double[] y, double[] value)
		{
			dataX = x;
			dataY = y;
			dataValue = value;

			// alglib expects a 2D array in [sample, x-y-value]
			double[,] formattedData = new double[dataX.Length, 3];
			for (int i = 0; i < dataX.Length; i++)
			{
				formattedData[i, 0] = dataX[i];
				formattedData[i, 1] = dataY[i];
				formattedData[i, 2] = dataValue[i];
			}
			alglib.rbfsetpoints(RBFModel, formattedData);

			// this call sets the algorithms and have hyperparameters that need tuning
			alglib.rbfsetalgohierarchical(RBFModel, 1.0, 3, 0.0);

			alglib.rbfbuildmodel(RBFModel, out RBFReport);
		}

		public double Evaluate(double x, double y)
		{
			return alglib.rbfcalc2(RBFModel, x, y);
		}
	}
}
