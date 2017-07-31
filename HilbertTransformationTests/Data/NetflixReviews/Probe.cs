using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertTransformationTests.Data.NetflixReviews
{
    public class Probe
    {
        /// <summary>
        /// The key is a movie id, the value, a list of reviewer ids for reviewers whose movie review 
        /// for the corresponding movie we must guess.
        /// </summary>
        public Dictionary<int, List<int>> ReviewersByMovie { get; private set; } = new Dictionary<int, List<int>>();

        public Probe(string filename)
        {
            LoadFromFile(filename);
        }

        /// <summary>
        /// Load the probe set from a file.
        /// 
        /// Lines that end with a colon contain a movie id. (The colon is removed.)
        /// All other lines contain a single reviewer id.
        /// </summary>
        /// <param name="filename"></param>
        private void LoadFromFile(string filename)
        {
            var lines = File.ReadLines(filename);
            List<int> currentMovieReviewList = null;
            foreach(var line in lines.Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                if (line.EndsWith(":"))
                {
                    var currentMovieId = int.Parse(line.Replace(":", ""));
                    currentMovieReviewList = new List<int>();
                    ReviewersByMovie[currentMovieId] = currentMovieReviewList;
                }
                else
                {
                    currentMovieReviewList.Add(int.Parse(line));
                }
            }
        }
    }
}
