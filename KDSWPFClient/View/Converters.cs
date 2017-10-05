using IntegraLib;
using KDSWPFClient.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;


namespace KDSWPFClient.View
{

    [ValueConversion(typeof(string), typeof(bool))]
    public class IsNullConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value == null);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(int), typeof(string))]
    public class IsZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int iValue = System.Convert.ToInt32(value);
            return (iValue <= 0) ? "" : iValue.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


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
            double left = 5d, top = 3d, right = 0d, bottom = 0d, val = (double)value;
            string sParam = (string)parameter;
            string[] aParam = null;
            if (sParam.Contains(',')) aParam = ((string)parameter).Split(',');
            else aParam = ((string)parameter).Split(';');

            if (aParam != null)
            {
                left = aParam[0].ToDouble() * val;
                top = (aParam.Length < 2) ? aParam[0].ToDouble() * val : aParam[1].ToDouble() * val;
                right = (aParam.Length < 3) ? aParam[0].ToDouble() * val : aParam[2].ToDouble() * val;
                bottom = (aParam.Length < 4) ? aParam[0].ToDouble() * val : aParam[3].ToDouble() * val;
            }

            return new Thickness(left, top, right, bottom);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    [ValueConversion(typeof(int), typeof(CornerRadius))]
    public class GetCornerRadius : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //Debug.Print((string)parameter);
            double left = 0.0, top = 0.0, right = 0.0, bottom = 0.0, val = (double)value;
            if (parameter != null)
            {
                string sParam = (string)parameter;
                string[] aParam = null;
                if (sParam.Contains(',')) aParam = ((string)parameter).Split(',');
                else aParam = ((string)parameter).Split(';');

                if (aParam != null)
                {
                    left = aParam[0].ToDouble() * val;
                    top = (aParam.Length < 2) ? aParam[0].ToDouble() * val : aParam[1].ToDouble() * val;
                    right = (aParam.Length < 3) ? aParam[0].ToDouble() * val : aParam[2].ToDouble() * val;
                    bottom = (aParam.Length < 4) ? aParam[0].ToDouble() * val : aParam[3].ToDouble() * val;
                }
            }

            return new CornerRadius(left, top, right, bottom);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    // преобразование даты
    [ValueConversion(typeof(DateTime), typeof(string))]
    public class ViewDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is DateTime)) return "no DateTime type";

            DateTime dt = (DateTime)value;

            if (dt.Equals(DateTime.MinValue))
                return "no data";
            else if (DateTime.Now.Day != dt.Day)  // показать и дату создания заказа
            {
                return dt.ToString("dd.MM.yyyy HH:mm:ss");
            }
            else  // показать только время создания заказа
            {
                return dt.ToString("HH:mm:ss");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


}
