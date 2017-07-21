using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HilbertTransformationTests.Data
{
    public interface Interpolator<T>
    {
        T Y(T x);
    }
}
