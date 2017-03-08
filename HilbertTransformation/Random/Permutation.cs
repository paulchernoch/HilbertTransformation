using System;
using System.Collections.Generic;
using System.Linq;

namespace HilbertTransformation.Random
{
	/// <summary>
	/// Defines a one-to-one permutation of dimensional indices that can be used to scramble the order of items in a list.
	/// 
	/// A permuted vector has all the same values but in a different order.
	/// </summary>
	public class Permutation<T>
	{

		/// <summary>
		/// Gets the mapping array used to transform the ordering of values in a list.
		/// The transformation makes use of the Mapping using the following rule: 
		/// 
		///   target[i] = source[Mapping[i]]
		/// 
		/// The array must contain all the integers from zero to N-1 exactly once, but they may be in any order.
		/// If Mapping[i] equals i, the transform is an identity that will not change the list if applied.
		/// </summary>
		public int[] Mapping { get; private set; }

		public int Dimensions { get { return Mapping.Length; } }

		#region Constructors, Factory methods and Validation

		/// <summary>
		/// Create either an identity transformation or a randomly generated one.
		/// </summary>
		/// <param name="dimensions">Dimensions.</param>
		/// <param name="randomize">If false,  create an identity transformation that, if applied, would yield a new list 
		/// with values in the same order as the input.
		/// If true, create a random permutation containing all the numbers zero to dimensions-1 in a random order.
		/// </param>
		public Permutation(int dimensions, bool randomize = false)
		{
			Mapping = new int[dimensions];
			if (!randomize)
			{
				// Create an identity mapping.
				for (var i = 0; i < dimensions; i++)
					Mapping[i] = i;
			}
			else {
				Mapping = dimensions.CreateRandomPermutation();
			}
		}

		/// <summary>
		/// Create and validate the given mapping.
		/// </summary>
		/// <param name="mapping">Mapping list, which must contain all the numbers zero to N-1 exactly once.</param>
		public Permutation(IList<int> mapping)
		{
			Validate(mapping);
			Mapping = mapping.ToArray();
		}

		/// <summary>
		/// Convert a Permutation that expects one type of input to one that expects a different type of input,
		/// but will reorder coordinates the same way.
		/// </summary>
		/// <param name="original">Original permutation.</param>
		/// <typeparam name="TNew">Type of input data for new Permutation.</typeparam>
		public static Permutation<TNew> Convert<TNew>(Permutation<T> original) 
		{
			return new Permutation<TNew>(original.Mapping);
		}

		/// <summary>
		/// Throw an exception if the mapping has values out of range or duplicates.
		/// </summary>
		/// <param name="mapping">Mapping list, which must contain all the numbers zero to N-1 exactly once.</param>
		public static void Validate(IList<int> mapping)
		{
			var dimensions = mapping.Count();
			var found = new bool[dimensions];
			foreach (var i in mapping)
			{
				if (i < 0 || i >= dimensions)
					throw new ArgumentOutOfRangeException(nameof(mapping), $"Index must be in the range zero to {dimensions-1}");
				if (found[i])
					throw new ArgumentException("Index occurs more than once", nameof(mapping));
				found[i] = true;
			}
		}

		#endregion

		#region Apply the Permutation Transformation

		/// <summary>
		/// Transform the source into the target by permuting the order of items according to the mapping.
		/// </summary>
		/// <param name="source">Source data before the transformation.</param>
		/// <returns>Transformed array of data taken from the source but rearranged.</returns>
		public T[] ApplyToArray(IList<T> source)
		{
			var dimensions = source.Count();
			if (dimensions != Dimensions)
				throw new ArgumentException("Permutation and data have different dimensionality", nameof(source));
			var target = new T[dimensions];
			for (var i = 0; i < dimensions; i++)
				target[i] = source[Mapping[i]];
			return target;
		}

		/// <summary>
		/// Transform the source into the target by permuting the order of items according to the mapping.
		/// </summary>
		/// <param name="source">Source data before the transformation.</param>
		/// <returns>Transformed list of data taken from the source but rearranged.</returns>
		public List<T> ApplyToList(IList<T> source)
		{
			var dimensions = source.Count();
			if (dimensions != Dimensions)
				throw new ArgumentException("Permutation and data have different dimensionality", nameof(source));
			var target = new List<T>(dimensions);
			for (var i = 0; i < dimensions; i++)
				target.Add(source[Mapping[i]]);
			return target;
		}

		/// <summary>
		/// Transform the source into the target by permuting the order of items according to the mapping
		/// and also applying a change of type using a converter delegate.
		/// </summary>
		/// <param name="source">Source data before the transformation.</param>
		/// <returns>Transformed array of data taken from the source and converted to a new type as well as rearranged.</returns>
		public TOut[] ApplyToArray<TOut>(IList<T> source, Func<T,TOut> converter)
		{
			var dimensions = source.Count();
			if (dimensions != Dimensions)
				throw new ArgumentException("Permutation and data have different dimensionality", nameof(source));
			var target = new TOut[dimensions];
			for (var i = 0; i < dimensions; i++)
				target[i] = converter(source[Mapping[i]]);
			return target;
		}

		#endregion

		#region Mutate a Permutation randomly

		/// <summary>
		/// Randomly swap the given number of dimensions
		/// </summary>
		/// <param name="dimsToScramble">Dims to scramble.</param>
		public Permutation<T> Scramble(int dimsToScramble)
		{
			var newPermutation = Mapping.PartialShuffle(dimsToScramble);
			return new Permutation<T>(newPermutation);
		}

		#endregion

		public static IEnumerable<Permutation<T>> AllPermutations(int dimensions)
		{
			foreach (var perm in dimensions.CreateAllPermutations())
				yield return new Permutation<T>(perm);
		}




	}
}
