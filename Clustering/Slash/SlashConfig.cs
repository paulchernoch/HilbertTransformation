using System;
using System.IO;
using YamlDotNet.Serialization;

namespace Clustering
{
	/// <summary>
	/// Configuration for running Slash.
	/// 
	/// This should hold all the parameters necessary to configure every phase of the clustering process.
	/// </summary>
	public class SlashConfig: IEquatable<SlashConfig>
	{
		/// <summary>
		/// Parameters needed to load and parse the data file holding the points to cluster.
		/// </summary>
		public class DataConfig: IEquatable<DataConfig>
		{
			/// <summary>
			/// True if the input data has a header record that must be read, false otherwise.
			/// </summary>
			public bool ReadHeader { get; set; } = true;

			/// <summary>
			/// The name of the field in the input CSV file that holds the Id for each record, 
			/// or ROWNUM, if an id should be generated from the row number.
			/// 
			/// If ReadHeader is false and IdField is not ROWNUM, assume IdField is a one-based column number
			/// that indicates which column holds the id.
			/// </summary>
			public String IdField { get; set; } = "ROWNUM";

			/// <summary>
			/// If "?", then the data is uncategorized, otherwise the name of the input field holding the category string.
			/// 
			/// If ReadHeader is false and CategoryField is not "?", assume CategoryField is a one-based column number
			/// that indicates which column holds the category.
			/// </summary>
			public String CategoryField { get; set; } = "?";

			/// <summary>
			/// Input file containing the points to be clustered.
			/// </summary>
			public String InputDataFile { get; set; } = "?";

			public override bool Equals(object obj)
			{
				return Equals(obj as DataConfig);
			}

			public bool Equals(DataConfig other)
			{
				if (other == null)
					return false;
				return ReadHeader == other.ReadHeader
                      && (IdField ?? "").Equals(other.IdField)
                      && (CategoryField ?? "").Equals(other.CategoryField)
                      && (InputDataFile ?? "").Equals(other.InputDataFile)
				;
			}
		}

		/// <summary>
		/// Defines where and how to output the results.
		/// </summary>
		public class OutputConfig: IEquatable<OutputConfig>
		{
			/// <summary>
			/// Output file into which to write results.
			/// 
			/// If "?", empty string or null, results will not be saved.
			/// </summary>
			public String OutputDataFile { get; set; } = "?";

			/// <summary>
			/// Returns true if an output file should be written.
			/// </summary>
			public bool ShouldWrite() { return (OutputDataFile ?? "?").Length > 0; }

			/// <summary>
			/// True if the output data shall be prefixed by a row containing a header record, false otherwise.
			/// </summary>
			public bool WriteHeader { get; set; } = true;

			/// <summary>
			/// The name of the field in the output CSV file that holds the Id for each record.
			/// 
			/// Ignored if WriteHeader is false.
			/// </summary>
			public String IdField { get; set; } = "id";

			/// <summary>
			/// The name of the output field holding the category string.
			/// 
			/// Ignored if WriteHeader is false.
			/// </summary>
			public String CategoryField { get; set; } = "category";

			public String LogFile { get; set; } = "";


			public override bool Equals(object obj)
			{
				return Equals(obj as OutputConfig);
			}

			public bool Equals(OutputConfig other)
			{
				if (other == null)
					return false;
				return (OutputDataFile ?? "").Equals(other.OutputDataFile)
	                 && WriteHeader == other.WriteHeader
                     && (IdField ?? "").Equals(other.IdField)
                     && (CategoryField ?? "").Equals(other.CategoryField)
                     && (LogFile ?? "").Equals(other.LogFile)
                 ;
			}
		}

		/// <summary>
		/// Parameters needed by OptimalIndex which define the budget (how hard to try before giving up)
		/// and  whether to operate on all points (accurate but extra memory) 
		/// or a sample (less memory but less accurate).
		/// </summary>
		public class IndexConfig: IEquatable<IndexConfig>
		{
			public HilbertClassifier.IndexBudget Budget { get; set; } = new HilbertClassifier.IndexBudget();

			/// <summary>
			/// Number of bits required to represent the largest coordinate value from among all points.
			/// If not positive, that value will be computed from the data itself.
			/// </summary>
			public int BitsPerDimension { get; set; } = -1;

			public override bool Equals(object obj)
			{
				return Equals(obj as IndexConfig);
			}

			public bool Equals(IndexConfig other)
			{
				if (other == null)
					return false;
				return BitsPerDimension == other.BitsPerDimension
					&& Budget.Equals(other.Budget);
			}
		}

		/// <summary>
		/// Parameters needed by ClusterCounter.
		/// </summary>
		public class ClusterCounterConfig: IEquatable<ClusterCounterConfig>
		{
			public int NoiseSkipBy { get; set; } = 10;
			public int ReducedNoiseSkipBy { get; set; } = 1;
			public int OutlierSize { get; set; } = 5;

			public override bool Equals(object obj)
			{
				return Equals(obj as ClusterCounterConfig);
			}

