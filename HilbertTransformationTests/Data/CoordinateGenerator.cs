using System;
using System.Collections.Generic;
using System.Linq;
using HilbertTransformation;
using HilbertTransformation.Random;

namespace HilbertTransformationTests.Data
{
    /// <summary>
    /// Generate data (random or otherwise) for a subset of a point's dimensions.
    /// </summary>
    public abstract class CoordinateGenerator
    {
        /// <summary>
        /// Uniform random number generator used for the random aspects of constructing a point.
        /// </summary>
        protected FastRandom Rng { get; private set; }

        /// <summary>
        /// Gaussian random number generator used for adding Gaussian noise to a point being constructed.
        /// </summary>
        protected ZigguratGaussianSampler GaussianRng { get; private set; }

        /// <summary>
        /// Identifies which indices (dimensions) should have values generated for them.
        /// 
        /// This should be filled with values drawn from the range [0,D) where D is the total number of dimensions that the point has.
        /// </summary>
        public List<int> AffectedDimensions { get; set; }

        /// <summary>
        /// Minimum value permitted for any coordinate value (inclusive).
        /// </summary>
        public int Minimum { get; set; }

        /// <summary>
        /// Maximum value permitted for any coordinate value (exclusive).
        /// </summary>
        public int Maximum { get; set; }

        protected CoordinateGenerator(IEnumerable<int> indices)
        {
            AffectedDimensions = indices.ToList();
            Init();
        }

        /// <summary>
        /// Create a generator for all the consecutive dimensions from startDimension to startDimension + dimensions - 1.
        /// </summary>
        /// <param name="dimensions">Number of dimensions that will be affected by this generator.</param>
        /// <param name="startDimension">Lowest affected dimension.</param>
        protected CoordinateGenerator(int dimensions, int startDimension = 0)
        {
            AffectedDimensions = new List<int>();
            for (var iDim = startDimension; iDim < dimensions + startDimension; iDim++)
                AffectedDimensions.Add(iDim);
            Init();
        }

		void Init()
		{
			Maximum = int.MaxValue;
			Minimum = 0;
			Rng = new FastRandom();
			GaussianRng = new ZigguratGaussianSampler(Rng);
		}

		/// <summary>
		/// Set values in the given point for all the indices addressed by this generator.
		/// </summary>
		/// <param name="point">Point whose coordinate values will be changed.
		/// The values will be changed in place.
		/// </param>
		/// <returns>The same point with changes made.</returns>
		public abstract int[] Generate(int[] point);

        /// <summary>
        /// Constrain the value to fall between Minimum and Maximum.
        /// </summary>
        /// <param name="i">Value to constrain.</param>
        /// <returns>Possibly altered value that falls between Minimum and Maximum.</returns>
        public int Constrain(int i)
        {
            if (i < Minimum) i = Minimum;
            if (i > Maximum) i = Maximum;
            return i;
        }

        /// <summary>
        /// Add Gaussian noise to a value, but constrain the value to not exceed the Minimum or Maximum.
        /// </summary>
        /// <param name="value">Input value.</param>
        /// <param name="stdDev">Standard deviation of noise.</param>
        /// <returns>Value with noise added.</returns>
        public int AddNoise(double value, double stdDev)
        {
            if (stdDev <= 0) return (int) Math.Round(value, 0);
            return Constrain((int)Math.Round(GaussianRng.NextSample(value, stdDev), 0));
        }

        /// <summary>
        /// Apply the generators in sequence to create new points.
        /// </summary>
        /// <param name="generators">Generators that can set the value of one or more coordinates in the point being created.</param>
        /// <param name="dimensions">Number of dimensions that each returned point will have.</param>
        /// <returns>A series of points that have been created by the given genrerators.</returns>
        public static IEnumerator<int[]> GetEnumerator(IList<CoordinateGenerator> generators, int dimensions)
        {
            int[] point = null;
            do
            {
                point = new int[dimensions];
                foreach (var generator in generators)
                {
                    point = generator.Generate(point);
                    if (point == null)
                        break;
                }
                if (point != null)
                    yield return point;
            } while (point != null);
        }
    }

