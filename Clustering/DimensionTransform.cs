using HilbertTransformation;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;

namespace Clustering
{
    /// <summary>
    /// Study the distribution of values across many points within a single dimension
    /// so that we can later transform those values to make them suitable for being further
    /// transformed into a Hilbert curve.
    /// </summary>
    /// <remarks>
    /// The goal is to shift each dimension by a separate amount so that the median value for all
    /// dimensions will be the same, and that median fall at the halfway point between zero and
    /// 2^BitsPerDimension. This goal is desirable, because it will permit a first pass sorting of
    /// points by a Hilbert curve based on a single bit (the high bit) taken from each coordinate
    /// value in each point. Such a Hilbert curve will have an index that is very small 
    /// (only D bits, not D*B bits) in memory usage and can be quickly computed and compared during
    /// a sort operation. 
    /// 
    /// Once the first pass sort is done, many buckets will have only one
    /// element and will not need to be sorted further; no Hilbert transform at the full amount of bits
    /// will be needed. Those buckets where two or more points share the same D-Bit Hilbert index
    /// will need to be resorted. 
    /// 
    /// When is this optimization likely to succeed?
    /// 
    ///   1. If 2^D is much larger than N (the number of points), 
    ///   2. If many dimensions are independent of one another
    ///   3. If many dimensions do not have a value that is repeated a majority of the time
    ///   
    /// If all three are true, it is possible that most points will have unique Hilbert indices even
    /// at one bit per dimension.
    /// 
    /// Typical usage is to create a DimensionTransform for each dimension, then take the largest value
    /// of MinimumBitsRequired as the value to use for the full-sized Hilbert transform and use it to set 
    /// MinimumBitsRequired for all dimensions to the same value.
    /// 
    /// TODO: Verify that when points are sorted by a D-Bit per dimension Hilbert curve, this ordering
    ///       is consistent with the ordering from a D*B-bit curve. What that means is:
    ///          if two points have different D-Bit Hilbert index values, their relative order
    ///          is the same as when sorted by a D*B-bit curve.
    /// </remarks>
    public class DimensionTransform
    {
        /// <summary>
        /// Minimum of all the untransformed values.
        /// </summary>
        public int Minimum { get; private set; } = int.MaxValue;

        /// <summary>
        /// Maximum of all the untransformed values.
        /// </summary>
        public int Maximum { get; private set; } = 0;

        /// <summary>
        /// Estimated median of the untransformed values.
        /// </summary>
        public int Median { get; private set; }

        /// <summary>
        /// Minimum bits required to represent the untransformed values.
        /// </summary>
        public int MinimumBitsRequired { get; private set; }

        /// <summary>
        /// Actual number of bits chosen by caller, which may differ from MinimumBitsRequired.
        /// </summary>
        public int BitsPerDimension { get; set; }

        public int Scale(int i) => 
            BitsPerDimension >= MinimumBitsRequired ? i : i >> (MinimumBitsRequired - BitsPerDimension);
        
        /// <summary>
        /// The number of bits to the right by which to shift the coordinate values.
        /// The number is zero if no shifting needs to be done, or a positive value
        /// if we are being afforded fewer BitsPerDimension than are required to faithfully represent the number.
        /// If necessary, we will quantize and lose precision.
        /// </summary>
        public int ScaleBy {
            get {
                return BitsPerDimension >= MinimumBitsRequired ? 0 : (MinimumBitsRequired - BitsPerDimension);
            }
        }
        
        /// <summary>
        /// Amount to add to a coordinate value to get as close as possible to 
        /// moving the median to the midpoint of the range of values representable by the given number of bits.
        /// This will cause half of the values for this dimension to have values less than the midpoint of the range,
        /// and half to fall above the midpoint.
        /// 
        /// Translation must occur before scaling.
        /// </summary>
        public int TranslateBy {
            get
            {
                var bitsForTranslation = Math.Max(BitsPerDimension, MinimumBitsRequired);
                var fullWay = (1 << bitsForTranslation) - 1;
                var halfWay = 1 << (bitsForTranslation - 1);
              
                var translateBy = halfWay - Median;
                var newMinimum = Minimum + translateBy;
                // If shifting makes the Minimum go negative, shift less.
                if (newMinimum < 0)
                    translateBy -= newMinimum;

                var newMaximum = Maximum + translateBy;
                // If shifting makes the Maximum exceed fullWay, shift less.
                if (newMaximum > fullWay)
                    translateBy -= newMaximum - fullWay;
                return translateBy;
            }
        }

        public DimensionTransform(IEnumerable<int> values)
        {
            Median = FrugalQuantile.ShuffledEstimate(
                values.Select(x =>
                {
                    // Since FrugalQuantile is going to iterate over the data once anyways,
                    // might as well piggyback to compute Minimum and Maximum.
                    Minimum = Min(Minimum, x);
                    Maximum = Max(Maximum, x);
                    return x;
                })
            );
            MinimumBitsRequired = BitsPerDimension = (Maximum + 1).SmallestPowerOfTwo();
        }

