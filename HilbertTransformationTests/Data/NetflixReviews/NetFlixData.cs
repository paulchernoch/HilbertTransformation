using Clustering;
using HilbertTransformation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertTransformationTests.Data.NetflixReviews
{
    public class NetFlixData
    {

        public Dictionary<int, Reviewer> ReviewersById { get; set; } = new Dictionary<int, Reviewer>();

        public List<Reviewer> ReviewersSorted { get; set; }

        public List<Movie> Movies { get; set; } = new List<Movie>();

        public int Dimensions { get { return Movies.Count; } }

        public List<SparsePoint> Points { get; set; }

        public NetFlixData(string dataDirectory)
        {
            var title = "Load Netflix Movie data";
            Timer.Start(title);
            var numMoviesLoaded = LoadFiles(dataDirectory);
            Timer.Stop(title);
            title = "Make SparsePoints for Netflix Movie data";
            Timer.Start(title);
            Points = ReviewersById.Values.Select(r => r.ToPoint(Dimensions)).ToList();
            Timer.Stop(title);
        }

        public string RatingFilePrefix { get; set; } = "mv_";
        public string RatingFileSuffix { get; set; } = "txt";

        private string MovieReviewFileName(int movieId)
        {
            var paddedId = movieId.ToString().PadLeft(7, '0');
            return $"{RatingFilePrefix}{paddedId}.{RatingFileSuffix}";
        }

        /// <summary>
        /// Load all Movie review files, assuming an unbroken sequence from one to the the maximum
        /// MovieId.
        /// </summary>
        /// <param name="dataDirectory">Directory in which moview review data files reside.</param>
        /// <returns>Number of files successfully read.</returns>
        public int LoadFiles(string dataDirectory)
        {
            var movieId = 0;
            var foundFile = true;
            while (foundFile)
            {
                movieId++;
                var movieFilePath = Path.Combine(dataDirectory, MovieReviewFileName(movieId));
                foundFile = LoadFile(movieFilePath);
            }
            return movieId - 1; // Last successfully read movie file, since the current one was the first to fail.
        }

        /// <summary>
        /// Read a Movie data file.
        /// 
        /// Format of file:
        ///    First line has MovieId followed by a colon.
        ///    Subsequent lines have: ReviewerId,Rating,ReviewDate
        /// </summary>
        /// <param name="dataFile">Name of file to read.</param>
        /// <returns>True if successfully read, false if no such file or failed to read.</returns>
        public bool LoadFile(string dataFile)
        {
            int movieId = 0;
            try
            {
                var lines = File.ReadLines(dataFile);
                movieId = Int32.Parse(lines.First().Trim().Replace(":", ""));
                var movie = new Movie(movieId);
                Movies.Add(movie);
                foreach (var fields in lines.Skip(1).Select(line => line.Trim().Split(new[] { ',' })))
                {
                    // Skip the ReviewDate. Not yet using this information.
                    var review = new Review {
                        ReviewerId = Int32.Parse(fields[0]),
                        Rating = Int32.Parse(fields[1])
                    };
                    movie.Add(review);
                    if (!ReviewersById.TryGetValue(review.ReviewerId, out Reviewer reviewer))
                    {
                        reviewer = new Reviewer(review.ReviewerId);
                        ReviewersById[review.ReviewerId] = reviewer;
                    }
                    reviewer.Add(movieId, (uint)review.Rating);
                }   
            }
            catch(Exception e) {
                Logger.Error($"LoadFile failed for file {dataFile} with error: {e.Message}");
                return false;
            }
            return true;
        }
    }
}
