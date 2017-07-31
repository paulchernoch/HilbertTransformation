using Clustering;
using HilbertTransformation;
using HilbertTransformationTests.Data.NetflixReviews;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Math;

namespace HilbertTransformationTests
{
    [TestFixture]
    public class NetflixPrizeTests
    {
        /// <summary>
        /// NOTE: Test data is not stored in source control - too many files.
        ///       Hardcoded to look under the temp directory.
        /// </summary>
        public static string TestDataDirectory = @"c:\temp\movie-data\training_set";

        public static string ProbeDataDirectory = @"c:\temp\movie-data";

        public static string LogDirectory = @"c:\temp\movie-data";

        private NetFlixData AllNetflixData;

        [SetUp]
        public void BeforeTest()
        {
            Logger.SetupForTests(Path.Combine(LogDirectory, "LoadNetflixData.log"));
            AllNetflixData = new NetFlixData(TestDataDirectory, ProbeDataDirectory);
            Timer.Log();
        }

        /// <summary>
        /// Verify that the Netflix Prize test data can be read, parsed, and converted into objects.
        /// </summary>
        [Test]
        public void LoadNetflixDataAndNotRunOutOfMemory()
        {
            var filesRead = AllNetflixData.Dimensions;
            var expectedMovieCount = 17770;
            var actualMovieCount = AllNetflixData.Movies.Count;
            Assert.AreEqual(expectedMovieCount, actualMovieCount, $"{actualMovieCount} movies loaded, expected {expectedMovieCount}");
        }

        [Test]
        public void RMSErrorFromChoosingMeanRatingsIsCorrect()
        {
            var expectedRmsError = 1.0519; // Rounded
            var actualRmsError = AllNetflixData.RMSError;
            Assert.LessOrEqual(Abs(expectedRmsError - actualRmsError), 0.001, $"{actualRmsError} RMS error found, expected {expectedRmsError}");
        }

        [Test]
        public void ClusterNetflixData()
        {
            throw new Exception("Test not fully written");
            var classifier = new HilbertClassifier(AllNetflixData.Points, 3);
            classifier.IndexConfig.IndexCount = 1;
            Timer.Start("Clustering Netflix data");
            var actualClusters = classifier.Classify();
            Timer.Stop("Clustering Netflix data");

            //TODO: Design a measure of success.

            LogCLusterStats(actualClusters);
            Timer.Log();
        }

        private void LogCLusterStats(Classification<UnsignedPoint,string> clusters)
        {
            var message = $"{clusters.NumPoints} points in {clusters.NumPartitions} clusters, of which {clusters.NumLargePartitions(100)} had more than 100 members. Effective number = {clusters.NumEffectivePartitions}";
            Logger.Info(message);
        }


    }
}
