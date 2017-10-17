using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegraLib
{
    // словарь глобальных свойств
    public static class AppProperties
    {
        private static Dictionary<string, object> _props;

        static AppProperties()
        {
            _props = new Dictionary<string, object>();
        }

        public static void SetProperty(string key, object value)
        {
            if (_props.ContainsKey(key))
            {
                _props[key] = value;
            }
            else
            {
                _props.Add(key, value);
            }
        }

        public static object GetProperty(string key)
        {
            if (_props.ContainsKey(key)) return _props[key];
            else return null;
        }

        public static bool GetBoolProperty(string key)
        {
            if (_props.ContainsKey(key)) return Convert.ToBoolean(_props[key]);
            else return false;
        }
        public static int GetIntProperty(string key)
        {
            if (_props.ContainsKey(key)) return Convert.ToInt32(_props[key]);
            else return 0;
        }
        public static double GetDoubleProperty(string key)
        {
            if (_props.ContainsKey(key)) return Convert.ToDouble(_props[key]);
            else return 0d;
        }

        public static void DeleteProperty(string key)
        {
            if (_props.ContainsKey(key)) _props.Remove(key);
        }

        public static bool ContainsKey(string key)
        {
            return _props.ContainsKey(key);
        }

    }  // class
}
