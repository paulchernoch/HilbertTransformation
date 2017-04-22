using System.Numerics;
using System.Collections.Generic;
using System;

namespace HilbertTransformation
{
    /// <summary>
    /// Convert between Hilbert index and N-dimensional points.
    /// 
    /// The intermediate form of the Hilbert index is an array of transposed bits. 
    /// 
    /// Example: 5 bits for each of n=3 coordinates.
    /// 15-bit Hilbert integer = A B C D E F G H I J K L M N O is stored
    /// as its Transpose                        ^
    /// X[0] = A D G J M					X[2]|  7
    /// X[1] = B E H K N		«-------»       | /X[1]
    /// X[2] = C F I L O				   axes |/
    ///        high low				    	    0------> X[0]
    ///        
    /// NOTE: This algorithm is derived from work done by John Skilling and published in "Programming the Hilbert curve".
    /// (c) 2004 American Institute of Physics.
    /// 
    /// NOTE: These are the most important extension methods:
    /// 
    ///    1. public static uint[] HilbertAxes(this BigInteger hilbertIndex, int bits, int dimensions)
    ///       Converts Hilbert Index to N-Space.
    /// 
    ///    2. public static BigInteger HilbertIndex(this uint[] hilbertAxes, int bits)
    ///       Converts N-Space to Hilbert Index.
    /// 
    ///    3. public static int SmallestPowerOfTwo(this int n)
    ///       Helps compute the number of bits needed to encode a dimension.
    /// </summary>
    public static class FastHilbert
    {

        /// <summary>
        /// Convert a Hilbert distance (index) stored in a BigInteger into a transposed matrix, where the bits are distributed
        /// among many integers in an array.
        /// </summary>
        /// <param name="hilbertIndex">Hilbert distance as a BigInteger.</param>
        /// <param name="bits">Number of bits per point in the N-space.</param>
        /// <param name="dimensions">Number of dimensions in the N-space.</param>
        /// <returns>The Hilbert distance expressed as a transposed array.</returns>
        public static uint[] Transpose(this BigInteger hilbertIndex, int bits, int dimensions)
        {
            // First coordinate gets its high bit from the highest bit of the hilbertIndex.
            // Second coordinate gets its high bit from the second bit of the index. Etc.

            return Uninterleave(hilbertIndex, bits, dimensions);
        }

		/// <summary>
		/// Convert a BigInteger into an array of bits.
		/// </summary>
		/// <param name="N">BigInteger to convert.</param>
		/// <returns>Array of ones and zeroes. The first element is the low bit of the BigInteger.
		/// The last bit is the sign bit.
		/// </returns>
		public static int[] UnpackBigInteger(BigInteger N)
		{
			var bytes = N.ToByteArray();
			var bits = new int[bytes.Length << 3];
			var bitIndex = 0;
			foreach (var b in bytes)
			{
				var bShift = b;
				for (var bitInByte = 0; bitInByte < 8; bitInByte++)
				{
					bits[bitIndex++] = bShift & 1;
					bShift >>= 1;
				}
			}
			return bits;
		}

		/// <summary>
		/// Convert a BigInteger into an array of bits using the supplied array to hold the results.
		/// </summary>
		/// <param name="N">BigInteger to convert.</param>
		/// <param name="bits">Array to hold bits from result. 
		/// If the BigInteger has more bits than this, the higher bits are dropped.</param>
		/// <returns>Array of ones and zeroes. The first element is the low bit of the BigInteger.
		/// The last bit is only the sign bit if the size of the supplied array times eight exactly matches the number of bytes returned by BigInteger.ToByteArray.
		/// </returns>
		public static int[] UnpackBigInteger(BigInteger N, int[] bits)
		{
			var bytes = N.ToByteArray();
			var bitIndex = 0;
			foreach (var b in bytes)
			{
				var bShift = b;
				for (var bitInByte = 0; bitInByte < 8 && (bitIndex < bits.Length); bitInByte++)
				{
					bits[bitIndex++] = bShift & 1;
					bShift >>= 1;
				}
				if (bitIndex >= bits.Length)
					break;
			}
			return bits;
		}

