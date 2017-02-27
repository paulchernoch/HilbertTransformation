using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;

namespace HilbertTransformationTests
{
	/// <summary>
	/// Compute the Kendall Tau Correlation of two orderings of values, a non-parametric correlation that compares the ranking
	/// of value, not the values themselves.
	/// 
	/// The correlation of the measures is one if all values are in the same order when sorted by two different measures.
	/// The correlation is minus one if the second ordering is the reverse of the first.
	/// The correlation is zero if the values are completely uncorrelated.
	/// 
	/// Two algorithms are provided: TauA and TauB. TauB accounts properly for duplicate values (ties), unlike TauA.
	/// </summary>
	public class KendallTauCorrelation<T, C> where C : IComparable<C>
	{
		private Func<T, C> Measure1 { get; }
		private Func<T, C> Measure2 { get; }

		public KendallTauCorrelation(Func<T, C> measure1, Func<T, C> measure2)
		{
			Measure1 = measure1;
			Measure2 = measure2;
		}

		/// <summary>
		/// Compute the Tau-a rank correlation, which is suitable if there are no ties in rank.
		/// </summary>
		/// <returns>A value between -1 and 1. 
		/// If the measures are ranked the same by both measures, returns 1.
		/// If the measures are ranked in exactly opposite order, return -1.
		/// The more items that are out of sequence, the lower the score.
		/// If the measures are completely uncorrelated, returns zero.
		/// </returns>
		/// <param name="data">Data to be ranked according to two measures and then correlated.</param>
		public double TauA(IList<T> data)
		{
			var ranked = data
					 .OrderBy(Measure1)
					 .Select((item, index) => new { Data = item, Rank1 = index + 1 })
					 .OrderBy(pair => Measure2(pair.Data))
					 .Select((pair, index) => new { pair.Rank1, Rank2 = index + 1 })
					 .ToList();
			var numerator = 0;

			var n = ranked.Count;
			var denominator = n * (n - 1) / 2.0;
			for (var i = 1; i < n; i++)
				for (var j = 0; j < i; j++)
				{
					numerator += Sign(ranked[i].Rank1 - ranked[j].Rank1)
							   * Sign(ranked[i].Rank2 - ranked[j].Rank2);
				}
			return numerator / denominator;
		}

		/// <summary>
		/// Compute the Tau-b correlation, which accounts for ties.
		/// 
		///             n  - n
		///              c    d
		///  τ  = -----------------------
		///   b    _____________________
		///       / (n  -  n )(n  -  n )
		///      √    0     1   0     2
		/// 
		/// where:
		///        n0 = n(n-1)/2
		///               
		///        n1 =  Σ  t (t - 1)/2
		///              i   i  i
		/// 
		///        n2 =  Σ  t (t - 1)/2
		///              j   j  j
		/// 
		///      t[i] = # of ties for the ith group according to measure 1.
		///      t[j] = # of ties for the jth group according to measure 2.
		///        nc = # of concordant pairs
		///        nd = # of discordant pairs
		/// </summary>
		/// <returns>A correlation value between -1 (perfect reverse correlation)
		///  and +1 (perfect correlation). 
		/// Zero means uncorrelated. </returns>
		/// <param name="data">Data.</param>
		public double TauB(IEnumerable<T> data)
		{
			// Compute two Ranks by sorting first by Measure1 and then by Measure2.
			// Group by like values of each in order to handle ties.
			var ranked = data.Select(item => new { M1 = Measure1(item), M2 = Measure2(item) })
				.GroupBy(measures => new { measures.M1 })
				.OrderBy(@group => @group.First().M1)
				.ThenBy(@group => @group.First().M2)
				.AsEnumerable()
				.Select((@group, groupIndex) => new
				{
					Measure1Ranked = @group.Select((measure, index) => new { measure.M1, measure.M2 }),
					Rank = ++groupIndex
				})
				.SelectMany(v => v.Measure1Ranked, (s, i) => new
				{
					i.M1,
					i.M2,
					DenseRank1 = s.Rank
				})
				.GroupBy(measures => new { measures.M2 })
				.OrderBy(@group => @group.First().M2)
				.ThenBy(@group => @group.First().M1)
				.AsEnumerable()
				.Select((@group, groupIndex) => new
				{
					Measure2Ranked = @group.Select((measure, index) => new { measure.M1, measure.M2, measure.DenseRank1 }),
					Rank = ++groupIndex
				})
				.SelectMany(v => v.Measure2Ranked, (s, i) => new { i.M1, i.M2, i.DenseRank1, DenseRank2 = s.Rank })
				.ToArray();
			if (ranked.Length <= 1)
				return 0; // No data or one data point. Impossible to establish correlation.

			// Now that we have ranked the data, compute the correlation.
			var n = ranked.Count();
			var n0 = n * (n - 1) / 2;
			var n1 = 0;
			var n2 = 0;
			var numerator = 0; // Stores nc - nd as a single value, rather than computing them separately.
			for (var i = 1; i < n; i++)
				for (var j = 0; j < i; j++)
				{
					var iRanked = ranked[i];
					var jRanked = ranked[j];
					numerator += Sign(iRanked.DenseRank1 - jRanked.DenseRank1)
							   * Sign(iRanked.DenseRank2 - jRanked.DenseRank2);
					// Keep track of ties. Because we are running the indices in a triangle,
					// we automatically get this for n1 and n2: ties * (ties - 1) / 2
					if (iRanked.M1.CompareTo(jRanked.M1) == 0)
						n1++;
					if (iRanked.M2.CompareTo(jRanked.M2) == 0)
						n2++;
				}
			if (n0 == n1 || n0 == n2)
				return 0; // All ties, so everything as the same rank.
						  // Observe that if n1 = n2 = 0, that this formula is identical to Tau-a.
			return numerator / Sqrt((double)(n0 - n1) * (n0 - n2));
		}
	}
}