			public bool Equals(ClusterCounterConfig other)
			{
				if (other == null)
					return false;
				return NoiseSkipBy == other.NoiseSkipBy
				   && ReducedNoiseSkipBy == other.ReducedNoiseSkipBy
				   && OutlierSize == other.OutlierSize
				;
			}
		}

		public class HilbertClassifierConfig:IEquatable<HilbertClassifierConfig>
		{
			/// <summary>
			/// When reducing the first cut Clusters (from the HilbertIndex) to a smaller set, 
			/// this sets a limit on how many nearest neighboring clusters will be compared to each cluster.
			/// A low number increases speed and decreases accuracy.
			/// </summary>
			public int MaxNeighborsToCompare { get; set; } = 5;

			/// <summary>
			/// If true, the exact distance between clusters is computed, otherwise a faster approximation is used.
			/// </summary>
			public bool UseExactClusterDistance { get; set; } = false;

			/// <summary>
			/// This is multiplied by MergeSquareDistance to derive the maximum square distance that an outlier may be
			/// from a neighboring cluster and still be permitted to merge.
			/// </summary>
			public double OutlierDistanceMultiplier { get; set; } = 5;

			public override bool Equals(object obj)
			{
				return Equals(obj as HilbertClassifierConfig);
			}

			public bool Equals(HilbertClassifierConfig other)
			{
				if (other == null)
					return false;
				return MaxNeighborsToCompare == other.MaxNeighborsToCompare
					 && UseExactClusterDistance == other.UseExactClusterDistance
                     && Math.Abs(OutlierDistanceMultiplier - other.OutlierDistanceMultiplier) <= 0.001
				;
			}
		}

		/// <summary>
		/// Parameters needed for DensityClassifier.
		/// </summary>
		public class DensityClassifierConfig: IEquatable<DensityClassifierConfig>
		{
			/// <summary>
			/// Multiplied by the number of points to cluster to get UnmergeableSize.
			/// </summary>
			public double UnmergeableSizeFraction { get; set; } = 1.0 / 6.0;

			/// <summary>
			/// Multiplied by the MergeSquareDistance to get the NeighborhoodRadius.
			/// </summary>
			public double NeighborhoodRadiusMultiplier { get; set; } = 0.4;

			public int OutlierSize { get; set; } = 5;

			public override bool Equals(object obj)
			{
				return Equals(obj as DensityClassifierConfig);
			}

			public bool Equals(DensityClassifierConfig other)
			{
				if (other == null)
					return false;
				return Math.Abs(UnmergeableSizeFraction - other.UnmergeableSizeFraction) <= 0.0001
			       && Math.Abs(NeighborhoodRadiusMultiplier - other.NeighborhoodRadiusMultiplier) <= 0.0001
				   && OutlierSize == other.OutlierSize
				;
			}
		}

		public DataConfig Data { get; set; } = new DataConfig();
		public OutputConfig Output { get; set; } = new OutputConfig();
		public IndexConfig Index { get; set; } = new IndexConfig();
		public ClusterCounterConfig ClusterCounter { get; set; }  = new ClusterCounterConfig();
		public HilbertClassifierConfig HilbertClassifier { get; set; } = new HilbertClassifierConfig();
		public DensityClassifierConfig DensityClassifier { get; set; } = new DensityClassifierConfig();

		/// <summary>
		/// If the data is already classified, this is how close we need to be to declare success,
		/// where 1.0 means perfect.
		/// </summary>
		public double AcceptableBCubed { get; set; } = 0.98;

		#region Constructors and Factory methods

		public SlashConfig(string inputDataFileName, string outputDataFileName, string idField = "ROWNUM", string categoryField = "?")
		{
			Data.InputDataFile = inputDataFileName;
			Output.OutputDataFile = outputDataFileName;
			Data.IdField = idField;
			Data.CategoryField = categoryField;
		}

		/// <summary>
		/// Construct a SlashConfig from a YAML string like one created via ToString().
		/// </summary>
		/// <param name="yaml">Yaml formatted string representation.</param>
		public static SlashConfig Deserialize(string yaml)
		{
			Deserializer deserializer = new Deserializer();
			return deserializer.Deserialize<SlashConfig>(yaml);
		}

		/// <summary>
		/// Default constructor for deserialization of YAML.
		/// </summary>
		public SlashConfig(){}

		#endregion

		/// <summary>
		/// Serialize the configuration to a YAML string.
		/// </summary>
		public override string ToString()
		{
			var builder = new SerializerBuilder();
			builder.EmitDefaults(); // Force even default values to be written, like 0, false.
			var serializer = builder.Build();
			var strWriter = new StringWriter();
			serializer.Serialize(strWriter, this);
			return strWriter.ToString();
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as SlashConfig);
		}

		public bool Equals(SlashConfig other)
		{
			if (other == null)
				return false;
			return Data.Equals(other.Data)
			   && Output.Equals(other.Output)
			   && Index.Equals(other.Index)
			   && ClusterCounter.Equals(other.ClusterCounter)
			   && HilbertClassifier.Equals(other.HilbertClassifier)
			   && DensityClassifier.Equals(other.DensityClassifier)
		       && Math.Abs(AcceptableBCubed - other.AcceptableBCubed) <= 0.0001
			;
		}
	}
}
