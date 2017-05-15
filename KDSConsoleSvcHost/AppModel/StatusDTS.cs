using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSConsoleSvcHost.AppModel
{
    // внутренняя структура для хранения даты входа в состояние и времени нахождения в нем
    // DTS - DateTime and TimeSpan
    internal struct StatusDTS
    {
        public DateTime DateEntered { get; set; }
        public int TimeStanding { get; set; }

        public StatusDTS(DateTime? dt, int? seconds)
        {
            DateEntered = dt ?? DateTime.MinValue;
            TimeStanding = seconds ?? 0;
        }

        public override bool Equals(object obj)
        {
            StatusDTS obj2 = (StatusDTS)obj;
            return DateEntered.Equals(obj2.DateEntered) && TimeStanding.Equals(obj2.TimeStanding);
        }
        public override int GetHashCode()
        {
            return DateEntered.GetHashCode() + TimeStanding.GetHashCode();
        }

    }  // class
}
