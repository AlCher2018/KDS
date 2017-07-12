using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSWPFClient.ViewModel
{
    public enum StatusEnum
    {
        None = -1,
        WaitingCook = 0,
        Cooking = 1,
        Ready = 2,
        Took = 3,
        Cancelled = 4,
        Commit = 5,
        CancelConfirmed = 6,
        Transferred = 7,
        ReadyConfirmed = 8,
        YesterdayNotTook = 9
    }
}
