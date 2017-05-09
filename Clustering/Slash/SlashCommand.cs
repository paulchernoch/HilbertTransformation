using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HilbertTransformation;

namespace Clustering
{
	/// <summary>
	/// Command line program that performs clustering using the SLASH algorithm:
	/// Single-Link Agglomerative Scalable Hilbert clustering.
	/// </summary>
	public class SlashCommand
	{

		public enum CommandType { Help, Version, Define, Assess, Cluster, Recluster }

		public const string Version = "0.1";

		public static string HelpMessage = $@"
Slash Version {Version}

Purpose: Slash clusters high-dimensional data.

Usage: 1. slash [help | -h | -help]
       2. slash define [config-file] [input-data-file] [output-data-file]
       3. slash assess [config-file] [input-data-file] [output-data-file]
       4. slash cluster [config-file] [input-data-file] [output-data-file]
       5. slash recluster [config-file] [input-data-file] [output-data-file]
       6. slash version

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

       ASSESS. Assess the clustering tendency of the data. 
       If the data has not clustering tendency, it is fruitless to cluster it.
       The assessment will draw one of the following conclusions:

          a. Unclustered - The data is fairly randomly distributed with no large clusters.
          b. Singly Clustered - All points not in outlying groups are in a single, large cluster.
          c. Majority Clustered - Of the points that are not outliers, two thirds or more are in a single, large cluster.
          d. Weakly Clustered - Two-thirds or more of the points are outliers.
          e. Moderately Clustered - Between one- and two-thirds of the points are outliers.
          f. Highly Clustered - Fewer than one-third of the points are outliers.


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
";

		public string InputFile { get; set; }

		public string OutputFile { get; set; }

		public string ConfigFile { get; set; } = "slash.yaml";

		public SlashConfig Configuration { get; set; }

		public CommandType Command { get; set; }

		/// <summary>
		/// Maps a point to the id from the input file (which may be numeric or not).
		/// </summary>
		public Dictionary<UnsignedPoint, string> InputDataIds { get; set; }

		/// <summary>
		/// The points in the order they were read in. 
		/// </summary>
		public List<UnsignedPoint> InputOrder { get; set; }

		/// <summary>
		/// How the data was classified before running the clustering process.
		/// </summary>
		public Classification<UnsignedPoint, string> InitialClassification { get; set; }

		/// <summary>
		/// How the data is classified after the clustering pricess.
		/// </summary>
		public Classification<UnsignedPoint, string> FinalClassification { get; set; }

		/// <summary>
		/// The maximum merge distance discovered by the HilbertClassifier.
		/// </summary>
		long MergeSquareDistance { get; set; }

		/// <summary>
		/// Comparison between InitialClassification with FinalClassification.
		/// </summary>
		public ClusterMetric<UnsignedPoint,string> MeasuredChange { get; private set; }

        /// <summary>
        /// If the Assess command is executed, this will be set.
        /// </summary>
        public ClusteringTendency Assessor { get; private set; }

		#region Constructors

		/// <summary>
		/// Create a SlashCommand that processes command line arguments and loads the configuration frmo a file.
		/// </summary>
		/// <param name="args">Command line Arguments.</param>
		public SlashCommand(string[] args)
		{
			Command = ParseAction(args.Length == 0 ? "" : args[0]);
			ParseFiles(args);
		}

		/// <summary>
		/// Create a SlashCommand that is told which command to execute and is handed its configuration,
		/// so does not need to load it from a file.
		/// </summary>
		/// <param name="command">Command to execute.</param>
		/// <param name="configuration">Configuration tht specifies how the clustering is to be performed.</param>
		public SlashCommand(CommandType command, SlashConfig configuration)
		{
			Command = command;
			Configuration = configuration;
			InputFile = Configuration.Data.InputDataFile;
			OutputFile = Configuration.Output.OutputDataFile;
			InitLogger(Configuration);
		}

		#endregion

