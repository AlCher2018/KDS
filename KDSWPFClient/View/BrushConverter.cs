using KDSWPFClient.Lib;
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
    [ValueConversion(typeof(string), typeof(Brush))]
    public class BrushConverter : IValueConverter
    {
        // в value ключ для основной пары кистей, в parameter - тип кисти: "back" для фона и "fore" для текста
        // для дополнительной пары в parameter через ";" надо указать ключ дополн.пары 
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Brushes.WhiteSmoke;

            Brush retVal = null;
            Dictionary<string, BrushesPair> appBrushes = (Dictionary<string, BrushesPair>)AppLib.GetAppGlobalValue("appBrushes");
            string key = value.ToString();
            if (!key.IsNull() && appBrushes.ContainsKey(key) && (parameter != null))
            {
                string[] aParam = parameter.ToString().Split(';');
                BrushesPair brPair = appBrushes[key];  // основная пара
                // дополнительная пара
                if ((aParam.Length > 1) && (brPair.SubDictionary != null) && (brPair.SubDictionary.ContainsKey(aParam[1])))
                    brPair = brPair.SubDictionary[aParam[1]];
                 
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
