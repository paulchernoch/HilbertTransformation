using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertTransformationTests.Data
{
    /// <summary>
    /// Given two points with different X values and a horizontal asymptote at positive infinity,
    /// derive the coefficients of a hyperbola that passes through those points and use it to interpolate
    /// Y values for other X values.
    /// 
    /// This works only for those hyperbolas that can be expressed in the form:
    /// 
    ///          Ax + B
    ///     y = --------
    ///           x + C
    ///           
    /// where A is the horizontal asymptote.
    /// </summary>
    public class HyperbolicInterpolator : Interpolator<double>
    {
        /// <summary>
        /// Horizontal asymptote, the limit of Y as x → ∞
        /// </summary>
        private double A { get; set; }
        private double B { get; set; }

        /// <summary>
        /// There is a vertical asymptote at y = -C.
        /// </summary>
        private double C { get; set; }

        /// <summary>
        /// If true, invert the result of the Y() method by assuming that the caller is supplying the y-value and wants the x-value back.
        /// </summary>
        private bool Invert { get; set; }

        /// <summary>
        /// Build an interpolator for the hyperbola that passes through the two given points and has the given
        /// horizontal asymptote at positive infinity.
        /// </summary>
        /// <param name="x0">X-coordinate of first point through which the hyperbola passes.</param>
        /// <param name="y0">Y-coordinate of first point through which the hyperbola passes.</param>
        /// <param name="x1">X-coordinate of second point through which the hyperbola passes.</param>
        /// <param name="y1">Y-coordinate of second point through which the hyperbola passes.</param>
        /// <param name="horizontalAsymptote">Horizontal asymptote, the limit of Y as x → ∞</param>
        public HyperbolicInterpolator(double x0, double y0, double x1, double y1, double horizontalAsymptote, bool invert = false)
        {
            A = horizontalAsymptote;
            C = (x0 * (y0 - A) - x1 * (y1 - A)) / (y1 - y0);
            B = x0 * (y0 - A) + y0 * (x0 * (y0 - A) - x1 * (y1 - A)) / (y1 - y0);
            Invert = invert;
        }

        /// <summary>
        /// If Invert is false, this returns the interpolated value of Y for the given X.
        /// Otherwise, it returns the interpolated value of X for the given Y.
        /// </summary>
        /// <param name="x">Independent parameter X.</param>
        /// <returns>Interpolated value that falls on the hyperbola.</returns>
        public double Y(double x)
        {
            double y;
            if (Invert)
                y = (B - C * x) / (x - A);
            else
                y = (A * x + B) / (x + C);
            return y;
        }
    }
}