        public static uint[] Uninterleave(BigInteger grayCode, int bitDepth, int dimensions, bool reverseOrder = true)
        {
            //TODO: There must be a more efficient way to delaminate, but I can't figure it out.
            var vector = new uint[dimensions];
            var numBits = dimensions * bitDepth;
            var bits = UnpackBigInteger(grayCode, new int[numBits]);
            var bitIndex = 0;
			var startDimension = reverseOrder ? dimensions - 1 : 0;
			var stopDimension = reverseOrder ? -1 : dimensions;
            int dimIncrement = reverseOrder ? -1 : 1;
            for (var bitNumber = 0; bitNumber < bitDepth; bitNumber++)
            {
                for (var dimension = startDimension; dimension != stopDimension; dimension += dimIncrement)
                {
                    vector[dimension] = vector[dimension] | (uint)(bits[bitIndex++] << bitNumber);
                }
            }
            return vector;
        }

        /// <summary>
        /// Convert a transposed Hilbert index back into a BigInteger index.
        /// 
        /// Assume that the number of dimensions of the point in N-space is the length of the transposedIndex array.
        /// </summary>
        /// <param name="transposedIndex">A Hilbert index in transposed form.</param>
        /// <param name="bits">Number of bits used to represent each coordinate.</param>
        /// <returns>The Hilbert index (or distance) expresssed as a BigInteger.</returns>
        public static BigInteger Untranspose(this uint[] transposedIndex, int bits, bool reverseOrder = true)
        {
            // The high bit of the first coordinate becomes the high bit of the index.
            // The high bit of the second coordinate becomes the second bit of the index.

            var interleavedBytes = Interleave(transposedIndex, bits, reverseOrder);
            return new BigInteger(interleavedBytes); 
        }

        /// <summary>
        /// Interleave the bits of an unsigned vector and generate a byte array in little-endian order, as needed for the BigInteger constructor.
        /// 
        /// The high-order bit from the last number in vector becomes the high-order bit of last byte in the generated byte array.
        /// The high-order bit of the next to last number becomes the second highest-ordered bit in the last byte in the generated byte array.
        /// The low-order bit of the first number becomes the low order bit of the first byte in the new array.
        /// 
        /// NOTE: For a given bitDepth and number of dimensions, many of the intermediate values can be precomputed: 
        ///    iFromUintVector
        ///    iFromUintBit
        ///    iToByteVector
        ///    iToByteBit
        /// This is done in the class Interleaver.
        /// </summary>
        public static byte[] Interleave(uint[] vector, int bitDepth, bool reverseOrder = true)
        {
            // The individual bytes in the value array must be created in little-endian order, from lowest-order byte to highest-order byte
            // in order to be useful when creating a BigInteger.
            var dimensions = vector.Length; // Pull member access out of loop!
            var bytesNeeded = (bitDepth * dimensions) >> 3;
            var byteVector = new byte[bytesNeeded + 1]; // BigInteger seems to need an extra, zero byte at the end. Might be for the sign bit.
            var numBits = dimensions * bitDepth;

            for (var iBit = 0; iBit < numBits; iBit++)
            {
                var iFromUintVector = iBit % dimensions;
                var iFromUintBit = iBit / dimensions;
                var iToByteVector = iBit >> 3;
                var iToByteBit = iBit & 0x7;

				reverseOrder = false; // DEBUG
				var indexToUse = reverseOrder ? dimensions - iFromUintVector - 1 : iFromUintVector;

                var bit = (byte)(((vector[indexToUse] >> iFromUintBit) & 1U) << iToByteBit);
                byteVector[iToByteVector] |= bit;
            }
            return byteVector;
        }

        /// <summary>
        /// Convert the Hilbert index into an N-dimensional point expressed as a vector of uints.
        /// </summary>
        /// <param name="transposedIndex">The Hilbert index stored in transposed form.</param>
        /// <param name="bits">Number of bits per coordinate.</param>
        /// <returns>Coordinate vector.</returns>
        public static uint[] HilbertAxes(this uint[] transposedIndex, int bits)
        {
            var X = (uint[])transposedIndex.Clone();
            int n = X.Length; // n: Number of dimensions
            uint N = 2U << (bits - 1), P, Q, t;
            int i;
            // Gray decode by H ^ (H/2)
            t = X[n - 1] >> 1;
            for (i = n - 1; i > 0; i--) // Corrected error in paper which had i >= 0 leading to negative array index.
                X[i] ^= X[i - 1];
            X[0] ^= t;
            // Undo excess work
            for (Q = 2; Q != N; Q <<= 1)
            {
                P = Q - 1;
                for (i = n - 1; i >= 0; i--)
                    if ((X[i] & Q) != 0U)
                        X[0] ^= P; // invert
                    else
                    {
                        t = (X[0] ^ X[i]) & P;
                        X[0] ^= t;
                        X[i] ^= t;
                    }
            } // exchange
            return X;
        }

