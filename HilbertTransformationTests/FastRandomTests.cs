using HilbertTransformation.Random;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertTransformationTests
{
    [TestFixture]
    public class FastRandomTests
    {
        /// <summary>
        /// Verify that if the same seed is used, two FastRandom generators yield the same series of random numbers.
        /// </summary>
        [Test]
        public void Repeatability()
        {
            for(var iSeed = 0; iSeed < 100; iSeed++)
            {
                var r1 = new FastRandom(iSeed);
                var r2 = new FastRandom(iSeed);
                foreach(var i in Enumerable.Range(0, 1000))
                {
                    var random1 = r1.Next(100);
                    var random2 = r2.Next(100);
                    Assert.AreEqual(random1, random2, $"Difference found for seed {iSeed} and iteration {i}: {random1} vs {random2}");
                }
            }
        }
    }
}