		/// <summary>
		/// Execute the command, potentially reading the configuration file and data file and writing the output file.
		/// </summary>
		public void Execute()
		{
			var alreadyHaveConfig = Configuration != null;
			switch (Command)
			{
				case CommandType.Help:
					Console.WriteLine(HelpMessage);
					break;
				case CommandType.Version:
					Console.WriteLine($@"Slash Version {Version}");
					break;
				case CommandType.Define:
					var definition = new SlashConfig(InputFile, OutputFile);
					File.WriteAllText(ConfigFile, definition.ToString());
					break;
                case CommandType.Assess:
                    if (!alreadyHaveConfig)
                        Configuration = LoadConfig();
                    Assess();
                    Timer.Log();
                    break;
				case CommandType.Cluster:
					if (!alreadyHaveConfig)
						Configuration = LoadConfig();
					Cluster();
					Timer.Log();
					break;
				case CommandType.Recluster:
					if (!alreadyHaveConfig)
						Configuration = LoadConfig();
					Recluster();
					Timer.Log();
					break;
			}
		}

        #region Assess Clustering tendency

        /// <summary>
        /// A fast command that reads a file of points and evaluates whether the data has a weak or string
        /// clustering tendency
        /// 
        /// Results are stored in Assessor.
        /// </summary>
        void Assess()
        {
            LoadData();
            Timer.Start("Assessing Clustering tendency");
            Assessor = new ClusteringTendency(InputOrder, Configuration.Index.Budget.OutlierSize);
            Timer.Stop("Assessing Clustering tendency");
            var resultsMessage = $"The clustering tendency of the {InputOrder.Count} input points has been evaluated:\n   {Assessor}";
            Logger.Info(resultsMessage);
            Console.WriteLine(resultsMessage);
        }

        #endregion

        #region Clustering

        /// <summary>
        /// Cluster the data, starting with an initially unclustered classification and combining points into fewer clusters.
        /// </summary>
        void Cluster()
		{
			LoadData();
			var classifier = new HilbertClassifier(InputOrder, Configuration.Index.BitsPerDimension)
			{
				IndexConfig = Configuration.Index.Budget,
				MaxNeighborsToCompare = Configuration.HilbertClassifier.MaxNeighborsToCompare,
				OutlierDistanceMultiplier = Configuration.HilbertClassifier.OutlierDistanceMultiplier,
				OutlierSize = Configuration.Index.Budget.OutlierSize,
				UseExactClusterDistance = Configuration.HilbertClassifier.UseExactClusterDistance
			};
			//TODO: Follow this with the DensityClassifier.
			Timer.Start("Classify by distance");
			FinalClassification = classifier.Classify();
			Timer.Stop("Classify by distance");
			MergeSquareDistance = classifier.MergeSquareDistance;
			ReclassifyByDensity();
            
            Logger.Info(Summary());
			SaveData();
		}

		/// <summary>
		/// Recluster the data, starting with an initial classification and further combining points into fewer clusters.
		/// </summary>
		void Recluster()
		{
			LoadData();
			var classifier = new HilbertClassifier(InitialClassification, Configuration.Index.BitsPerDimension)
			{
				IndexConfig = Configuration.Index.Budget,
				MaxNeighborsToCompare = Configuration.HilbertClassifier.MaxNeighborsToCompare,
				OutlierDistanceMultiplier = Configuration.HilbertClassifier.OutlierDistanceMultiplier,
				OutlierSize = Configuration.Index.Budget.OutlierSize,
				UseExactClusterDistance = Configuration.HilbertClassifier.UseExactClusterDistance
			};
			//TODO: Follow this with the DensityClassifier.
			Timer.Start("Classify by distance");
			FinalClassification = classifier.Classify();
			Timer.Stop("Classify by distance");
			MergeSquareDistance = classifier.MergeSquareDistance;
			ReclassifyByDensity();
			Logger.Info(Summary());
			SaveData();
		}

