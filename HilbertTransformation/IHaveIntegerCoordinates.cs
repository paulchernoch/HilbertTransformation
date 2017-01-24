using System;
using System.Collections.Generic;

namespace HilbertTransformation
{
	/// <summary>
	/// A vector for which one can get integer coordinate values and compute the largest value across all of its dimensions.
	/// This mapping may require scaling, translating, or loss of precision.
	/// </summary>
	public interface IHaveIntegerCoordinates
	{
		/// <summary>
		/// Largest value among all coordinates.
		/// </summary>
		int Range();

		/// <summary>
		/// Number of dimensions.
		/// </summary>
		int GetDimensions();

		IEnumerable<int> GetCoordinates();
	}
}
