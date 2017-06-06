using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSWPFClient.Lib
{
    public static class DebugTimer
    {
        private static DateTime _dtInit;
        private static string _message;

        public static void Init(string message = null)
        {
            _dtInit = DateTime.Now;
            if (message != null)
            {
                _message = message;
                Debug.Print("{0}, start date: {1}", _message, _dtInit);
            }
        }

        public static string GetInterval()
        {
            DateTime dtEnd = DateTime.Now;
            string sInterval = (dtEnd - _dtInit).ToString();
            if (_message != null) Debug.Print("{0}, end date: {1}, interval: {2}", _message, dtEnd, sInterval);

            return sInterval;
        }

    }
}
