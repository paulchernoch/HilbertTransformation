using Clustering;
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
        /// Verify that the Netflix Prize test data can be read, parsed, and converted into objects.
        /// </summary>
        [Test]
        public void LoadNetflixDataAndNotRunOutOfMemory()
        {
            // NOTE: Test data is not stored in source control - too many files.
            //       Hardcoded to look under the temp directory.
            var testDataDirectory = @"c:\temp\movie-data\training_set";
            var logDirectory = @"c:\temp\movie-data";
            Logger.SetupForTests(Path.Combine(logDirectory, "LoadNetflixData.log"));
            var netflixData = new NetFlixData(testDataDirectory);
            Timer.Log();
            var filesRead = netflixData.Dimensions;
            var expectedMovieCount = 17770;
            var actualMovieCount = netflixData.Movies.Count;
            Assert.AreEqual(expectedMovieCount, actualMovieCount, $"{actualMovieCount} movies loaded, expected {expectedMovieCount}");
        }
    }
}