        public DimensionTransform(IEnumerable<uint> values) : this(values.Select(u => (int) u))
        {
        }

        /// <summary>
        /// Transform all the integer values taken from the same dimension across many points.
        /// 
        /// The points will be adjusted to satisfy two constraints:
        /// 
        ///   1) The median value across all points will fall near the halfway point from zero to the maximum value
        ///      that can be expressed with the desired number of bits.
        ///   2) Precision will be reduced by right-shifting if necessary, should fewer bits be used than are necessary
        ///      to span the range from Minimum to Maximum.
        /// </summary>
        /// <param name="values">Values to transform.</param>
        /// <param name="bitsPerDimension">If non-positive, use the value of BitsPerDimension
        /// already stored in the object.
        /// Otherwise, change BitsPerDimension to this value and use in the computation.</param>
        /// <returns>Values corresponding in order to the input, transformed.</returns>
        public IEnumerable<int> Transform(IEnumerable<int> values, int bitsPerDimension = 0)
        {
            if (bitsPerDimension > 0)
                BitsPerDimension = bitsPerDimension;
            var translate = TranslateBy;
            var scale = ScaleBy;
            foreach(var x in values)
            {
                yield return (x + translate) >> scale;
            }
        }

        /// <summary>
        /// Transform all the unsigned integer values taken from the same dimension across many points.
        /// 
        /// The points will be adjusted to satisfy two constraints:
        /// 
        ///   1) The median value across all points will fall near the halfway point from zero to the maximum value
        ///      that can be expressed with the desired number of bits.
        ///   2) Precision will be reduced by right-shifting if necessary, should fewer bits be used than are necessary
        ///      to span the range from Minimum to Maximum.
        /// </summary>
        /// <param name="values">Values to transform.</param>
        /// <param name="bitsPerDimension">If non-positive, use the value of BitsPerDimension
        /// already stored in the object.
        /// Otherwise, change BitsPerDimension to this value and use in the computation.</param>
        /// <returns>Values corresponding in order to the input, transformed.</returns>
        public IEnumerable<uint> Transform(IEnumerable<uint> values, int bitsPerDimension = 0)
        {
            if (bitsPerDimension > 0)
                BitsPerDimension = bitsPerDimension;
            var translate = TranslateBy;
            var scale = ScaleBy;
            foreach (var x in values)
            {
                yield return (uint)(((int)x + translate) >> scale);
            }
        }

        public uint Transform(uint x, int bitsPerDimension = 0)
        {
            if (bitsPerDimension > 0)
                BitsPerDimension = bitsPerDimension;
            var translate = TranslateBy;
            var scale = ScaleBy;
            return (uint)(((int)x + translate) >> scale);
        }

        public int Transform(int x, int bitsPerDimension = 0)
        {
            if (bitsPerDimension > 0)
                BitsPerDimension = bitsPerDimension;
            var translate = TranslateBy;
            var scale = ScaleBy;
            return ((x + translate) >> scale);
        }

        /// <summary>
        /// Construct a DimensionTransform for each dimension of the given UNSIGNED integer points, 
        /// find the largest value of MinimumBitsRequired among the dimensions and
        /// use it as the value for all the transforms.
        /// 
        /// Do not actually transform any coordinate values.
        /// </summary>
        /// <param name="points">Each "point" is just a collection of uint values.</param>
        /// <returns>The transforms, with MinimumBitsRequired set to the maximum value
        /// taken from all the dimensions.</returns>
        public static List<DimensionTransform> CreateMany(IEnumerable<IEnumerable<uint>> points)
        {
            var transforms = points.Select(point => new DimensionTransform(point)).ToList();
            var largestMinBits = transforms.Select(t => t.MinimumBitsRequired).Max();
            foreach (var t in transforms)
                t.MinimumBitsRequired = largestMinBits;
            return transforms;
        }

        /// <summary>
        /// Construct a DimensionTransform for each dimension of the given SIGNED integer points, 
        /// find the largest value of MinimumBitsRequired among the dimensions and
        /// use it as the value for all the transforms.
        /// 
        /// Do not actually transform any coordinate values.
        /// 
        /// NOTE: Even though the values are signed, no negative values may be present.
        /// The Hilbert curve requires non-negative values.
        /// </summary>
        /// <param name="points">Each "point" is just a collection of uint values.</param>
        /// <returns>The transforms, with MinimumBitsRequired set to the maximum value
        /// taken from all the dimensions.</returns>
        public static List<DimensionTransform> CreateMany(IEnumerable<IEnumerable<int>> points)
        {
            var transforms = points.Select(point => new DimensionTransform(point)).ToList();
            var largestMinBits = transforms.Select(t => t.MinimumBitsRequired).Max();
            foreach (var t in transforms)
                t.MinimumBitsRequired = largestMinBits;
            return transforms;
        }

    }
}
