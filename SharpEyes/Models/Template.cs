using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Eyetracking
{
	/// <summary>
	/// Struct encapsulating an image template to search for
	/// Include information about the center of the object in the template,
	/// which may be different from the center of the image if the template
	/// was clipped from the edge of an image
	/// </summary>
	[Serializable()]
	public readonly struct Template
	{
		public readonly Mat Image;

		// X, Y center of the object in the coordinate frame of this template
		public readonly double X;
		public readonly double Y;
		public int Width => Image.Width;
		public int Height => Image.Height;

		// Radius of object, can be set to null if not needed
		public readonly double? Radius;

		// Mean brightness of the pupil
		public double MeanPupilBrightness
		{
			get
			{
				// take only the center of the pupil
				int left = Width / 2 - Width / 6;
				int right = left + Width / 3;
				int top = Height / 2 - Height / 6;
				int bottom = top + Height / 3;
				return (Image.SubMat(left, right, top, bottom).Sum().ToDouble() / (Width * Height));
			}
		}

		// Mean brightness of the window used during eyetracking
		// for better blink detection
		public readonly double MeanWindowBrightness;

		public Template(Mat image, double? radius, double windowBrightness = 0)
		{
			Image = image;
			Radius = radius;
			MeanWindowBrightness = windowBrightness;
			X = (double)image.Width / 2;
			Y = (double)image.Height / 2;
		}

		public Template(Mat image, double? radius, double x, double y, double windowBrightness = 0)
		{
			if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
				throw new ArgumentOutOfRangeException();

			Image = image;
			Radius = radius;
			MeanWindowBrightness = windowBrightness;

			X = x;
			Y = y;
		}
	}
}
