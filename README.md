# HilbertTransformation
Transform N-dimensional points to and from a 1-dimensional Hilbert fractal curve index in C# .Net.

The core algorithm is a port to C# of a C program written by John Skilling and published in
the journal article "Programming the Hilbert curve", (c) 2004 American Institute of Physics.

The original C# code was written by Paul Anton Chernoch and may be freely used with attribution.

## How to Perform the transformations:
 
  A. The using statements
  
      using System.Numerics;
      using HilbertTransformation;
 
  B. Hilbert Index to HilbertPoint to N-Dimensional coordinates
   
  This is how to convert a distance along the Hilbert curve into a D-dimensional point.

      int bits = ???;       // Pick so that 2^bits exceeds the largest value in any coordinate.
      int dimensions = ???; // Number of dimensions for the point.
      var index1 = new BigInteger(...);
      var hPoint1 = new HilbertPoint(index1, dimensions, bits);
      uint[] coordinates = hPoint.Coordinates;
	 
  C. Coordinates to Hilbert Index
	 
  This is how you transform a point to a HilbertPoint.

      var hPoint2 = new HilbertPoint(coordinates, bits);
      BigInteger index2 = hPoint2.Index;

  If one does not need the transformed index but merely want to sort points in Hilbert order,
  a memory-efficient, in-place sort is implemented by the HilbertSort class:

      UnsignedPoint[] points = ...
      PointBalancer balancer = null;
      HilbertSort.Sort(points, ref balancer);

  The UnsignedPoint class is little more than a vector of unsigned coordinates with some
  extra space to hold pre-computated values that will speed up Euclidean distance calculations.
  The PointBalancer shifts coordinates such that their median value falls in the middle of the
  range, which aids in reducing memory usage and speeds up the sort by reducing the number
  of bits necessary to represent the Hilbert index.

  The sort algorithm starts by sorting every point using a Hilbert transform of one bit per
  dimension to form large buckets of points sharing the same low-precision Hilbert index. 
  Then it sorts each bucket with progressively more bits of precision until every bucket has
  a single element, or we reach the maximum precision and are faced with true duplicates.
  This approach means that the full-precision Hilbert transform is needed for comparatively 
  few points, and at no time do we hold the Hilbert index for all points in memory at the same 
  time. At any given time, we have one-to-two bits per dimension of space in memory per point,
  as opposed to doubling the data size during the sort as the former method did.

## The Four Transformations

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

## Modeling Floating point data

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

 ## Unassisted Classification

 In progress.... SLASH, a tool to cluster high-dimensional data using the Hilbert curve.

 The class SlashCommand implements a console application that can perform unassisted classification
 of high-dimensional data. To run the program, execute the bash command slash.sh.
 You may need to modify this script to point to your mono installation.

 "./slash.sh help" will exlpain how to call the program.
 Study class SlashConfig to understand the attributes found in the yaml configuration file.
 You will want to set the input and output files, and the name of the id field in your data, if any.

 Example output from slash help:

       Slash Version 0.1

       Purpose: Slash clusters high-dimensional data.

       Usage: 1. slash [help | -h | -help]
              2. slash define [config-file] [input-data-file] [output-data-file]
              3. slash cluster [config-file] [input-data-file] [output-data-file]
              4. slash recluster [config-file] [input-data-file] [output-data-file]
              5. slash version

       config-file ....... If omitted, assume slash.yaml is the configuration file.
                           Configuration values are either written to this file
                           (for 'define') or read from it, optionally overriding
                           the values for input and output files.
       input-data-file ... If given, read input records from this file.
                           If a hyphen, read from standard input.
                           If omitted when defining a configuration, assume standard
                           input holds the input records.
                           Otherwise, use the value from the existing configuration file.
       output-data-file .. If present, write output records to this file.
                           If a hyphen, write to standard input.
                           If a question mark, suppress output.
                           If omitted when defining a configuration, assume writing
                           to standard output.
                           Otherwise, use the value from the existing configuration file. 

       HELP. The first usage shows this help message.

       DEFINE. The second usage creates a new YAML configuration file with the given name
       but does not perform clustering. 
       The file will have default settings for all properties, except any file names
       optionally supplied on the command line. The user should edit this file
       to specify important properties like the names of the id field and category field, 
       and whether there is a header record in the input CSV file.

       CLUSTER. The third usage reads a configuration file and the indicated input data file
       (or standard input), clusters the data and writes the results to the indicated 
       output file (or standard output). If the input data includes clustering
       categories, a comparison is logged to indicate how similar the new clustering
       is to the clustering done via some other source or a previous run of SLASH.
       The original clustering, if present, has no influence over the resulting clustering.
       
       RECLUSTER. The fourth usage reads a configuration file and the indicated input data file
       (or standard input). It assumes that the records have already been clustered.
       It begins with the records grouped by this original clustering and continues
       with a new round of clustering. It writes the results to the indicated 
       output file (or standard output). A comparison between the original categories
       and the final categories is logged to indicate how different the new clustering
       is from the original clustering.

       VERSION. Print out the program version number.