    /// <summary>
    /// Generate a random point that conforms to a Gaussian distribution about the given center point.
    /// 
    /// This can generate spherical or ellipsoidal clusters of points.
    /// </summary>
    public class EllipsoidalGenerator : CoordinateGenerator
    {
        /// <summary>
        /// Center point around which values will be clustered.
        /// 
        /// Only those dimensions included in AffectedDimensions influence the generated point.
        /// This represents the mean of the normal distribution.
        /// </summary>
        public int[] Center { get; set; }

        public int Dimensions { get { return Center.Length; } }

        /// <summary>
        /// Standard deviation for each affected dimension. 
        /// This should be an array with a separate deviation for each corresponding dimension in AffectedDimensions.
        /// </summary>
        public double[] StandardDeviation { get; set; }

        /// <summary>
        /// Given two spherical Gaussian distributions, this computes the minimum separation between the centers of those clusters 
        /// such that points from the clusters are unlikely to overlap.
        /// 
        /// See page 13 of http://www.cs.cornell.edu/courses/cs4850/2010sp/Course%20Notes/2%20High%20Dimensional%20Data-Jan-2010.pdf
		/// 
		/// In one-dimension, one would normally separate two guassians by two standard deviations or more to distinguish them, 
		/// but in high dimensional spaces, the points cluster strongly at one standard deviation times the root od the number of dimensions.
        /// </summary>
        /// <param name="standardDeviation">Standard deviation of the clusters. If not uniform in each dimension, take the largest standard deviation
        /// for each cluster, then average the deviations for the two clusters.</param>
        /// <param name="dimensions">Number of dimensions for the space.</param>
        /// <returns>Minimum distance (not squared distance) between the clusters such that the points will be resolved into distinct clusters and not
        /// merged into one.</returns>
        public static long MinimumSeparation(double standardDeviation, int dimensions)
        {
            // Imagine two spheres each of radius σ√d and separated by a gap of σ√d. This would give us 3σ√d separation.
            return (long) Math.Ceiling(3 * standardDeviation * Math.Sqrt(dimensions));
        }

        public EllipsoidalGenerator(int[] center, double[] stdDevs, IEnumerable<int> indices) : base(indices)
        {
            Center = center;
            StandardDeviation = stdDevs;
            Init(stdDevs);
        }

        public EllipsoidalGenerator(int[] center, double[] stdDevs, int dimensions, int startDimension = 0)
            : base(dimensions, startDimension)
        {
            Center = center;
            Init(stdDevs);
        }

        private void Init(double[] stdDevs)
        {
            InitStandardDeviation(stdDevs);
        }

        /// <summary>
        /// Initialize StandardDeviation.
        /// </summary>
        /// <param name="stdDevs">If this has a single value, make an array of size Dimensions that has this value repeated
        /// for each dimension, which will generate a spherical distribution.
        /// If this is already of size Dimensions, each corresponding dimension will have a separate standard deviation,
        /// which will generate an ellipsoidal distribution.</param>
        private void InitStandardDeviation(double[] stdDevs)
        {
            StandardDeviation = new double[Dimensions];
            if (stdDevs.Length == Dimensions)
                StandardDeviation = stdDevs.ToArray();
            else
            {
                StandardDeviation = new double[Dimensions];
                for (var iDim = 0; iDim < Dimensions; iDim++)
                    StandardDeviation[iDim] = stdDevs[0];        
            }
        }

        /// <summary>
        /// Set values in the given point for all the indices addressed by this generator to equal the Center
        /// with a random spread whose standard deviation per dimension is given by the StandardDeviation array.
        /// </summary>
        /// <param name="point">Point whose coordinate values will be changed.
        /// The values will be changed in place.
        /// </param>
        /// <returns>The same point with changes made.</returns>
        public override int[] Generate(int[] point)
        {
            for (var iDim = 0; iDim < AffectedDimensions.Count; iDim++)
                point[iDim] = AddNoise(Center[iDim], StandardDeviation[iDim]);
            return point;
        }
    }

    /// <summary>
    /// Cause the coordinate value for a TargetDimension to be strongly correlated to a SourceDimension.
    /// 
    /// Goal: To create dimensions that are so highly correlated that they can be detected and eliminated by the class MeasureRedundancy.
    /// </summary>
    public class CorrelatedDimensionGenerator : CoordinateGenerator
    {
        /// <summary>
        /// Perform a (usually) one-to-one mapping of a source value (the dictionary key) to a target value.
        /// </summary>
        private Func<int, int> Translator { get; set; }

