using ClientOrderQueue.Lib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ClientOrderQueue.Lib
{
    [ValueConversion(typeof(double), typeof(double))]
    public class MultiplyParamValueConverter : IValueConverter
    {
        public double DefaultValue { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double dBuf = 0f, retVal = 0f;
            string sParam = parameter.ToString();

            dBuf = sParam.ToDouble();

            retVal = dBuf * System.Convert.ToDouble(value);
            if ((retVal == 0) && (DefaultValue != 0)) retVal = DefaultValue;

            return retVal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }

    // конвертер возвращает Thickness, параметры которого рассчиываются из переданного значения и строки коэффициентов сторон L-T-R-B
    [ValueConversion(typeof(double), typeof(Thickness))]
    public class GetMargin : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string[] param = ((string)parameter).Split(',');
            double left = 0.0, top = 0.0, right = 0.0, bottom = 0.0, val = (double)value;

            left = param[0].ToDouble() * val;
            top = param[1].ToDouble() * val;
            right = param[2].ToDouble() * val;
            bottom = param[3].ToDouble() * val;

            return new Thickness(left, top, right, bottom);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


}
