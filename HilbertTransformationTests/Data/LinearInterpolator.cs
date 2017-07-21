using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertTransformationTests.Data
{
    /// <summary>
    /// Given two points with different X values that define a line,
    /// perform linear interpolation of a different Y value given an X value.
    /// </summary>
    public class LinearInterpolator : Interpolator<double>
    {
        private double X0 { get; set; }
        private double Y0 { get; set; }
        private double X1 { get; set; }
        private double Y1 { get; set; }

        public LinearInterpolator(double x0, double y0, double x1, double y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        /// <summary>
        /// Interpolated value of Y for the given X.
        /// </summary>
        /// <param name="x">Independent parameter X.</param>
        /// <returns>Linearly interpolated value.</returns>
        public double Y(double x)
        {
            return Y0 + (Y1 - Y0) * (x - X0) / (X1 - X0);
        }
    }
}