        public int SourceDimension { get; set; }

        public int TargetDimension { get { return AffectedDimensions[0]; } }

        /// <summary>
        /// Standard deviation for noise added to the data so that the correlation is not perfect. 
        /// This should probably be a value of one or less, or the noise will be so large as to break the correlation.
        /// </summary>
        public double StandardDeviation { get; set; }

        public CorrelatedDimensionGenerator(Func<int, int> translator, int sourceDimension, int targetDimension) : base(new int[] { targetDimension })
        {
			Translator = translator;
            SourceDimension = sourceDimension;
            StandardDeviation = 0;
        }

        /// <summary>
        /// Set the value for the sole AffectedDimension in the given point to a value correlated to the SourceDimension. 
        /// 
        /// The correlation is accomplished by executing the Translator function, which should perform a one-to-one mapping between the 
        /// value of the SourceDimension and the intended target value for the target dimension.
        /// A random spread may be applied whose standard deviation is given by StandardDeviation.
        /// </summary>
        /// <param name="point">Point whose coordinate values will be changed.
        /// The values will be changed in place.
        /// </param>
        /// <returns>The same point with changes made.</returns>
        public override int[] Generate(int[] point)
        {
            point[TargetDimension] = AddNoise(Translator(point[SourceDimension]), StandardDeviation);
            return point;
        }
    }

    /// <summary>
    /// Cause a TargetDimension to be a linear combination of one or more source dimensions.
    /// 
    /// Goal: To create dimensions that are so highly correlated that they can be detected and eliminated.
    /// </summary>
    public class LinearCombinationGenerator : CoordinateGenerator
    {
        /// <summary>
        /// Key is a source dimension index, value is the weight to multiply the corresponding dimension's coordinate value by.
        /// </summary>
        public Dictionary<int,double> SourceDimensionWeights { get; set; }

        public int TargetDimension { get { return AffectedDimensions[0]; } }

        /// <summary>
        /// Standard deviation for noise added to the data so that the correlation is not perfect. 
        /// This should probably be a value of one or less, or the noise will be so large as to break the correlation.
        /// </summary>
        public double StandardDeviation { get; set; }

        public LinearCombinationGenerator(Dictionary<int, double> linearWeights, int targetDimension)
            : base(new int[] { targetDimension })
        {
            SourceDimensionWeights = linearWeights;
            StandardDeviation = 0;
        }

        /// <summary>
        /// Combine the coordinate values from one or more source dimensions into a single target value using linear weights for each dimension. 
        /// 
        /// A random spread may be applied whose standard deviation is given by StandardDeviation.
        /// </summary>
        /// <param name="point">Point whose TargetDimension coordinate value will be changed.
        /// The values will be changed in place.
        /// </param>
        /// <returns>The same point with changes made.</returns>
        public override int[] Generate(int[] point)
        {
            double xlatedValue = 0;
            foreach (var pair in SourceDimensionWeights)
                xlatedValue += point[pair.Key] * pair.Value;
            
            point[TargetDimension] = AddNoise(xlatedValue, StandardDeviation); 
            return point;
        }
    }

    /// <summary>
    /// Generate a random point that falls in a random position along the line segment joining two given end points, with the optional
    /// addition of Gaussian noise.
    /// </summary>
    public class LineGenerator : CoordinateGenerator
    {
        /// <summary>
        /// First end point for the line segment.
        /// </summary>
        public int[] Start { get; set; }

        /// <summary>
        /// Second end point for the line segment.
        /// </summary>
        public int[] End { get; set; }

        public int Dimensions { get { return Start.Length; } }

        /// <summary>
        /// Standard deviation for each affected dimension. 
        /// This should be an array with a separate deviation for each corresponding dimension in AffectedDimensions.
        /// </summary>
        public double[] StandardDeviation { get; set; }

        public LineGenerator(int[] start, int[] end, double[] stdDevs, IEnumerable<int> indices)
            : base(indices)
        {
            Start = start;
            End = end;
            StandardDeviation = stdDevs;
            Init(stdDevs);
        }

