using System;

namespace Clustering
{
    /// <summary>
    /// Holds an item and the measure by which it should be sorted.
    /// </summary>
    /// <typeparam name="TItem">Item to be sorted.</typeparam>
    /// <typeparam name="TMeasure">Measure by which to sort the item.</typeparam>
    public class MeasuredItem<TItem, TMeasure> : IComparable<MeasuredItem<TItem, TMeasure>>, IComparable
        where TMeasure : IComparable<TMeasure> 
    {
        public TItem Item { get; private set; }
        public TMeasure Measure { get; private set; }
        private int _multiplier;

        /// <summary>
        /// Influences how GetHashCode values are used to break ties when sorting. 
        /// </summary>
        public bool ReverseHashCodeOrder
        {
            get { return  _multiplier == -1; }
            set { _multiplier = value ? -1 : 1; }
        }

        public MeasuredItem(TItem item, TMeasure measure, bool reverseHashCodeOrder = false)
        {
            Item = item;
            Measure = measure;
            ReverseHashCodeOrder = reverseHashCodeOrder;
        }

        public int CompareTo(MeasuredItem<TItem, TMeasure> other)
        {
            if (other == null) return -1;
            var cmp = Measure.CompareTo(other.Measure);
            return cmp != 0 ? cmp : _multiplier * Item.GetHashCode().CompareTo(other.Item.GetHashCode());
        }

        public int CompareTo(object obj)
        {
            return CompareTo(obj as MeasuredItem<TItem,TMeasure>);
        }
    }
}
