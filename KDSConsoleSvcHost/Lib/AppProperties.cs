using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.Lib
{
    public class AppProperties
    {
        private Dictionary<string, object> _props;

        public AppProperties()
        {
            _props = new Dictionary<string, object>();
        }

        public void SetProperty(string key, object value)
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

        public object GetProperty(string key)
        {
            if (_props.ContainsKey(key)) return _props[key];
            else return null;
        }

        public bool GetBoolProperty(string key)
        {
            return Convert.ToBoolean(_props[key]);
        }

        public void DeleteProperty(string key)
        {
            if (_props.ContainsKey(key)) _props.Remove(key);
        }

        public  bool ContainsKey(string key)
        {
            return _props.ContainsKey(key);
        }

    }  // class
}
