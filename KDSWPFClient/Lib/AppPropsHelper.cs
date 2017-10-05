using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace KDSWPFClient.Lib
{

    // класс, который хранит свойства приложения в словаре System.Windows.Application.Current.Properties

    public static class AppPropsHelper
    {
        // получить глобальное значение приложения из его свойств
        public static object GetAppGlobalValue(string key, object defaultValue = null)
        {
            IDictionary dict = System.Windows.Application.Current.Properties;
            if (dict == null) return null;

            if (dict.Contains(key) == false) return defaultValue;
            else return dict[key];
        }

        // установить глобальное значение приложения (в свойствах приложения)
        public static void SetAppGlobalValue(string key, object value)
        {
            IDictionary dict = System.Windows.Application.Current.Properties;
            if (dict.Contains(key) == false)  // если еще нет значения в словаре
            {
                dict.Add(key, value);   // то добавить
            }
            else    // иначе - изменить существующее
            {
                dict[key] = value;
            }
        }

    }  // class
}
