using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.Lib
{
    public static class DebugTimer
    {
        private static DateTime _dtInit;
        private static string _message;
        private static bool _isDebugPrint;

        public static void Init(string message = null, bool isDebugPrint = true)
        {
            _dtInit = DateTime.Now;
            _message = message;
            _isDebugPrint = isDebugPrint;

            if (_isDebugPrint) Debug.Print("{0}start date: {1}", (_message==null ? "" : _message + ", "), _dtInit);
        }

        public static string GetInterval()
        {
            DateTime dtEnd = DateTime.Now;
            string sInterval = (dtEnd - _dtInit).ToString();

            if (_isDebugPrint) Debug.Print("{0}end date: {1}, interval: {2}", (_message == null ? "" : _message + ", "), dtEnd, sInterval);

            return sInterval;
        }

    }
}
