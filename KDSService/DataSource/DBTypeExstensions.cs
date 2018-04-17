using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.DataSource
{
    public static class DBTypesExtensions
    {
        public static int ToInt(this DataRow source, string fieldName)
        {
            if (source.IsNull(fieldName))
                return 0;
            else
                return System.Convert.ToInt32(source[fieldName]);
        }

        public static decimal ToDecimal(this DataRow source, string fieldName)
        {
            if (source.IsNull(fieldName))
                return 0m;
            else
                return System.Convert.ToDecimal(source[fieldName], System.Globalization.CultureInfo.InvariantCulture);
        }

        public static DateTime ToDateTime(this DataRow source, string fieldName)
        {
            if (source.IsNull(fieldName))
                return DateTime.MinValue;
            else
                return System.Convert.ToDateTime(source[fieldName], System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool ToBool(this DataRow source, string fieldName)
        {
            if (source.IsNull(fieldName))
                return false;
            else
                return System.Convert.ToBoolean(source[fieldName], System.Globalization.CultureInfo.InvariantCulture);
        }

    }  // class DBTypesExtensions
}