        /// <summary>
        /// Convert a Hilbert index (the distance from the origin along the Hilbert curve) into normal N-space coordinates.
        /// 
        /// Note: This is the most caller-friendly transformation from Hilbert to N-space.
        /// </summary>
        /// <param name="hilbertIndex">1-dimensional Distance from origin along the Hilbert curve.</param>
        /// <param name="bits">Number of bits used to encode each dimension.</param>
        /// <param name="dimensions">Number of dimensions in N-space.</param>
        /// <returns>N-dimensional coordinate.</returns>
        public static uint[] HilbertAxes(this BigInteger hilbertIndex, int bits, int dimensions)
        {
            return hilbertIndex.Transpose(bits, dimensions).HilbertAxes(bits);
        }

        /// <summary>
        /// Given the axes (coordinates) of a point in N-Dimensional space, find the distance to that point along the Hilbert curve.
        /// That distance will be transposed; broken into pieces and distributed into an array.
        /// 
        /// 
        /// The number of dimensions is the length of the hilbertAxes array.
        /// </summary>
        /// <param name="hilbertAxes">Point in N-space.</param>
        /// <param name="bits">Depth of the Hilbert curve. If bits is one, this is the top-level Hilbert curve.</param>
        /// <returns>The Hilbert distance (or index) as a transposed Hilbert index.</returns>
        public static uint[] HilbertIndexTransposed(this uint[] hilbertAxes, int bits)
        {
            var X = (uint[])hilbertAxes.Clone();
            var n = hilbertAxes.Length; // n: Number of dimensions
            uint M = 1U << (bits - 1), P, Q, t;
            int i;
            // Inverse undo
            for (Q = M; Q > 1; Q >>= 1)
            {
                P = Q - 1;

                // Split out first iteration of loop and store X[0] in a local variable to reduce array accesses.
                // Plus, we do not need to XOR X[0] with t twice since they cancel each other out.
                var X_0 = X[0];
                if ((X_0 & Q) != 0)
                    X_0 ^= P; // invert

                for (i = 1; i < n; i++)
                {
                    var X_i = X[i];
                    if ((X_i & Q) != 0)
                        X_0 ^= P; // invert
                    else
                    {
                        t = (X_0 ^ X_i) & P;
                        X_0 ^= t;
                        X[i] = X_i ^ t;
                    }
                }
                X[0] = X_0;
            } // exchange
            // Gray encode
            var X_i_minus_1 = X[0];
            for (i = 1; i < n; i++)
                X_i_minus_1 = X[i] ^= X_i_minus_1;
            t = 0;
            for (Q = M; Q > 1; Q >>= 1)
                if ((X[n - 1] & Q)!=0)
                    t ^= Q - 1;
            for (i = 0; i < n; i++)
                X[i] ^= t;

            return X;
        }

        /// <summary>
        /// Given the axes (coordinates) of a point in N-Dimensional space, find the distance to that point along the Hilbert curve.
        /// That distance will be transposed; broken into pieces and distributed into an array.
        /// 
        /// 
        /// The number of dimensions is the length of the hilbertAxes array.
        /// </summary>
        /// <param name="hilbertAxes">Point in N-space.</param>
        /// <param name="bits">Depth of the Hilbert curve. If bits is one, this is the top-level Hilbert curve.</param>
        /// <returns>The Hilbert distance (or index) as a transposed Hilbert index.</returns>
        public static uint[] HilbertIndexTransposed_unoptimized(this uint[] hilbertAxes, int bits)
        {
            var X = (uint[])hilbertAxes.Clone();
            var n = hilbertAxes.Length; // n: Number of dimensions
            uint M = 1U << (bits - 1), P, Q, t;
            int i;
            // Inverse undo
            for (Q = M; Q > 1; Q >>= 1)
            {
                P = Q - 1;
                for (i = 0; i < n; i++)
                    if ((X[i] & Q) != 0)
                        X[0] ^= P; // invert
                    else
                    {
                        t = (X[0] ^ X[i]) & P;
                        X[0] ^= t;
                        X[i] ^= t;
                    }
            } // exchange
            // Gray encode
            for (i = 1; i < n; i++)
                X[i] ^= X[i - 1];
            t = 0;
            for (Q = M; Q > 1; Q >>= 1)
                if ((X[n - 1] & Q) != 0)
                    t ^= Q - 1;
            for (i = 0; i < n; i++)
                X[i] ^= t;

            return X;
        }

