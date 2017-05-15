using HilbertTransformation;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;

namespace HilbertTransformationTests.Data.NetflixReviews
{
    public class Movie
    {
        public int MovieId { get; set; }

        /// <summary>
        /// Reviews of the movie, keyed by the ReviewerId.
        /// </summary>
        public Dictionary<int, Review> Reviews { get; set; } = new Dictionary<int, Review>();

        public int Count { get { return Reviews.Count; } }

        /// <summary>
        /// Zero-based positions of Reviewers when sorted in Hilbert order.
        /// </summary>
        public List<int> ReviewerPositionsSorted { get; private set; } = new List<int>();

        /// <summary>
        /// Ids of Reviewers corresponding to positions in ReviewerPositionsSorted.
        /// </summary>
        public List<int> ReviewerIdsSorted { get; private set; } = new List<int>();

        public double MeanRating { get; private set; } 

        public Movie(int movieId)
        {
            MovieId = movieId;
        }

        public void Add(Review review)
        {
            Reviews[review.ReviewerId] = review;
        }

        public void DoneAdding(Dictionary<int, Reviewer> reviewersById)
        {
            MeanRating = Reviews.Values.Select(r => r.Rating).Average();
            SortReviews(reviewersById);
        }

        private void SortReviews(Dictionary<int,Reviewer> reviewersById)
        {
            foreach(var reviewer in Reviews.Keys
                .Select(revId => reviewersById[revId])
                .OrderBy(reviewer => reviewer.HilbertOrder)
            )
            {
                ReviewerPositionsSorted.Add(reviewer.HilbertOrder);
                ReviewerIdsSorted.Add(reviewer.ReviewerId);
            }
        }

        /// <summary>
        /// From among the Reviewers who have reviewed this Movie, find those that have the 
        /// most similar opinions about other movies, if such an overlap exists.
        /// </summary>
        /// <param name="reviewer">Reviewer whose opinion of the Movie we do not know and hope to guess.</param>
        /// <param name="k">Number of similar Reviewers to search for.</param>
        /// <param name="window">Half of the maximum number of Reviewers to compare. 
        /// If Count is not greater than twice the window, all Reviewers of this Movie will be checked. 
        /// Otherwise, the closest Reviewer according to a Binary search by Hilbert position will
        /// be checked, plus a number of Reviewers immediately before it in Hilbert order equal to the window size, and 
        /// an identical number after it in Hilbert order.
        /// </param>
        /// <param name="reviewersById">This can be all the known Reviewers or just those drawn from a single cluster if the data has been clustered.
        /// This set contains not just those that have reviewed this Movie.</param>
        /// <returns>Up to K Reviewers whose opinions are similar to the given Reviewer, drawn from an intersection
        /// between reviewersById and the set of reviewers for this movie,
        /// sorted from nearest to farthest, associated with the square distance between the Reviewer and its neighbor.
        /// The List may be empty, if no overlap exists.</returns>
        public List<Tuple<Reviewer,long>> KNearest(Reviewer reviewer, int k, int window, Dictionary<int, Reviewer> reviewersById)
        {
            var point = reviewer.Point;
            // NOTE: This assumes that both the Point and the HilbertOrder for each Reviewer has already been computed.
            if (point == null)
                throw new NullReferenceException($"The Point for reviewer {reviewer.ReviewerId} has not yet been calculated.");
            if (reviewer.HilbertOrder < 0)
                throw new ApplicationException($"Reviewer {reviewer.ReviewerId} has not yet had its HilbertOrder calculated.");
            IEnumerable<int> idsToSort;
            var overlappingReviewerIds = ReviewerIdsSorted
                .Where(id => reviewersById.ContainsKey(id))
                .ToList();
            if (!overlappingReviewerIds.Any())
                return new List<Tuple<Reviewer, long>>();
            if (overlappingReviewerIds.Count <= 2*k)
            {
                // Sort all reviewers of this movie by distance from near to far.
                idsToSort = overlappingReviewerIds;
            }
            else
            {
                // Only sort some reviewers, those within window positions on either side
                // of the insertion point where this reviewer would go if added to the list
                // in Hilbert order.
                var insertionPosition = overlappingReviewerIds.BinarySearch(reviewer.HilbertOrder);
                if (insertionPosition < 0)
                    insertionPosition = ~insertionPosition;
                var lowPosition = Max(0, insertionPosition - window);
                idsToSort = overlappingReviewerIds.Skip(lowPosition).Take(2 * window);
            }
            return idsToSort
                .Select(revId => reviewersById[revId])
                .Select(r => new Tuple<Reviewer, long>(r, point.Measure(r.Point)))
                .OrderBy(tup => tup.Item2)
                .Take(k)
                .ToList();
        }

        /// <summary>
        /// Simple way of inferring a rating, weighting each neighboring Review's Rating
        /// in proportion to how close it is to the test reviewer.
        /// 
        /// Example:
        ///   If k = 10, the most similar Reviewer is weighted ten, the next is weighted nine,
        ///   on to the least similar Reviewer, whose weight is one.
        /// </summary>
        /// <param name="reviewer">Reviewer whose Rating for this Movie is sought.</param>
        /// <param name="k">Number of near neighbors to include in the computation.</param>
        /// <param name="window">The k nearest by Euclidean distance will be chosen from among the
        /// 2*window nearest according to the Hilbert ordering.</param>
        /// <param name="reviewersById">This can be all the known Reviewers or just those drawn from a single cluster if the data has been clustered.
        /// This set contains not just those that have reviewed this Movie.</param>
        /// <returns>An estimated Rating between one and five.</returns>
        public double InferRating(Reviewer reviewer, int k, int window, Dictionary<int, Reviewer> reviewersById)
        {
            if (Reviews.ContainsKey(reviewer.ReviewerId))
            {
                // No need to estimate; we have the actual value here!
                return Reviews[reviewer.ReviewerId].Rating;
            }
            var nearest = KNearest(reviewer, k, window, reviewersById);

            if (!nearest.Any())
                return MeanRating;
            
            var denominator = k * (k + 1) / 2;
            var numerator = Enumerable.Range(0, nearest.Count)
                .Select(i => Reviews[nearest[i].Item1.ReviewerId].Rating * (Count - i))
                .Sum();
            return (double)numerator / denominator;
        }

    }
}
