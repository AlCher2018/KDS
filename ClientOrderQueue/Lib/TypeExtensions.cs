using ClientOrderQueue.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ClientOrderQueue.Lib
{
    public static class StringExtensions
    {
        // convert string to bool
        public static bool ToBool(this string source)
        {
            bool retValue = false;
            if (string.IsNullOrEmpty(source)) return retValue;

            string sLower = source.ToLower();

            if (sLower.Equals("true") || sLower.Equals("да") || sLower.Equals("yes") || sLower.Equals("истина"))
                retValue = true;
            else
            {
                int iBuf = 0;
                if (int.TryParse(source, out iBuf) == true) retValue = (iBuf != 0);
            }

            return retValue;
        }  // method

        public static double ToDouble(this string source)
        {
            double retVal = 0d;
            if (source == null) return retVal;
            string sVal = source;

            double.TryParse(sVal, out retVal);
            if (retVal == 0)
            {
                if (sVal.Contains(".")) sVal = sVal.Replace('.', ',');
                else if (sVal.Contains(",")) sVal = sVal.Replace(',', '.');
                double.TryParse(sVal, out retVal);
            }
            return retVal;
        }

        public static int ToInt(this string source)
        {
            if (source == null) return 0;

            List<char> chList = new List<char>();
            foreach (char c in source)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.DecimalDigitNumber) chList.Add(c);
            }
            return (chList.Count > 0) ? int.Parse(string.Join("", chList.ToArray())) : 0;
        }

        public static bool IsNull(this string source)
        {
            string retVal = null;
            if (!string.IsNullOrEmpty(source)) retVal = source.Trim();

            return string.IsNullOrEmpty(retVal);
        }

        public static bool IsNumber(this string source)
        {
            return source.All(c => char.IsDigit(c));
        }

    } // class

    public static class IntExtensions
    {
        public static int SetBit(this int bitMask, int bit)
        {
            return (bitMask |= (1 << bit));
        }
        public static int ClearBit(this int bitMask, int bit)
        {
            return (bitMask &= ~(1 << bit));
        }
        public static bool IsSetBit(this int bitMask, int bit)
        {
            int val = (1 << bit);
            return (bitMask & val) == val;
        }
    }

    public static class UIElementExtensions
    {
        private static Action EmptyDelegate = delegate () { };

        public static void Refresh(this UIElement uiElement)
        {
            uiElement.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
        }
    }

}
