using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;


namespace IntegraLib
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

        public static double ToDouble(this string sParam)
        {
            double retVal = 0;
            double.TryParse(sParam, out retVal);
            if (retVal == 0)
            {
                if (sParam.Contains(",")) sParam = sParam.Replace(',', '.');

                double.TryParse(sParam, NumberStyles.Float, CultureInfo.InvariantCulture, out retVal);
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
            return (string.IsNullOrEmpty(source));
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

    }  // class


    public static class DateTimeExtension
    {
        public static void SetZero(this DateTime source)
        {
            source = DateTime.MinValue;
        }
        public static bool IsZero(this DateTime source)
        {
            return source.Equals(DateTime.MinValue);
        }

        public static string ToSQLExpr(this DateTime source)
        {
            return string.Format("CONVERT(datetime, '{0}', 20)", source.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        }

    } // class

    public static class TimeSpanExtension
    {
        public static void SetZero(this TimeSpan source)
        {
            source = TimeSpan.Zero;
        }
        public static bool IsZero(this TimeSpan source)
        {
            return source.Equals(TimeSpan.Zero);
        }

        public static DateTime ToDateTime(this TimeSpan source)
        {
            return new DateTime(source.Ticks, DateTimeKind.Local);
        }

        public static int ToIntSec(this TimeSpan source)
        {
            return (int)source.TotalSeconds;
        }

    }  // class

}