		string Summary()
		{
			return $"{FinalClassification.NumPoints} points with {InputOrder[0].Dimensions} dimensions were divided into {FinalClassification.NumPartitions} clusters";
		}

		/// <summary>
		/// Apply Density-based reclassification to the FinalClassification.
		/// This may cause some clusters to be split into smaller clusters.
		/// It will not cause any existing clusters to be merged.
		/// </summary>
		void ReclassifyByDensity()
		{
			// 0. Decide if we will be doing this or not, based on the configuration.
			if (!Configuration.DensityClassifier.SkipDensityClassification)
			{
				Timer.Start("Reclassify by density");
				var numberOfClustersSplit = 0;

				// 1. Loop through all clusters in FinalClassification
				// We will be modifying FinalClassification while iterating over it,
				// so we need to copy the list of labels up front.
				var classLabels = FinalClassification.ClassLabels().ToList();
				foreach (var clusterId in classLabels)
				{
					// 2. Decide if the cluster needs reclustering.
					if (NeedsReclustering(clusterId))
					{
						// 3. Obtain the members of the cluster and index them by the Hilbert curve
						var pointsToClassify = FinalClassification.PointsInClass(clusterId);
						var lookupPointById = new Dictionary<int, UnsignedPoint>();
						foreach (var p in pointsToClassify)
							lookupPointById[p.UniqueId] = p;
						int labelCounter = 1;
						var subClassification = new Classification<UnsignedPoint, string>(pointsToClassify, p => (labelCounter++).ToString());
						var hIndex = new HilbertIndex(subClassification, Configuration.Index.BitsPerDimension);

						// 4. Create a DensityClassifier, properly configured.
						var unmergeableSize = (int) (pointsToClassify.Count * Configuration.DensityClassifier.UnmergeableSizeFraction);
						var densityClassifier = new DensityClassifier(hIndex, MergeSquareDistance, unmergeableSize)
						{
						 	NeighborhoodRadiusMultiplier = Configuration.DensityClassifier.NeighborhoodRadiusMultiplier,
							OutlierSize = Configuration.DensityClassifier.OutlierSize,
							MergeableShrinkage = Configuration.DensityClassifier.MergeableShrinkage
						};

						// 5. Reclassify.
						//    This classification is in terms of HilbertPoints, so afterwards we will need to map them to
						//    their non-HilbertPoint, original UnsignedPoints.
						var densityClassification = densityClassifier.Classify();

						// 6. If the number of clusters made from the points is more than one...
						if (densityClassification.NumPartitions > 1)
						{
							numberOfClustersSplit++;

							// 7. ... loop through all HilbertPoints from cluster and find corresponding UnsignedPoints.
							foreach (var hPoint in densityClassification.Points())
							{
								var uPoint = lookupPointById[hPoint.UniqueId];

								// Form the new class label by appending the previous label and the density-based label.
								var previousClassLabel = FinalClassification.GetClassLabel(uPoint);
								var densityClassLabel = densityClassification.GetClassLabel(hPoint);
								var newClassLabel = $"{previousClassLabel}-{densityClassLabel}";

								// 8. Pull point from its current cluster and add it to a new cluster
								FinalClassification.Remove(uPoint);
								FinalClassification.Add(uPoint, newClassLabel);
							}
						}
					}
				}
				Timer.Stop("Reclassify by density");
				Logger.Info($"Clusters split due to density-based reclassification: {numberOfClustersSplit}");
			}
		}

		/// <summary>
		/// A cluster needs reclustering if it is oddly-shaped, weakly-connected.
		/// </summary>
		/// <returns><c>true</c>, if reclustering is needsed, <c>false</c> otherwise.</returns>
		/// <param name="clusterId">Cluster identifier for a cluster in FinalClassification.</param>
		bool NeedsReclustering(string clusterId)
		{
			//TODO: Figure out how to triage clusters and decide which are oddly-shaped and need reclustering.
			return true;
		}

		#endregion