        /// <summary>
        /// Given the axes (coordinates) of a point in N-Dimensional space, find the distance to that point along the Hilbert curve.
        /// 
        /// The number of dimensions is the length of the hilbertAxes array.
        /// 
        /// Note: This is the most caller-friendly transformation from N-space to Hilbert distance.
        /// </summary>
        /// <param name="hilbertAxes">Point in N-space.</param>
        /// <param name="bits">Depth of the Hilbert curve. If bits is one, this is the top-level Hilbert curve.</param>
        /// <returns>The Hilbert distance (or index) as a BigInteger.</returns>
        public static BigInteger HilbertIndex(this uint[] hilbertAxes, int bits)
        {
			var interleaver = Interleaver.Instance(hilbertAxes.Length, bits);
			return interleaver.Untranspose(hilbertAxes.HilbertIndexTransposed(bits));
        }

        /// <summary>
        /// Smallest power of two such that two raised to that power is greater than or equal to the given number.
        /// </summary>
        /// <param name="n">Find the smallest power of two that equals or exceeds this value.</param>
        /// <returns>The power, not the number. 
        /// For example, SmallestPowerOfTwo(7) returns 3, because 2^3 = 8 which is greater than 7.</returns>
        public static int SmallestPowerOfTwo(this int n)
        {
            var logTwo = 0;
            uint r = 1U;
            while (r < n)
            {
                r <<= 1;
                logTwo++;
            }
            return logTwo;
        }

        /// <summary>
        /// Compute the bounding-box volume ratio, WBV, for a window of the given size.
        /// 
        /// The ideal WBV for a curve (which is unobtainable) would be one, meaning that all points in a segment of the Hilbert curve
        /// fit within a rectangular volume with no included points outside the bounding box and no non-included points inside the box.
        /// 
        /// </summary>
        /// <param name="bitsPerDimension">Number of bits per dimension needed to define the Hilbert curve at the desired depth.</param>
        /// <param name="dimensions">Number of dimensions in the Hilbert curve.</param>
        /// <param name="windowSize">The WBV is calculated by averaging many WBV values for a sliding window of this size.</param>
        /// <param name="windowsToTest">For high dimensionality, it is not feasible to test all possible windows, so only test this many.</param>
        /// <returns>A value between zero and one, the bounding-box volume ratio.</returns>
        public static double WBV(int bitsPerDimension, int dimensions, int windowSize, int windowsToTest)
        {
            var subsetOfPoints = new List<uint[]>();
            int numSamples = windowsToTest + windowSize - 1;
            double WBVAve; // Bounding-box volume ratio.
            double WBVSum = 0;
            double WBVCount = 0;
            for (var i = 0; i < numSamples; i++)
            {
                if (i > windowSize)
                    subsetOfPoints.RemoveAt(0);
                uint[] hilbertAxes = HilbertAxes(new BigInteger(i), bitsPerDimension, dimensions);
                subsetOfPoints.Add(hilbertAxes);

                // Compute WBV
                var high = new uint[dimensions];
                var low = new uint[dimensions];
                for (var d = 0; d < dimensions; d++)
                {
                    high[d] = hilbertAxes[d];
                    low[d] = hilbertAxes[d];
                }
                foreach (var point in subsetOfPoints)
                {
                    for (var d = 0; d < dimensions; d++)
                    {
                        high[d] = Math.Max(high[d], point[d]);
                        low[d] = Math.Min(low[d], point[d]);
                    }
                }
                long volume = 1;
                for (var d = 0; d < dimensions; d++)
                {
                    volume *= ((long)high[d] - (long)low[d] + 1);
                }
                double wbv = (double)volume / windowSize;
                WBVSum += wbv;
                WBVCount++;
            }
            WBVAve = WBVSum / WBVCount;
            return WBVAve;
        }

    }
}
