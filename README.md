# HilbertTransformation
Transform N-dimensional points to and from a 1-dimensional Hilbert fractal curve index in C# .Net.

The core algorithm is a port to C# of a C program written by John Skilling and published in
the journal article "Programming the Hilbert curve", (c) 2004 American Institute of Physics.

The original C# code was written by Paul Anton Chernoch and may be freely used with attribution.

##How to Perform the transformations:
 
  A. The using statements
  
      using System.Numerics;
      using HilbertTransformation;
 
  B. Hilbert Index to HilbertPoint to N-Dimensional coordinates
   
      int bits = ???;       // Pick so that 2^bits exceeds the largest value in any coordinate.
      int dimensions = ???; // Number of dimensions for the point.
      var index1 = new BigInteger(...);
      var hPoint1 = new HilbertPoint(index1, dimensions, bits);
      uint[] coordinates = hPoint.Coordinates;
	 
  C. Coordinates to Hilbert Index
	 
      var hPoint2 = new HilbertPoint(coordinates, bits);
      BigInteger index2 = hPoint2.Index;

##The Four Transformations

 There are really four transformations that occur:

 1. From BigInteger (the Hilbert index) to Transposed.
 2. From Transposed to Hilbert Axes (N-dimensional point, an array of uints).
 3. From Hilbert Axes to Transposed.
 4. From Transposed back to BigInteger.

 The transposed form is a rewrite of the BigInteger in which the high bit of the BigInteger goes to the 
 high bit of the first byte of the transposed array, the next highest bit goes to the high bit of the next transposed byte, 
 etc in a striped fashion. It is only in this rearranged form that the Hilbert transformation can be performed.
 However, only the BigInteger index and the Hilbert Axes (multi-dimensional coordinate form) are useful to library users 
 in their analysis. The HilbertPoint class provides the interface to these operations.

##Modeling Floating point data

 This transform is most suitable for non-negative integer data. To apply it to floating point numbers, you need to do the following:

 1. Decide how much resolution in bits you require for each coordinate. 
    The more bits of precision you use, the higher the cost of the transformation.

 2. Write methods to perform a two-way mapping from your coordinate system to the non-negative integers.
    This transform may require shifting and scaling each dimension a different amount in order to yield a desirable
    distance metric and origin. 

 Example.

    This mapping will have to quantize values. For example, if your numbers range from -10 to +20 and you want 
    to resolve to 0.1 increments, then perform these transformations:
       a. translate by +10 (so all numbers are positive)
       b. scale by x10 (so all numbers are integers)
       c. Since the range is now from zero to 300, the next highest power of two is 512, so choose nine bits of resolution 
          for your HilbertPoints.

 