		#region File I/O: IsDataLoaded, LoadData, SaveData, etc.

		bool IsDataLoaded { get { return InitialClassification != null && InitialClassification.NumPoints > 0; } }

		/// <summary>
		/// Load the data from a file or standard input unless it has already been loaded into InitialClassification.
		/// 
		/// It must have a header row if Configuration.Data.ReadHeader is true.
		/// Field values may be separated by commas or tabs.
		/// </summary>
		void LoadData()
		{
			if (IsDataLoaded)
				return;
			Timer.Start("Load data");
			InitialClassification = new Classification<UnsignedPoint, string>();
			InputOrder = new List<UnsignedPoint>();
			InputDataIds = new Dictionary<UnsignedPoint, string>();
			IEnumerable<string> lines;
			if (Configuration.Data.ReadFromStandardIn())
				lines = ReadLinesFromConsole();
			else 
				lines = File.ReadLines(Configuration.Data.InputDataFile);
			var idPosition = -1;
			var categoryPosition = -1;
			if (!Configuration.Data.ReadHeader) {
				idPosition = SafeParseInt(Configuration.Data.IdField, -1);
				categoryPosition = SafeParseInt(Configuration.Data.CategoryField, -1);
			}
			var rownum = Configuration.Data.ReadHeader ? 0 : 1;
			string[] header = null;
			foreach (var values in lines.Select(line => line.Split(new [] { ',', '\t' })))
			{
				// Skip blank lines and comments
				if (values.Length < 2)
					continue;
				if (rownum == 0 && Configuration.Data.ReadHeader)
				{
					// Identify which columns hold the Id and the Category, if any.
					// If no column holds the Id, then the one-based row number will be used.
					// Regardless of whether the file has a header row, row number one
					// is the first row with data, not column headings.
					header = values;
					var tryIdPosition = Array.FindIndex(
						header,
						heading => heading.ToUpperInvariant().Equals(Configuration.Data.IdField.ToUpperInvariant())
					);
					if (tryIdPosition != -1)
						idPosition = tryIdPosition;
					var tryCategoryPosition = Array.FindIndex(
						header,
						heading => heading.ToUpperInvariant().Equals(Configuration.Data.CategoryField.ToUpperInvariant())
					);
					if (tryCategoryPosition != -1)
						categoryPosition = tryCategoryPosition;
				}
				else {
					int id;
					string idString;
					if (idPosition == -1)
					{
						id = rownum;
						idString = rownum.ToString();
					}
					else 
					{
						// If the id is not a number, we use the rownum in the points we create as the id, but 
						// make a correspondence between the string and the point.
						id = SafeParseInt(values[idPosition], rownum);
						idString = values[idPosition];
					}
					string categoryString;
					if (categoryPosition == -1)
						categoryString = rownum.ToString(); // Unclassified - all points in their own cluster.
					else 
						categoryString = values[categoryPosition];
					
					var coordinates = new List<uint>();
					foreach (var pair in values.Select((v, i) => new { Value = v, Position = i }))
					{
						//TODO: Reject negative values and log.
						if (pair.Position != idPosition && pair.Position != categoryPosition)
							coordinates.Add((uint)SafeParseInt(pair.Value, 0));
					}
					var point = new UnsignedPoint(coordinates, id);
					//TODO: Check for duplicate ids and log.
					InputDataIds[point] = idString;
					InitialClassification.Add(point, categoryString);
					InputOrder.Add(point);
				}
				rownum++;
			}
			Timer.Stop("Load data");
		}

		/// <summary>
		/// Load data directly, not via a file.
		/// </summary>
		/// <param name="initialClassification">Initial classification.</param>
		/// <param name="originalOrder">Original order. If omitted, an arbitrary ordering of points will be defined.</param>
		public void LoadData(Classification<UnsignedPoint,string> initialClassification, IList<UnsignedPoint> originalOrder = null)
		{
			InitialClassification = initialClassification;
			if (originalOrder == null)
				InputOrder = InitialClassification.Points().ToList();
			else
				InputOrder = originalOrder.ToList();
			InputDataIds = new Dictionary<UnsignedPoint, string>();
			foreach (var point in InitialClassification.Points())
			{
				InputDataIds[point] = point.UniqueId.ToString();
			}
		}

