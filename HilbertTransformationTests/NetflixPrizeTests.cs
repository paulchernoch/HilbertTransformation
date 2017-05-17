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
        /// <summary>
        /// Verify that the Netflix Prize test data can be read, parsed, and converted into objects.
        /// </summary>
        [Test]
        public void LoadNetflixDataAndNotRunOutOfMemory()
        {
            var logDirectory = @"c:\temp\movie-data";
            Logger.SetupForTests(Path.Combine(logDirectory, "LoadNetflixData.log"));
            var netflixData = new NetFlixData(TestDataDirectory);
            Timer.Log();
            var filesRead = netflixData.Dimensions;
            var expectedMovieCount = 17770;
            var actualMovieCount = netflixData.Movies.Count;
            Assert.AreEqual(expectedMovieCount, actualMovieCount, $"{actualMovieCount} movies loaded, expected {expectedMovieCount}");
        }

        [Test]
        public void ClusterNetflixData()
        {
            var logDirectory = @"c:\temp\movie-data";
            Logger.SetupForTests(Path.Combine(logDirectory, "ClusterNetflixData.log"));
            var netflixData = new NetFlixData(TestDataDirectory);

            var classifier = new HilbertClassifier(netflixData.Points, 3);
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
