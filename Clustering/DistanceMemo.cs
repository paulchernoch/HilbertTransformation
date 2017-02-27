using System;
using System.Collections.Generic;
using System.Linq;
using HilbertTransformation;

namespace Clustering
{
	/// <summary>
	/// Record square distances between points so they may be reused later, trading space for time.
	/// 
	/// Only memoize short distances, since it is mostly near neighbors that we care about studying.
	/// </summary>
	/// <remarks>
	/// When testing the effects of varying the WindowRadius on the accuracy of DensityMeter or some other
	/// purpose, start with a higher value of WindowRadius and then decrease it, since that will require
	/// that fewer distance comparisons be repeated.
	/// </remarks>
	public class DistanceMemo
	{
		/// <summary>
		/// Remember for reuse the distance from some points to other points.
		/// 
		/// Only square distances that do not exceed NeighborhoodRadius are recorded, in order to save on 
		/// memory.
		/// 
		/// The index into the array is the point's position in the Hilbert ordering in the Index.
		/// The key in each dictionary is index of a second point. 
		/// The value in the dictionary is the square distance between the two indicated points.
		/// </summary>
		Dictionary<int, long>[] Distances { get; set; }

		/// <summary>
		/// For every point, this records whether distances to all points have been measured or not.
		/// 
		/// The index is the position of the point in the Index in Hilbert order.
		/// </summary>
		bool[] AllMeasured { get; set; }

		/// <summary>
		/// Only distances smaller than this value will be recorded.
		/// 
		/// This value should be chosen such that only a few percent of point pairs have a distance that is smaller,
		/// otherwise the memory usage will be significant.
		/// 
		/// A good value to use is a small multiple of the MaximumSquareDistance derived using a ClusterCounter.
		/// </summary>
		public long NeighborhoodRadius { get; private set; }

		private int _windowRadius = 0;
		/// <summary>
		/// Gets/sets the window radius. When counting a point's neighbors in a window, it is compared
		/// to this many other points on each side of it in along the HilbertIndex, or 2*WindowRadius points in all.
		/// 
		/// If this radius is increased, AllInWindowMeasured is set to false, until such time as the distances to 
		/// more neighbors are measured.
		/// </summary>
		public int WindowRadius { 
			get { return _windowRadius; }
			set { 
				if (value > _windowRadius) AllInWindowMeasured = false;
				_windowRadius = value;
			} 
		}

		/// <summary>
		/// Once every point has been compared to every point in its window, this is set to true.
		/// 
		/// If the WindowRadius is subsequently increased, this will be set to false.
		/// </summary>
		/// <value><c>true</c> if the distance to all points in the window for every point have been measured; otherwise, <c>false</c>.</value>
		bool AllInWindowMeasured { get; set; } = false;

		/// <summary>
		/// Order the points whose distances are to be measured.
		/// 
		/// A point's position in SortedOrder will be used to identify it in the Distances array and the AllMeasured array.
		/// </summary>
		public HilbertIndex Index { get; private set; }

		/// <summary>
		/// Number of points in the index.
		/// </summary>
		int Count { get { return Index.SortedPoints.Count; } }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Clustering.DistanceMemo"/> class.
		/// </summary>
		/// <param name="index">Index of points whose distances will be measured and rememerbed.</param>
		/// <param name="neighborhoodRadius">We will only memoize distances that are less than or equal to this value.
		/// Larger distances will need to be recomputed.</param>
		public DistanceMemo(HilbertIndex index, long neighborhoodRadius, int windowRadius = 0)
		{
			Index = index;
			NeighborhoodRadius = neighborhoodRadius;
			Distances = new Dictionary<int, long>[Count];
			AllMeasured = new bool[Count];
			if (windowRadius <= 0)
				windowRadius = (int) Math.Sqrt(Count/2);
			WindowRadius = windowRadius;
		}

		/// <summary>
		/// Measure the square distance between two points specified by their position in the Index.
		/// 
		/// Possibly reuse a previously computed and recorded measure.
		/// Possibly record the distance if it was not previously computed.
		/// </summary>
		/// <param name="iPoint1">Position in the Index of the first point to compare.</param>
		/// <param name="iPoint2">Position in the Index of the second point to compare.</param>
		/// <param name="limitToNeighborhood">If false, return the correct square distance in all cases, and record its value
		/// if it does not exceed the NeighborhoodRadius and has not yet been recorded.
		/// If true and the distance has already been measured and recorded, return the recorded (and correct) square distance.
		/// If true and the distance has not yet been measured and AllMeasured is not set for either point,
		/// compute and return the proper square distance and record it if it does not exceed the NeighborhoodRadius.
		/// Otherwise, AllMeasured is true for one of the points and the value was not recorded because it exceeds NeighborhoodRadius,
		/// therefore return long.MaxValue.
		/// </param>
		public long Measure(int iPoint1, int iPoint2, bool limitToNeighborhood = false)
		{
			long measure;
			EnsureStorage(iPoint1);
			if (Distances[iPoint1].TryGetValue(iPoint2, out measure))
				return measure;
			EnsureStorage(iPoint2);
			if (!limitToNeighborhood)
				measure = MeasureAndRecord(iPoint1, iPoint2);
			else if (AllMeasured[iPoint1] || AllMeasured[iPoint2])
			{
				// Because we assert that all distances have been computed for at least one of the points
				// and we did not record the value, it must exceed NeighborhoodRadius.
				// Since limitToNeighborhood is false, we do not need to compute the value and can return MaxValue.
				measure = long.MaxValue;
			}
			else {
				// We may or may not have computed the value before, so it may be already computed and greater than NeighborhoodRadius,
				// hence not recorded and we have to compute it again, or we may not have computed it yet and its 
				// alue may be anything.
				measure = MeasureAndRecord(iPoint1, iPoint2);
			}
			return measure;
		}

