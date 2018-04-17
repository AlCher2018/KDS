using ClientOrderQueue.Lib;
using IntegraLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

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
            string param = parameter.ToString();
            if (string.IsNullOrEmpty(param)) return new Thickness(0);

            if (param.Contains(';')) param = param.Replace(';', ',');
            string[] aparam = ((string)parameter).Split(',');
            double left = 0.0, top = 0.0, right = 0.0, bottom = 0.0, val = (double)value;

            if (aparam.Length == 1)
            {
                left = aparam[0].ToDouble() * val;
                top = aparam[0].ToDouble() * val;
                right = aparam[0].ToDouble() * val;
                bottom = aparam[0].ToDouble() * val;
            }
            else if (aparam.Length <= 3)
            {
                left = aparam[0].ToDouble() * val;
                top = aparam[1].ToDouble() * val;
                right = aparam[0].ToDouble() * val;
                bottom = aparam[1].ToDouble() * val;
            }
            else
            {
                left = aparam[0].ToDouble() * val;
                top = aparam[1].ToDouble() * val;
                right = aparam[2].ToDouble() * val;
                bottom = aparam[3].ToDouble() * val;
            }

            return new Thickness(left, top, right, bottom);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // возвращает минимальное из массива double-значений
    [ValueConversion(typeof(double[]), typeof(double))]
    public class GetMinValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double[] doubleValues = new double[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                doubleValues[i] = values[i].ToString().ToDouble();
            }

            return Math.Floor(doubleValues.Min());
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }



    // возвращает радиус из минимального значения переданного массива
    public class GetCornerRadiusConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double[] doubleValues = new double[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                doubleValues[i] = values[i].ToString().ToDouble();
            }

            double radius = 0;
            if (parameter != null) radius = Math.Floor(doubleValues.Min()) * parameter.ToString().ToDouble();

            return new CornerRadius(radius);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // возвращает поля (Thickness) из актуальной ширины/высоты (элементы массива 0, 1), 
    // умноженных на строку коэффициентов (элементы массива 2-5)
    public class CalculatedMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3) return new Thickness(0);

            double[] doubleValues = new double[values.Length];
            doubleValues[0] = values[0].ToString().ToDouble();  // width
            doubleValues[1] = values[1].ToString().ToDouble();  // height

            if (string.IsNullOrEmpty(values[2].ToString())) return new Thickness(0);

            string sMargs = values[2].ToString();
            if (sMargs.Contains(';')) sMargs = sMargs.Replace(';', ',');
            string[] aKoefStr = sMargs.Split(',');
            double[] aKoefDbl = new double[aKoefStr.Length];
            for (int i = 0; i < aKoefStr.Length; i++)
            {
                aKoefDbl[i] = aKoefStr[i].ToDouble();
            }
            double kL = 0, kT = 0, kR = 0, kB = 0;
            if (aKoefDbl.Length == 1) { kL = kT = kR = kB = aKoefDbl[0]; }
            else if (aKoefDbl.Length <= 3) { kL = kR = aKoefDbl[0]; kT = kB = aKoefDbl[1]; }
            else if (aKoefDbl.Length == 4)
            {
                kL = aKoefDbl[0]; kT = aKoefDbl[1];
                kR = aKoefDbl[2]; kB = aKoefDbl[3];
            }
            double margLeft = doubleValues[0] * kL;
            double margTop = doubleValues[1] * kT;
            double margRight = doubleValues[0] * kR;
            double margBottom = doubleValues[1] * kB;

            return new Thickness(margLeft, margTop, margRight, margBottom);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }



    public class GetDarkerColorConverter : IValueConverter
    {
        public object Convert(object values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.GetType() != typeof(SolidColorBrush)) return Brushes.Gray;

            float darkKoef = 0.6f; // the less the darker
            Color col = (values as SolidColorBrush).Color;
            Color col1 = Color.Multiply(col, darkKoef); col1.A = 255;
            return new SolidColorBrush(col1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


}
