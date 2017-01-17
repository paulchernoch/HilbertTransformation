# HilbertTransformation
Transform N-dimensional points to and from a 1-dimensional Hilbert fractal curve index in C# .Net.

##How to Perform the transformations:
 
  A. The using statements
  
      using System.Numerics;
      using HilbertTransformation;
 
  B. Hilbert Index to HilbertPoint to N-Dimensional coordinates
   
      int bits = ???;       // Pick so that 2^bits exceeds the larges value in any coordinate.
      int dimensions = ???; // Number of dimensions for the point.
      var index1 = new BigInteger(...);
      var hPoint1 = new HilbertPoint(index1, dimensions, bits);
      uint[] coordinates = hPoint.Coordinates;
	 
  C. Coordinates to Hilbert Index
	 
      var hPoint2 = new HilbertPoint(coordinates, bits);
      BigInteger index2 = hPoint2.Index;