		/// <summary>
		/// Measure the square distance between the specified points, possibly reusing a memoized value.
		/// </summary>
		/// <param name="point1">Point1.</param>
		/// <param name="point2">Point2.</param>
		/// <param name="limitToNeighborhood">If false, return the correct square distance in all cases, and record its value
		/// if it does not exceed the NeighborhoodRadius and has not yet been recorded.
		/// If true and the distance has already been measured and recorded, return the recorded (and correct) square distance.
		/// If true and the distance has not yet been measured and AllMeasured is not set for either point,
		/// compute and return the proper square distance and record it if it does not exceed the NeighborhoodRadius.
		/// Otherwise, AllMeasured is true for one of the points and the value was not recorded because it exceeds NeighborhoodRadius,
		/// therefore return long.MaxValue.
		/// </param>
		public long Measure(HilbertPoint point1, HilbertPoint point2, bool limitToNeighborhood = false)
		{
			return Measure(Index.SortedPosition(point1), Index.SortedPosition(point2), limitToNeighborhood);
		}

		long MeasureAndRecord(int iPoint1, int iPoint2)
		{
			var measure = Index.SortedPoints[iPoint1].Measure(Index.SortedPoints[iPoint2]);
			if (measure <= NeighborhoodRadius)
			{
				Distances[iPoint1][iPoint2] = measure;
				Distances[iPoint2][iPoint1] = measure;
			}
			return measure;
		}

		/// <summary>
		/// Mark a point as being AllMeasured, meaning that we have measured the distance from that point to all other points 
		/// and recorded the smaller distances of interest.
		/// </summary>
		/// <param name="point">Point.</param>
		public void Complete(HilbertPoint point)
		{
			AllMeasured[Index.SortedPosition(point)] = true;
		}

		void EnsureStorage(int iPoint)
		{
			if (Distances[iPoint] == null)
				Distances[iPoint] = new Dictionary<int, long>();
		}

		/// <summary>
		/// Count how many neighbors are near the given point, within the NeighborhoodRadius.
		/// </summary>
		/// <param name="point">Point whose neighbors are to be counted.</param>
		/// <param name="allNeighbors">If false, only return the number of neighbors already known due to previous measurements.
		/// If true, make sure we measure the distance from this point to all other points, but still reuse
		/// any already computed distances.</param>
		public int Neighbors(HilbertPoint point, bool allNeighbors = true)
		{
			var iPoint1 = Index.SortedPosition(point);
			if (!AllMeasured[iPoint1] && allNeighbors)
			{
				for (var iPoint2 = 0; iPoint2 < Count; iPoint2++)
				{
					// If all distances have already been computed for iPoint2, 
					// then we do not need to recompute that paricular distance.
					if (iPoint1 != iPoint2 && !AllMeasured[iPoint2])
						Measure(iPoint1, iPoint2, true);
				}
				Complete(point);
			}
			return Distances[iPoint1].Count;
		}

		/// <summary>
		/// Count the number of Neighbors this point has the in window to either side of it along the Hilbert curve.
		/// 
		/// These neighbors must be no farther away than the NeighborhoodRadius.
		/// These neighbors must be in the window to either side of the given point along the Hilbert curve.
		/// </summary>
		/// <returns>Count of neighbors.</returns>
		/// <param name="point">Point whose neighbors are to be counted.</param>
		public int NeighborsInWindow(HilbertPoint point)
		{
			MeasureWindow();
			var iPoint1 = Index.SortedPosition(point);
			var center = iPoint1;
			if (center < WindowRadius)
				center = WindowRadius;
			else if (center > Count - WindowRadius - 1)
				center = Count - WindowRadius - 1;

			var start = center - WindowRadius;
			var stop = center + WindowRadius;
			return Distances[iPoint1].Keys.Count(i => i >= start && i <= stop);

			// If we wanted all points in the neighborhood, not just in the window, we would do this:
			//   return Distances[iPoint1].Count; 
			// Why don't we? Though in many cases it may lead to a mo accurate value for some points,
			// it would worsen the correlation. If most points undercount, and some are accurate, that
			// would be inferior.
		}

		/// <summary>
		/// Compute the distance from every point to every point within a window around itself in the HilbertIndex.
		/// Then record that this has been done, so that subsequent calls to NeighborsInWindow don't have to do any work.
		/// </summary>
		private void MeasureWindow()
		{
			if (AllInWindowMeasured)
				return;
			for (var i = 0; i < Count - 1; i++)
			{
				var extra = 0;
				if (i < WindowRadius)
					extra = WindowRadius - i;
				var limit = Math.Min(i + WindowRadius + 1 + extra, Count);
				for (var j = i + 1; j < limit; j++)
				{
					Measure(i, j, true);
				}
			}
			AllInWindowMeasured = true;
		}
	}
}
