using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace waitCopy
{
    internal class AppArgs
    {
        private const string keyChars = "-/\\";

        private static Dictionary<string, string> _args;
        public static Dictionary<string, string> Args { get { return _args; } }


        static AppArgs()
        {
            _args = new Dictionary<string, string>();
        }

        // сложить аргументы приложения в словарь
        public static void PutArgs(string[] args)
        {
            if (_args.Count > 0) _args.Clear();

            string key = null, value = null;
            char firstChar;
            foreach (string item in args)
            {
                firstChar = item[0];
                if (keyChars.Contains(firstChar))
                {
                    if (key != null) saveArg(key, value);
                    key = item.Substring(1); value = null;
                }
                else if (value == null)
                {
                    value = item;
                }
            }
            if (key != null) saveArg(key, value);
        }

        // проверить существование аргумента
        public static bool IsExists(string arg)
        {
            return _args.ContainsKey(arg);
        }

        // получить значение аргумента
        public static string GetValue(string arg)
        {
            if (_args.ContainsKey(arg))
                return _args[arg];
            else
                return null;
        }

        private static void saveArg(string key, string value)
        {
            if (_args.ContainsKey(key))
                _args[key] = value;
            else
                _args.Add(key, value);
        }


    }  // class
}
