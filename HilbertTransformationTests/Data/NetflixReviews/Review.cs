using System;
using System.Collections.Generic;


namespace HilbertTransformationTests.Data.NetflixReviews
{
    /// <summary>
    /// Indicates the Rating that a Reviewer gave a movie.
    /// </summary>
    public struct Review
    {
        /// <summary>
        /// Both the ReviewerId and the Rating will be stored in the same field
        /// to save memory.
        /// </summary>
        private uint _bothFields;

        public int ReviewerId {
            get
            {
                return (int) (_bothFields >> 8);
            }
            set
            {
                _bothFields = ((uint)value << 8) | (_bothFields & 0xff);
            }
        }

        /// <summary>
        /// Movie rating (from 1 for worst to 5 for best).
        /// </summary>
        public int Rating
        {
            get { return (int)(_bothFields & 0xff); }
            set { _bothFields = (_bothFields & 0xffffff00) | (uint)value; }
        }

        public override string ToString()
        {
            return $"Reviewer {ReviewerId} rated the movie a {Rating}";
        }
    }
}
