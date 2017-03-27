using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections;
using System.Windows;

namespace ClientOrderQueue.Lib
{
    public static class AppLib
    {
        // общий логгер
        public static NLog.Logger AppLogger;

        static AppLib()
        {
            // логгер приложения
            AppLogger = NLog.LogManager.GetCurrentClassLogger();
        }

        #region app logger
        public static void WriteLogTraceMessage(string msg)
        {
            if ((bool)AppLib.GetAppGlobalValue("IsWriteTraceMessages")) AppLogger.Trace(string.Format("{0}: {1}", DateTime.Now.ToString(), msg));
        }
        public static void WriteLogTraceMessage(string format, params string[] values)
        {
            if ((bool)AppLib.GetAppGlobalValue("IsWriteTraceMessages")) AppLogger.Trace(format, values);
        }

        public static void WriteLogInfoMessage(string msg)
        {
            AppLogger.Info(msg);
        }
        public static void WriteLogInfoMessage(string format, params string[] values)
        {
            AppLogger.Info(format, values);
        }

        public static void WriteLogErrorMessage(string msg)
        {
            AppLogger.Error(msg);
        }
        public static void WriteLogErrorMessage(string format, params string[] values)
        {
            AppLogger.Error(format, values);
        }
        #endregion

        #region system info
        // in Mb
        public static int getAvailableRAM()
        {
            int retVal = 0;

            // class get memory size in kB
            System.Management.ManagementObjectSearcher mgmtObjects = new System.Management.ManagementObjectSearcher("Select * from Win32_OperatingSystem");
            foreach (var item in mgmtObjects.Get())
            {
                //System.Diagnostics.Debug.Print("FreePhysicalMemory:" + item.Properties["FreeVirtualMemory"].Value);
                //System.Diagnostics.Debug.Print("FreeVirtualMemory:" + item.Properties["FreeVirtualMemory"].Value);
                //System.Diagnostics.Debug.Print("TotalVirtualMemorySize:" + item.Properties["TotalVirtualMemorySize"].Value);
                retVal = (Convert.ToInt32(item.Properties["FreeVirtualMemory"].Value)) / 1024;
            }
            return retVal;
        }

        public static string GetAppFileName()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.ManifestModule.Name;
        }

        public static string GetAppFullFile()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.Location;
        }

        public static string GetAppDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetFullFileName(string fileName)
        {
            return getImagePath() + fileName;
        }
        private static string getImagePath()
        {
            string imgPath = AppLib.GetAppSetting("ImagesPath");
            if (string.IsNullOrEmpty(imgPath))  // путь не указан в конфиге - берем путь приложения
                imgPath = AppLib.GetAppDirectory();
            else if (imgPath.Contains(@"\:"))   // абсолютный путь
            { }
            else  // относительный путь
            {
                imgPath = AppLib.GetAppDirectory() + imgPath;
            }
            if (imgPath.EndsWith(@"\") == false) imgPath += @"\";
            return imgPath;
        }
        #endregion

        #region app settings
        // получить настройки приложения из config-файла
        public static string GetAppSetting(string key)
        {
            if (ConfigurationManager.AppSettings.AllKeys.Any(k => k.ToLower().Equals(key.ToLower())) == true)
                return ConfigurationManager.AppSettings.Get(key);
            else
                return null;
        }

        // получить глобальное значение приложения из его свойств
        public static object GetAppGlobalValue(string key, object defaultValue = null)
        {
            IDictionary dict = App.Current.Properties;
            if (dict.Contains(key) == false) return defaultValue;
            else return dict[key];
        }

        // установить глобальное значение приложения (в свойствах приложения)
        public static void SetAppGlobalValue(string key, object value)
        {
            IDictionary dict = App.Current.Properties;
            if (dict.Contains(key) == false)  // если еще нет значения в словаре
            {
                dict.Add(key, value);   // то добавить
            }
            else    // иначе - изменить существующее
            {
                dict[key] = value;
            }
        }

        #endregion

        #region WPF UI interface
        public static bool IsAppVerticalLayout
        {
            get
            {
                double appWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
                double appHeight = (double)AppLib.GetAppGlobalValue("screenHeight");
                return (appWidth < appHeight);
            }
        }


        #endregion

    }
}