		/// <summary>
		/// If configured to write to a file, points will be written to the output file or standard out 
		/// in the order they were loaded, with categories attached.
		/// The results will also be recorded in MeasuredChange.
		/// </summary>
		/// <returns>True if the data was saved, false otherwise.
		/// MeasuredChange is set in either case.
		/// </returns>
		bool SaveData()
		{
			var dataWasSaved = false;
			if (Configuration.Output.ShouldWrite())
			{
				Timer.Start("Save data");
				TextWriter writer;
				bool shouldCloseWriter;
				var d = ","; // Field delimiter
				if ((Configuration.Output.OutputDataFile ?? "").Length > 1)
				{
					writer = new StreamWriter(File.OpenWrite(Configuration.Output.OutputDataFile));
					shouldCloseWriter = true;
				}
				else
				{
					writer = Console.Out;
					shouldCloseWriter = false;
				}

				try
				{
					if (Configuration.Output.WriteHeader)
					{
						// Write the header record.
						writer.Write($"id{d}category");
						for (var i = 0; i < InputOrder[0].Dimensions; i++)
							writer.Write($"{d}col{i}");
						writer.WriteLine();
					}

					// Write the points.
					foreach (var point in InputOrder)
						writer.WriteLine(PointToRecord(point, d));
				}
				finally
				{
					if (shouldCloseWriter)
						writer.Close();
				}
				dataWasSaved = true;
				Timer.Stop("Save data");
			}
			RecordResult();
			return dataWasSaved;
		}


		/// <summary>
		/// Format a point as a delimited string record, without the terminating newline.
		/// </summary>
		/// <returns>The record.</returns>
		/// <param name="point">Point to format.</param>
		/// <param name="fieldDelimiter">Field delimiter.</param>
		string PointToRecord(UnsignedPoint point, string fieldDelimiter = ",")
		{
			var category = FinalClassification.GetClassLabel(point);
			var id = InputDataIds[point];

			var sb = new StringBuilder();
			sb.Append(id).Append(fieldDelimiter).Append(category);
			foreach (var coordinate in point.LazyCoordinates())
			{
				sb.Append(fieldDelimiter).Append(coordinate);
			}
			return sb.ToString();
		}

		static int SafeParseInt(string s, int defaultIfUnparseable)
		{
			int i = defaultIfUnparseable;
			if ((s ?? "").Length == 0)
				return i;
			Int32.TryParse(s, out i);
			return i;
		}

		public static IEnumerable<string> ReadLinesFromConsole()
		{
			string line;
			while (null != (line = Console.ReadLine()))
				yield return line;
		}

		#endregion

		#region Comparing InitialClassification to FinalClassification

		/// <summary>
		/// Compare the initial and final classifications and set MeasuredChange.
		/// </summary>
		/// <returns>The result of the comparison.</returns>
		ClusterMetric<UnsignedPoint, string> RecordResult()
		{
			// Compute the BCubed score between the initial and final classification IF there was an initial classification.
			if (InitialClassification.NumPartitions != InitialClassification.NumPoints)
			{
				Timer.Start("Compare classifications");
				MeasuredChange = CompareClassifications();
				Timer.Stop("Compare classifications");
				// Write comparison to the log.
				Logger.Info($"Comparison between initial and final classification: {MeasuredChange}. Acceptable: {Configuration.AcceptableBCubed}");
			}
			else {
				MeasuredChange = new ClusterMetric<UnsignedPoint, string>()
				{
					Precision = 0,
					Recall = 0
				};
			}
			return MeasuredChange;
		}

