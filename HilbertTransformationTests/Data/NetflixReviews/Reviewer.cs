using HilbertTransformation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HilbertTransformationTests.Data.NetflixReviews
{
    /// <summary>
    /// A Netflix movie reviewer holds ratings for different movies.
    /// </summary>
    public class Reviewer
    {
        public int ReviewerId { get; set; }

        /// <summary>
        /// Ids of every movie rated by this Reviewer, in ascending order.
        /// The lowest possible Id is one.
        /// </summary>
        public List<int> MovieIds { get; set; }

        /// <summary>
        /// Review (a value from 1 to 5) for each corresponding movie.
        /// </summary>
        public List<byte> Ratings { get; set; }

        /// <summary>
        /// Once all Reviewers are fully assembled and sorted in Hilbert order, this is the
        /// zero-based position of the Reviewer in the sorted list.
        /// </summary>
        public int HilbertOrder { get; set; }


        public Reviewer(int reviewerId)
        {
            ReviewerId = reviewerId;
            MovieIds = new List<int>();
            Ratings = new List<byte>();
            HilbertOrder = -1;
        }

        /// <summary>
        /// Add a new review and maintain the MovieIds in sorted order.
        /// </summary>
        /// <param name="movieId">Movie that weas reviewed.
        /// If this reviewer already has a review for the movie, replace it with the new rating.
        /// </param>
        /// <param name="rating">Rating of the movie, from one to five.</param>
        public void Add(int movieId, uint rating)
        {
            if (Count == 0 || MovieIds.Last() < movieId)
            {
                MovieIds.Add(movieId);
                Ratings.Add((byte)rating);
            }
            else
            {
                var position = MovieIds.BinarySearch(movieId);
                if (position >= 0)
                {
                    Ratings[position] = (byte)rating;
                }
                else
                {
                    // It is not possible for the position to be after the end of the array,
                    // because we already tested for the case where the movieId is larger than the last value.
                    position = ~position;
                    MovieIds.Insert(position, movieId);
                    Ratings.Insert(position, (byte)rating);
                }
            }
        }

        public int Count { get { return MovieIds.Count; } }

        public SparsePoint Point { get; private set; }

        /// <summary>
        /// Create a point from a sparse set of (x,y) pairs where the x is the MovieId minus one (to make it zero-based) and the
        /// y is the Rating.
        /// </summary>
        /// <param name="dimensions">Total number of dimensions, including those which are missing a value, hence have 
        /// no corresponding pair (MovieId,Rating).</param>
        /// <returns>A new SparsePoint, whose UniqueId is the ReviewerId.</returns>
        public SparsePoint ToPoint(int dimensions)
        {
            if (Point == null)
                Point = new SparsePoint(
                    MovieIds.Select(movieId => movieId - 1), 
                    Ratings.Select(rating => (uint)rating).ToList(), 
                    dimensions,
                    0U, 
                    ReviewerId
                );
            return Point;
        }

        public override string ToString()
        {
            return $"Reviewer {ReviewerId} rated {Count} movies.";
        }
    }
}
