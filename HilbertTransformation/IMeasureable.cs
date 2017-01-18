using System;

namespace HilbertTransformation
{
    /// <summary>
    /// An IMeasurable can be measured relative to a reference object, and such measurements may be compared 
    /// to the measurements taken from other objects.
    /// 
    /// For example, when finding all points that are near neighbors to a reference point, 
    /// the Cartesian distance between the potential neighbors and the reference point is the measure.
    /// </summary>
    /// <typeparam name="TReference">Type of object to be compared to the IMeasurable.</typeparam>
    /// <typeparam name="TMeasurement">Type of measurement taken, which can be compared to other measurements.</typeparam>
    public interface IMeasurable<in TReference, out TMeasurement> where TMeasurement : IComparable<TMeasurement>
    {
        TMeasurement Measure(TReference reference);
    }
}
