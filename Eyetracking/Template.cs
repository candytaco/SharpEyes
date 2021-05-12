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
	public readonly struct Template
	{
		public readonly Mat Image;

		// X, Y center of the object in the coordinate frame of this template
		public readonly int X;
		public readonly int Y;
		public int Width => Image.Width;
		public int Height => Image.Height;

		public Template(Mat image)
		{
			Image = image;
			X = image.Width / 2;
			Y = image.Height / 2;
		}

		public Template(Mat image, int x, int y)
		{
			Image = image;

			if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
				throw new ArgumentOutOfRangeException();

			X = x;
			Y = y;
		}
	}
}