        private void Init(double[] stdDevs)
        {
            InitStandardDeviation(stdDevs);
        }

        /// <summary>
        /// Initialize StandardDeviation.
        /// </summary>
        /// <param name="stdDevs">If this has a single value, make an array of size Dimensions that has this value repeated
        /// for each dimension, which will generate a spherical distribution.
        /// If this is already of size Dimensions, each corresponding dimension will have a separate standard deviation,
        /// which will generate an ellipsoidal distribution.</param>
        private void InitStandardDeviation(double[] stdDevs)
        {
            StandardDeviation = new double[Dimensions];
            if (stdDevs.Length == Dimensions)
                StandardDeviation = stdDevs.ToArray();
            else
            {
                StandardDeviation = new double[Dimensions];
                for (var iDim = 0; iDim < Dimensions; iDim++)
                    StandardDeviation[iDim] = stdDevs[0];
            }
        }

        /// <summary>
        /// Randomly choose a point somewhere on the line segment between start and end, then add Gaussian noise.
        /// 
        /// Only affect those coordinates included in AffectedDimensions.
        /// </summary>
        /// <param name="point">Point whose coordinate values will be changed.
        /// The values will be changed in place.
        /// </param>
        /// <returns>The same point with changes made.</returns>
        public override int[] Generate(int[] point)
        {
            double distance = Rng.NextDouble(); // Distance from start to end point as a fraction from zero to one.
            for (var iDim = 0; iDim < AffectedDimensions.Count; iDim++)
            {
                var lineCoordinate = Start[iDim] * (1.0 - distance) + End[iDim] * distance;
                point[iDim] = AddNoise(lineCoordinate, StandardDeviation[iDim]);
            }
            return point;
        }

    }

    /// <summary>
    /// Generate a random point whose distance from all previously generated points exceeds the given minimum distance.
    /// 
    /// Goal: To create center points for non-overlapping clusters.
    /// </summary>
    public class DiffuseGenerator : CoordinateGenerator, IEnumerable<int[]>
    {
        /// <summary>
        /// Cache of already generated points.
        /// </summary>
        public List<UnsignedPoint> PreviousPoints { get; set; }

        /// <summary>
        /// Number of dimensions in each point.
        /// </summary>
        public int Dimensions { get; private set; }

        /// <summary>
        /// No two points that are generated will be closer to each other than this distance.
        /// </summary>
        public double MinimumDistance { get; set; }

		/// <summary>
		/// Create a DiffuseGenerator which ensures that randonly generated points are not too close to each other.
		/// </summary>
		/// <param name="dimensions">Number of dimensions for each point.</param>
		/// <param name="minDistance">Minimum distance between points.</param>
        public DiffuseGenerator(int dimensions, double minDistance)
            : base(dimensions)
        {
            Dimensions = dimensions;
            PreviousPoints = new List<UnsignedPoint>();
			MinimumDistance = minDistance;
        }

        /// <summary>
        /// Compose a random point whose values fall between Minimum (inclusive) and Maximum (exclusive) but
        /// which is no closer to all previous points than MinimumDistance.
        /// 
        /// NOTE: If too many unsuccessful attempts are made to find such a point, null will be returned instead.
        /// </summary>
        /// <param name="point">Point whose coordinate values will be changed.
        /// The values will be changed in place.
        /// This array must have at least as many elements as Dimensions.
        /// </param>
        /// <returns>The same point with changes made.</returns>
        public override int[] Generate(int[] point)
        {
            var minDistSquared = (long) Math.Ceiling(MinimumDistance * MinimumDistance);
            var tries = 0;
            while (tries < 100) 
            {
                for (var iDim = 0; iDim < AffectedDimensions.Count; iDim++)
                    point[iDim] = Rng.Next(Minimum, Maximum);
                var pt = new UnsignedPoint(point); // Copies the array.
                if (!PreviousPoints.Any(p => p.Measure(pt) < minDistSquared))
                {  
                    PreviousPoints.Add(pt);
                    return point;
                }
                tries++;
            }
            return null;
        }

        #region IEnumerable implementation

        public IEnumerator<int[]> GetEnumerator()
        {
            int[] point = null;
            do
            {
                point = Generate(new int[Dimensions]);
                if (point != null)
                    yield return point;
            } while (point != null);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
