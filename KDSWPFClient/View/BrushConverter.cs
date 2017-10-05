using IntegraLib;
using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace KDSWPFClient.View
{
    [ValueConversion(typeof(object), typeof(Brush))]
    public class BrushConverter : IValueConverter
    {
        private static Brush _defaultBrush = Brushes.White;

        // в value ключ для основной пары кистей, в parameter - тип кисти: "back" для фона и "fore" для текста
        // также можно передать ссылку на OrderDishViewModel для более точного получения кисти
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return _defaultBrush;

            Brush retVal = null;
            Dictionary<string, BrushesPair> appBrushes = BrushHelper.AppBrushes;

            string key = null;

            if (value is string)
                key = value.ToString();
            else if ((value is OrderStatusEnum) || (value is StatusEnum))
                key = value.ToString();
            else if (value is OrderDishViewModel)
                key = (value as OrderDishViewModel).Status.ToString();

            if (!key.IsNull() && appBrushes.ContainsKey(key) && (parameter != null))
            {
                string[] aParam = parameter.ToString().Split(';');
                BrushesPair brPair = appBrushes[key];

                retVal = (aParam[0] == "fore") ? brPair.Foreground : brPair.Background;
            }

            return (retVal == null) ? _defaultBrush : retVal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class BrushOrderHeaderConverter : IMultiValueConverter
    {
        private static Brush _defaultBrush = Brushes.White;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            StatusEnum status1 = (StatusEnum)values[0]; // статус заказа из БД
            StatusEnum status2 = (StatusEnum)values[1]; // статус заказа по блюдам, разрешенным на КДСе
                                                        
            Brush retVal = null;
            Dictionary<string, BrushesPair> appBrushes = BrushHelper.AppBrushes;
            string key = null;
            
            if (((bool)AppPropsHelper.GetAppGlobalValue("IsShowOrderStatusByAllShownDishes")) 
                && (status2 != StatusEnum.None) && (status2 != StatusEnum.WaitingCook) && (status2 != status1))
                key = status2.ToString();
            else
                key = status1.ToString();

            if (!key.IsNull() && appBrushes.ContainsKey(key) && (parameter != null))
            {
                string[] aParam = parameter.ToString().Split(';');
                BrushesPair brPair = appBrushes[key];

                retVal = (aParam[0] == "fore") ? brPair.Foreground : brPair.Background;
            }

            return (retVal == null) ? _defaultBrush : retVal;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
