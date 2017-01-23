using System;
using System.Diagnostics;
using NUnit.Framework;
using System.Linq;

namespace HilbertTransformationTests
{
	[TestFixture]
	public class CartesianDistanceTests
	{

		[Test]
		public void SquareDistanceBenchmark()
		{
			var dims = 2000;
			var x = new uint[dims];
			var y = new uint[dims];
			var xMag2 = 0L;
			var yMag2 = 0L;

			for (var i = 0; i < dims; i++)
			{
				x[i] = (uint)i;
				xMag2 += x[i] * (long)x[i];
				y[i] = (uint)(10000 - i);
				yMag2 += y[i] * (long)y[i];
			}
			var xMax = (long)x.Max();
			var yMax = (long)y.Max();
			var repetitions = 100000;
			var naiveTime = Time(() => SquareDistanceNaive(x, y), repetitions);
			var distributeTime = Time(() => SquareDistanceDistributed(x, y), repetitions);
var branchTime = Time(() => SquareDistanceBranching(x, y), repetitions);
			var dotProductTime = Time(() => SquareDistanceDotProduct(x, y, xMag2, yMag2, xMax, yMax), repetitions);

			Console.Write($@"
For {repetitions} iterations and {dims} dimensions. 
    Naive time        = {naiveTime} sec. 
    Branch time       = {branchTime} sec. 
    Distributed time  = {distributeTime} sec.
    Dot Product time  = {dotProductTime} sec.
    Improve vs Naive  = {((int)(10000 * (naiveTime - dotProductTime) / naiveTime)) / 100.0}%.
    Improve vs Branch = {((int)(10000 * (branchTime - dotProductTime) / branchTime)) / 100.0}%.
");
			Assert.Less(dotProductTime, branchTime, "Dot product time should have been less than branch time");
		}

		private static double Time(Action action, int repeatCount)
		{
			var timer = new Stopwatch();
			timer.Start();
			for (var j = 0; j < repeatCount; j++)
				action();
			timer.Stop();
			return timer.ElapsedMilliseconds / 1000.0;
		}

		private static long SquareDistanceNaive(uint[] x, uint[] y)
		{
			var squareDistance = 0L;
			for (var i = 0; i < x.Length; i++)
			{
				var delta = (long)x[i] - (long)y[i];
				squareDistance += delta * delta;
			}
			return squareDistance;
		}

		/// <summary>
		/// Compute the square distance, using ternary operators for branching to keep subtraction operations from going negative,
		/// which is inappropriate for unsigned numbers.
		/// </summary>
		/// <returns>The distance branching.</returns>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		private static long SquareDistanceBranching(uint[] x, uint[] y)
		{
			long squareDistanceLoopUnrolled;

			// Unroll the loop partially to improve speed. (2.7x improvement!)
			var distance = 0UL;
			var leftovers = x.Length % 4;
			var dimensions = x.Length;
			var roundDimensions = dimensions - leftovers;

			for (var i = 0; i < roundDimensions; i += 4)
			{
				var x1 = x[i];
				var y1 = y[i];
				var x2 = x[i + 1];
				var y2 = y[i + 1];
				var x3 = x[i + 2];
				var y3 = y[i + 2];
				var x4 = x[i + 3];
				var y4 = y[i + 3];
				var delta1 = x1 > y1 ? x1 - y1 : y1 - x1;
				var delta2 = x2 > y2 ? x2 - y2 : y2 - x2;
				var delta3 = x3 > y3 ? x3 - y3 : y3 - x3;
				var delta4 = x4 > y4 ? x4 - y4 : y4 - x4;
				distance += delta1 * delta1 + delta2 * delta2 + delta3 * delta3 + delta4 * delta4;
			}
			for (var i = roundDimensions; i < dimensions; i++)
			{
				var xi = x[i];
				var yi = y[i];
				var delta = xi > yi ? xi - yi : yi - xi;
				distance += delta * delta;
			}
			squareDistanceLoopUnrolled = (long)distance;

			return squareDistanceLoopUnrolled;
		}

		private static long SquareDistanceDistributed(uint[] x, uint[] y)
		{
			long squareDistanceLoopUnrolled;

			// Unroll the loop partially to improve speed. (2.7x improvement!)
			var distance = 0UL;
			var dSubtract = 0UL;
			var leftovers = x.Length % 4;
			var dimensions = x.Length;
			var roundDimensions = dimensions - leftovers;

			for (var i = 0; i < roundDimensions; i += 4)
			{
				ulong x1 = x[i];
				ulong y1 = y[i];
				ulong x2 = x[i + 1];
				ulong y2 = y[i + 1];
				ulong x3 = x[i + 2];
				ulong y3 = y[i + 2];
				ulong x4 = x[i + 3];
				ulong y4 = y[i + 3];

				distance += x1 * x1 + y1 * y1 
						  + x2 * x2 + y2 * y2 
						  + x3 * x3 + y3 * y3 
					      + x4 * x4 + y4 * y4;
				dSubtract += x1 * y1 + x2 * y2 + x3 * y3 + x4 * y4;
			}
			distance = distance - 2UL * dSubtract;
			for (var i = roundDimensions; i < dimensions; i++)
			{
				var xi = x[i];
				var yi = y[i];
				var delta = xi > yi ? xi - yi : yi - xi;
				distance += delta * delta;
			}
			squareDistanceLoopUnrolled = (long)distance;

			return squareDistanceLoopUnrolled;
		}

		private static long SquareDistanceDotProduct(uint[] x, uint[] y, long xMag2, long yMag2, long xMax, long yMax)
		{
			const int unroll = 4;
			if (xMax * yMax * unroll < (long) uint.MaxValue)
				return SquareDistanceDotProductNoOverflow(x, y, xMag2, yMag2);

			// Unroll the loop partially to improve speed. (2.7x improvement!)
			var dotProduct = 0UL;
			var leftovers = x.Length % unroll;
			var dimensions = x.Length;
			var roundDimensions = dimensions - leftovers;

			for (var i = 0; i < roundDimensions; i += unroll)
			{
				var x1 = x[i];
				ulong y1 = y[i];
				var x2 = x[i + 1];
				ulong y2 = y[i + 1];
				var x3 = x[i + 2];
				ulong y3 = y[i + 2];
				var x4 = x[i + 3];
				ulong y4 = y[i + 3];
				dotProduct += x1 * y1 + x2 * y2 + x3 * y3 + x4 * y4;
			}
			for (var i = roundDimensions; i < dimensions; i++)
				dotProduct += x[i] * (ulong)y[i];
			return xMag2 + yMag2 - 2L * (long)dotProduct;
		}

		/// <summary>
		/// Compute the square of the Cartesian distance using the dotproduct method,
		/// assuming that calculations wont overflow uint.
		/// 
		/// This permits us to skip some widening conversions to ulong, making the computation faster.
		/// 
		/// Algorithm:
		/// 
		///    2         2       2
		///   D    =  |x|  +  |y|  -  2(x·y)
		/// 
		/// Using the dot product of x and y and precomputed values for the square magnitudes of x and y
		/// permits us to use two operations (multiply and add) instead of three (subtract, multiply and add)
		/// in the main loop, saving one third of the time.
		/// </summary>
		/// <returns>The square distance.</returns>
		/// <param name="x">First point.</param>
		/// <param name="y">Second point.</param>
		/// <param name="xMag2">Distance from x to the origin, squared.</param>
		/// <param name="yMag2">Distance from y to the origin, squared.</param>
		private static long SquareDistanceDotProductNoOverflow(uint[] x, uint[] y, long xMag2, long yMag2)
		{
			// Unroll the loop partially to improve speed. (2.7x improvement!)
			const int unroll = 4;
			var dotProduct = 0UL;
			var leftovers = x.Length % unroll;
			var dimensions = x.Length;
			var roundDimensions = dimensions - leftovers;
			for (var i = 0; i < roundDimensions; i += unroll)
				dotProduct += (x[i] * y[i] + x[i+1] * y[i+1] + x[i+2] * y[i+2] + x[i+3] * y[i+3]);
			for (var i = roundDimensions; i < dimensions; i++)
				dotProduct += x[i] * y[i];
			return xMag2 + yMag2 - 2L * (long)dotProduct;
		}


	}

}
