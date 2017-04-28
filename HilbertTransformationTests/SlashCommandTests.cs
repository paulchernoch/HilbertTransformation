using System;
using System.IO;
using Clustering;
using HilbertTransformationTests.Data;
using NUnit.Framework;

namespace HilbertTransformationTests
{
	[TestFixture]
	public class SlashCommandTests
	{
		/// <summary>
		/// Prepare the SlashConfig and data without reading from files,
		/// and cluster the data without writing the results, and skipping over
		/// density-based classification.
		/// </summary>
		[Test]
		public void ClusterWithoutFiles()
		{
			var bitsPerDimension = 10;
			var data = new GaussianClustering
			{
				ClusterCount = 20,
				Dimensions = 50,
				MaxCoordinate = (1 << bitsPerDimension) - 1,
				MinClusterSize = 200,
				MaxClusterSize = 600
			};
			var expectedClassification = data.MakeClusters();

			var config = new SlashConfig() { AcceptableBCubed = 0.98 };
			config.Index.BitsPerDimension = bitsPerDimension;
			config.UseNoFiles();
			var command = new SlashCommand(SlashCommand.CommandType.Cluster, config)
			{
				InputFile = null,
				OutputFile = null
			};
			command.Configuration.DensityClassifier.SkipDensityClassification = true;
			// Need to put this here, because the command initializes the logger differently.
			Logger.SetupForTests(null);
			command.LoadData(expectedClassification);

			command.Execute();

			Assert.IsTrue(command.IsClassificationAcceptable, $"The BCubed value of {command.MeasuredChange.BCubed} was not good enough.");
		}

		/// <summary>
		/// Use the SlashCommand to read a data file that already has categories, perform clustering, 
		/// but do not write the results to an output file.
		/// Skip over density-based classification.
		/// </summary>
		[Test]
		public void ReadCategorizedButDontWriteResults()
		{
			var runDir = AppDomain.CurrentDomain.BaseDirectory;
			var inputDataFile = Path.Combine(runDir, "Data/N1024_D128_K16.txt");
			Console.WriteLine($"Looking for datafile here: {inputDataFile}");
			// Largest value < 256
			var bitsPerDimension = 10;
			var config = new SlashConfig() { AcceptableBCubed = 0.98 };
			config.Index.BitsPerDimension = bitsPerDimension;
			config.Data = new SlashConfig.DataConfig()
			{
				InputDataFile = inputDataFile,
				IdField = "id",
				CategoryField = "category",
				ReadHeader = true
			};
			config.DensityClassifier.SkipDensityClassification = true;
			config.Output.OutputDataFile = null;
			var command = new SlashCommand(SlashCommand.CommandType.Cluster, config)
			{
				OutputFile = null
			};
			// Need to put this here, because the command initializes the logger differently.
			Logger.SetupForTests(null);
			command.Execute();

			Assert.IsTrue(command.IsClassificationAcceptable, $"The BCubed value of {command.MeasuredChange.BCubed} was not good enough.");
		}

        /// <summary>
        /// Use the SlashCommand to read a data file that already has categories, and Assess its clustering tendency, 
        /// but do not write the results to an output file.
        /// </summary>
        [Test]
        public void AssessCommand()
        {
            var runDir = AppDomain.CurrentDomain.BaseDirectory;
            var inputDataFile = Path.Combine(runDir, "Data/N1024_D128_K16.txt");
            Console.WriteLine($"Looking for datafile here: {inputDataFile}");
            // Largest value < 256
            var bitsPerDimension = 10;
            var config = new SlashConfig() { AcceptableBCubed = 0.98 };
            config.Index.BitsPerDimension = bitsPerDimension;
            config.Data = new SlashConfig.DataConfig()
            {
                InputDataFile = inputDataFile,
                IdField = "id",
                CategoryField = "category",
                ReadHeader = true
            };
            config.DensityClassifier.SkipDensityClassification = true;
            config.Output.OutputDataFile = null;
            var command = new SlashCommand(SlashCommand.CommandType.Assess, config)
            {
                OutputFile = null
            };
            // Need to put this here, because the command initializes the logger differently.
            Logger.SetupForTests(null);
            command.Execute();
            var assessment = command.Assessor.HowClustered;
            Assert.AreEqual(ClusteringTendency.ClusteringQuality.HighlyClustered, assessment, $"Expected HighlyClustered, got {command.Assessor}");
        }
    }
}
