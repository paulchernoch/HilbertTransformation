using Clustering;
using HilbertTransformation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static System.Math;
using System.Threading.Tasks;

namespace HilbertTransformationTests.Data.NetflixReviews
{
    public class NetFlixData
    {

        public Dictionary<int, Reviewer> ReviewersById { get; set; } = new Dictionary<int, Reviewer>();

        public List<Reviewer> ReviewersSorted { get; set; }

        /// <summary>
        /// Movies, sorted by MovieId. Since there should be no gaps and MovieIds begin at one, to find a movie by id,
        /// use Movies[movieId-1].
        /// </summary>
        public List<Movie> Movies { get; set; } = new List<Movie>();

        public Probe ReviewsToGuess { get; set; }

        public int Dimensions { get { return Movies.Count; } }

        public List<UnsignedPoint> Points { get; set; }

        public double RMSError { get; private set; }

        public NetFlixData(string dataDirectory, string probeDataDirectory)
        {
            HyperContrastedPoint.Cache.Resize(30000);
            var title = "Load Netflix Movie data";
            Timer.Start(title);
            var numMoviesLoaded = LoadFiles(dataDirectory);
            Timer.Stop(title);

            title = "Load Probe";
            Timer.Start(title);
            var probeFilename = Path.Combine(probeDataDirectory, "probe.txt");
            ReviewsToGuess = new Probe(probeFilename);
            Timer.Stop(title);

            title = "Time to compute RMS Error for Mean";
            Timer.Start(title);
            RMSError = ComputeRMSErrorForMean();
            var message = $"Value of RMS Error for mean = {RMSError}";
            Logger.Info(message);
            Timer.Stop(title);

            title = "Make SparsePoints for Netflix Movie data";
            Timer.Start(title);
            Points = ReviewersById.Values.Select(r => r.ToPoint(Dimensions)).ToList();
            Timer.Stop(title);

            // Cache statistics
            Logger.Info($"Cache Hit Ratio: {HyperContrastedPoint.Cache.HitRatio}. Hits = {HyperContrastedPoint.Cache.Hits}");
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
            foreach (var movie in Movies)
            {
                movie.DoneAdding(ReviewersById);
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

        public int? GetReview(int reviewerId, int movieId)
        {
            var reviewer = ReviewersById[reviewerId];
            return reviewer.Review(movieId);
        }

        /// <summary>
        /// Compute the RMS error that we see if we use the mean rating in place of the true rating for all probe queries.
        /// </summary>
        /// <returns>The root mean square error across all probe queries if we use the mean rating for each movie in place of the true rating.</returns>
        private double ComputeRMSErrorForMean()
        {
            var squareError = 0.0;
            var queryCount = 0;
            foreach(var query in ReviewsToGuess.ReviewersByMovie.Select(pair => new { MovieId = pair.Key, ReviewerIds = pair.Value }))
            {
                var movie = Movies[query.MovieId - 1];
                var meanReview = movie.MeanRating;
                foreach (var reviewer in query.ReviewerIds.Select(reviewerId => ReviewersById[reviewerId]))
                {
                    var trueReview = GetReview(reviewer.ReviewerId, query.MovieId);
                    if (trueReview == null)
                        throw new ApplicationException($"Expected reviewer {reviewer.ReviewerId} to have a review for movie {query.MovieId}");
                    
                    squareError += (trueReview.Value - meanReview) * (trueReview.Value - meanReview);
                    queryCount++;
                }
            }
            return Sqrt(squareError / queryCount);
        }
    }
}
