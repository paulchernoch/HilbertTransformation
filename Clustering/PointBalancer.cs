using HilbertTransformation;
using HilbertTransformation.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Clustering
{
    /// <summary>
    /// Create Hilbert indices for points with any number of Bits per dimension after first balancing them.
    /// 
    /// Balancing means that we shift the coordinates on points, such that for a given number of bits per dimension B,
    /// half of the points have values for any given dimension that are below the halfway value and
    /// half are above the halfway value, where the halfway value is 2^(B-1).
    /// </summary>
    public class PointBalancer
    {
        List<DimensionTransform> Transforms { get; set; }

        /// <summary>
        /// Number of bits required to represent all coordinate values of all points being balanced without loss of precision.
        /// </summary>
        public int BitsPerDimension {  get { return Transforms[0].MinimumBitsRequired; } }

        /// <summary>
        /// Create a PointBalancer and all its component DimensionTransforms, inferring the required BitsPerDimension in the process.
        /// </summary>
        /// <param name="points">Points to be studied so that we know what must be done to balance them.</param>
        public PointBalancer(IReadOnlyList<UnsignedPoint> points)
        {
            Transforms = DimensionTransform.CreateMany(
                points.Select(point => point.Coordinates)    
            );
        }

        /// <summary>
        /// Balance a single point.
        /// </summary>
        /// <param name="point">Point to balance.</param>
        /// <param name="bitsPerDimension"></param>
        /// <returns>A balanced point.</returns>
        public UnsignedPoint Balance(UnsignedPoint point, int bitsPerDimension)
        {
            var balancedCoordinates = Balance(point.Coordinates, bitsPerDimension);
            return new UnsignedPoint(balancedCoordinates, point.UniqueId);
        }

        public uint[] Balance(uint[] point, int bitsPerDimension)
        {
            var balancedCoordinates = point
                .Select((x, index) => Transforms[index].Transform(x, bitsPerDimension)).ToArray();
            return balancedCoordinates;
        }

        /// <summary>
        /// Get the Hilbert position for a given point after balancing it, performing an optional permutation of the coordinates.
        /// The point may have its coordinates reduced in precision if bitsPerDimension is lower than the required value.
        /// </summary>
        /// <param name="unbalancedPoint">Point prior to balancing.</param>
        /// <param name="bitsPerDimension">Number of bits per dimension to use in forming the Hilbert position,
        /// which may be lower than the number of bits required to faithfully represent all coordinate values, 
        /// causing the coordinate values of all coordinates to be reduced in precision.</param>
        /// <param name="perm">Permutation to apply to coordinates, scrambling their order in a consistent way for all points.</param>
        /// <returns>The Hilbert position.</returns>
        public BigInteger ToHilbertPosition(UnsignedPoint unbalancedPoint, int bitsPerDimension, Permutation<uint> perm = null)
        {
            uint[] balancedCoordinates;
            if (perm == null) { 
                balancedCoordinates = Balance(unbalancedPoint.Coordinates, bitsPerDimension);
                return balancedCoordinates.HilbertIndex(bitsPerDimension);
            }
            else
            {
                balancedCoordinates = Balance(unbalancedPoint.Coordinates, bitsPerDimension);
                var permutedCoordinates = perm.ApplyToArray(balancedCoordinates);
                return permutedCoordinates.HilbertIndex(bitsPerDimension);
            }
        }

    }
}
