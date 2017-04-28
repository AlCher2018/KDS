using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDS.ViewModel
{
    public enum CookPhaseEnum
    {
        Wait = 0,
        InProcess = 1,
        Finished = 2,
        Took = 3,
        Cancelled = 4,
        Return = 5
    }
}