		/// <summary>
		/// Compare the original and final classifications to see how different they are.
		/// </summary>
		ClusterMetric<UnsignedPoint, string> CompareClassifications()
		{
			return InitialClassification.Compare(FinalClassification);
		}

		/// <summary>
		/// Returns true if clustering has completed and the comparison between InitialClassification and FinalClassification
		/// has been performed and the result is a BCubed value that is not less than Configuration.AcceptableBCubed.
		/// </summary>
		public bool IsClassificationAcceptable
		{
			get
			{
				if (FinalClassification == null)
					return false;
				if (InitialClassification == null)
					return false;
				if (MeasuredChange == null)
					return false;
				if (Configuration.AcceptableBCubed > MeasuredChange.BCubed)
					return false;
				return true;
			}
		}

		#endregion

		#region Parse Command Line, Load Configuration

		/// <summary>
		/// Load the configuration file.
		/// 
		/// If the InputFile was specified on the command line, adjust the configuration to use it,
		/// otherwise change InputFile to reflect what was read in from the config file.
		/// 
		/// Do the same for the OutputFile.
		/// </summary>
		/// <returns>The configuration.</returns>
		SlashConfig LoadConfig()
		{
			//TODO: Valid configuration, checking for existence of input file, config file.
			var definitionText = File.ReadAllText(ConfigFile);
			var configuration = SlashConfig.Deserialize(definitionText);
			if ((InputFile ?? "").Length != 0)
				configuration.Data.InputDataFile = InputFile;
			else
				InputFile = configuration.Data.InputDataFile;
			if ((OutputFile ?? "").Length != 0)
				configuration.Output.OutputDataFile = OutputFile;
			else
				OutputFile = configuration.Output.OutputDataFile;
			InitLogger(configuration);
			return configuration;
		}

		void InitLogger(SlashConfig configuration)
		{
			Logger.Instance.LogFile = configuration.Output.LogFile;
			Logger.Instance.Level = configuration.Output.LogLevel;
			Logger.Instance.WriteToFile = configuration.Output.LogFile.Length > 1;
			Logger.Instance.WriteToStandardOut = configuration.Output.LogFile.Equals("-");
			Logger.Instance.WriteToStandardError = configuration.Output.LogFile.Equals("-");
		}

		CommandType ParseAction(string action)
		{
			CommandType command;
			// Permit leading hyphens because people love their hyphens.
			// Permit using just the first letter of the command.
			var helpPattern = new Regex(@"^-*h(elp)?$", RegexOptions.IgnoreCase);
			var versionPattern = new Regex(@"^-*v(ersion)?$", RegexOptions.IgnoreCase);
            var assessPattern = new Regex(@"^-*a(ssess)?", RegexOptions.IgnoreCase);
			var clusterPattern = new Regex(@"^-*c(luster)?$", RegexOptions.IgnoreCase);
			var definePattern = new Regex(@"^-*d(efine)?$", RegexOptions.IgnoreCase);
			var reclusterPattern = new Regex(@"^-*r(ecluster)?$", RegexOptions.IgnoreCase);

			if (action.Length == 0)
				command = CommandType.Help;
			else if (helpPattern.IsMatch(action))
				command = CommandType.Help;
			else if (versionPattern.IsMatch(action))
				command = CommandType.Version;
			else if (definePattern.IsMatch(action))
				command = CommandType.Define;
            else if (assessPattern.IsMatch(action))
                command = CommandType.Assess;
            else if (clusterPattern.IsMatch(action))
				command = CommandType.Cluster;
			else if (reclusterPattern.IsMatch(action))
				command = CommandType.Recluster;
			else
				command = CommandType.Help;
			return command;
		}

	    void ParseFiles(string[] args) 
		{ 
			if (args.Length >= 2)
				ConfigFile = args[1];
			if (args.Length >= 3)
				InputFile = args[2];
			if (args.Length >= 4)
				OutputFile = args[3];
		}

		#endregion
 	}
}
