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
        // в value ключ для основной пары кистей, в parameter - тип кисти: "back" для фона и "fore" для текста
        // для дополнительной пары в parameter через ";" надо указать ключ дополн.пары 
        // также можно передать ссылку на OrderDishViewModel для более точного получения кисти
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Brushes.White;

            var v1 = AppLib.GetAppGlobalValue("appBrushes");
            if (v1 == null) return Brushes.White;

            Brush retVal = null;
            Dictionary<string, BrushesPair> appBrushes = (v1 as Dictionary<string, BrushesPair>);

            string key=null;

            if (value is string)
                key = value.ToString();
            else if ((value is OrderStatusEnum) || (value is StatusEnum))
                key = value.ToString();
            else if (value is OrderDishViewModel)
                key = (value as OrderDishViewModel).Status.ToString();

            if (!key.IsNull() && appBrushes.ContainsKey(key) && (parameter != null))
            {
                string[] aParam = parameter.ToString().Split(';');
                BrushesPair brPair = appBrushes[key];  // основная пара

                // дополнительная пара
                if ((aParam.Length > 1) && (brPair.SubDictionary != null) && (brPair.SubDictionary.ContainsKey(aParam[1])))
                    brPair = brPair.SubDictionary[aParam[1]];
                else if (value is OrderDishViewModel)
                {
                    OrderDishViewModel dish = (value as OrderDishViewModel);
                    // варианты кистей для состояний
                    if (dish.Status == StatusEnum.WaitingCook)
                    {
                        if (dish.DelayedStartTime != 0) brPair = brPair.SubDictionary["estimateStart"];  // кисти ожидания, если есть "готовить через"
                        else if (dish.EstimatedTime != 0) brPair = brPair.SubDictionary["estimateCook"];  // кисти ожидания, если есть "время готовки"
                    }
                }
                 
                retVal = (aParam[0] == "fore") ? brPair.Foreground : brPair.Background;
            }

            return (retVal == null) ? Brushes.White : retVal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
