using System;

namespace HilbertTransformationTests.Data
{
    /// <summary>
    /// Interpolates values using quadratic Lagrange Interpolating polynomials.
    /// </summary>
    public class LagrangeQuadraticInterpolator: Interpolator<double>
    {
        private double X0 { get; set; }
        private double X1 { get; set; }
        private double X2 { get; set; }
        private double C0 { get; set; }
        private double C1 { get; set; }
        private double C2 { get; set; }

        public double Y(double x) => Interpolate(x);

        /// <summary>
        /// Construct a quadratic polynomial given three points that lie on the curve, which must have distinct x-values.
        /// </summary>
        /// <param name="x0">X-coordinate of first point.</param>
        /// <param name="y0">Y-coordinate of first point.</param>
        /// <param name="x1">X-coordinate of second point.</param>
        /// <param name="y1">Y-coordinate of second point.</param>
        /// <param name="x2">X-coordinate of third point.</param>
        /// <param name="y2">Y-coordinate of third point.</param>
        public LagrangeQuadraticInterpolator(double x0, double y0, double x1, double y1, double x2, double y2)
        {
            X0 = x0;
            X1 = x1;
            X2 = x2;
            C0 = y0 / ((x0 - x1) * (x0 - x2));
            C1 = y1 / ((x1 - x0) * (x1 - x2));
            C2 = y2 / ((x2 - x0) * (x2 - x1));
        }

        /// <summary>
        /// Interpolate an approximate Y value corresponding to the given X value, using a Lagrange quadratic interpolating polynomial.
        /// </summary>
        /// <param name="x">X value.</param>
        /// <returns>Interpolated Y value.</returns>
        private double Interpolate(double x)
        {
            var d0 = x - X0;
            var d1 = x - X1;
            var d2 = x - X2;
            return C0 * d1 * d2 + C1 * d0 * d2 + C2 * d0 * d1;
        }
    }
}
