using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;


namespace ClientOrderQueue.Lib
{
    public static class AppLib
    {
        // общий логгер
        public static NLog.Logger AppLogger;

        static AppLib()
        {
            // логгер приложения
            AppLogger = NLog.LogManager.GetLogger("appLogger");
        }

        #region app logger
        // отладочные сообщения
        public static void WriteLogTraceMessage(string msg)
        {
            if (AppLib.GetAppSetting("IsWriteTraceMessages").ToBool() && AppLogger.IsTraceEnabled)
                AppLogger.Trace(msg ?? "null");
        }
        public static void WriteLogTraceMessage(string format, params object[] args)
        {
            if (AppLib.GetAppSetting("IsWriteTraceMessages").ToBool() && AppLogger.IsTraceEnabled)
                AppLogger.Trace(format, args);
        }

        // сообщения о действиях пользователя
        public static void WriteLogUserAction(string msg)
        {
            if (AppLib.GetAppSetting("IsLogUserAction").ToBool() && AppLogger.IsTraceEnabled) AppLogger.Trace("userAct: " + msg);
        }
        public static void WriteLogUserAction(string format, params object[] paramArray)
        {
            if (AppLib.GetAppSetting("IsLogUserAction").ToBool() && AppLogger.IsTraceEnabled) AppLogger.Trace("userAct: " + format, paramArray);
        }

        public static void WriteLogInfoMessage(string msg)
        {
            if (AppLogger.IsInfoEnabled) AppLogger.Info(msg ?? "null");
        }
        public static void WriteLogInfoMessage(string format, params object[] args)
        {
            if (AppLogger.IsInfoEnabled) AppLogger.Info(format, args);
        }

        public static void WriteLogErrorMessage(string msg)
        {
            if (AppLogger.IsErrorEnabled) AppLogger.Error(msg ?? "null");
        }
        public static void WriteLogErrorMessage(string format, params object[] args)
        {
            if (AppLogger.IsErrorEnabled) AppLogger.Error(format, args);
        }
        public static void WriteLogErrorShortMessage(Exception ex)
        {
            if (AppLogger.IsErrorEnabled) AppLogger.Error(GetShortErrMessage(ex));
        }

        #endregion

        #region system info
        internal static string GetEnvironmentString()
        {
            return string.Format("machine={0}, user={1}, current directory={2}, OS version={3}, isOS64bit={4}, processor count={5}, free RAM={6} Mb",
                Environment.MachineName, Environment.UserName, Environment.CurrentDirectory, Environment.OSVersion, Environment.Is64BitOperatingSystem, Environment.ProcessorCount, getAvailableRAM());
        }


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

        internal static bool CheckDBConnection(Type dbType)
        {
            AppLib.WriteLogTraceMessage("- проверка доступа к базе данных...");
            
            // контекст БД
            DbContext dbContext = (DbContext)Activator.CreateInstance(dbType);

            SqlConnection dbConn = (SqlConnection)dbContext.Database.Connection;
            AppLib.WriteLogTraceMessage("-- строка подключения: " + dbConn.ConnectionString);

            // создать такое же подключение, но с TimeOut = 1 сек
            SqlConnectionStringBuilder confBld = new SqlConnectionStringBuilder(dbConn.ConnectionString);
            SqlConnectionStringBuilder testBld = new SqlConnectionStringBuilder()
            {
                DataSource = confBld.DataSource,
                InitialCatalog = confBld.InitialCatalog,
                PersistSecurityInfo = confBld.PersistSecurityInfo,
                IntegratedSecurity = confBld.IntegratedSecurity,
                UserID = confBld.UserID,
                Password = confBld.Password,
                ConnectRetryCount = 1,
                ConnectTimeout = 1
            };
            SqlConnection testConn = new SqlConnection(testBld.ConnectionString);
            bool retVal = false;
            try
            {
                testConn.Open();
                retVal = true;
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage("--- ошибка доступа к БД: " + ex.Message);
            }
            finally
            {
                testConn.Close();
                testConn = null;
            }

            AppLib.WriteLogTraceMessage("- проверка доступа к базе данных - " + ((retVal) ? "READY" : "ERROR!!!"));
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

        public static string GetFullFileName(string relPath, string fileName)
        {
            if (relPath.IsNull() || fileName.IsNull()) return null;

            return getFullPath(relPath) + fileName;
        }
        private static string getFullPath(string relPath)
        {
            string retVal = relPath;

            if (string.IsNullOrEmpty(relPath))  // путь не указан в конфиге - берем путь приложения
                retVal = AppLib.GetAppDirectory();
            else if (retVal.Contains(@"\:") == false)  // относительный путь
            {
                retVal = AppLib.GetAppDirectory() + retVal;
            }
            if (retVal.EndsWith(@"\") == false) retVal += @"\";

            return retVal;
        }

        public static string GetAppVersion()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
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

        // настройки из config-файла
        internal static string GetAppSettingsFromConfigFile()
        {
            return GetAppSettingsFromConfigFile(ConfigurationManager.AppSettings.AllKeys);
        }
        internal static string GetAppSettingsFromConfigFile(string appSettingNames)
        {
            if (appSettingNames == null) return null;
            return GetAppSettingsFromConfigFile(appSettingNames.Split(';'));
        }
        internal static string GetAppSettingsFromConfigFile(string[] appSettingNames)
        {
            StringBuilder sb = new StringBuilder();
            string sValue;
            foreach (string settingName in appSettingNames)
            {
                sValue = ConfigurationManager.AppSettings[settingName];
                if (sValue.IsNull() == false)
                {
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append(settingName + "=" + sValue);
                }
            }
            return sb.ToString();
        }

        // получить глобальное значение приложения из его свойств
        public static object GetAppGlobalValue(string key, object defaultValue = null)
        {
            IDictionary dict = Application.Current.Properties;
            if (dict.Contains(key) == false) return defaultValue;
            else return dict[key];
        }

        // установить глобальное значение приложения (в свойствах приложения)
        public static void SetAppGlobalValue(string key, object value)
        {
            IDictionary dict = Application.Current.Properties;
            if (dict.Contains(key) == false)  // если еще нет значения в словаре
            {
                dict.Add(key, value);   // то добавить
            }
            else    // иначе - изменить существующее
            {
                dict[key] = value;
            }
        }

        internal static string GetShortErrMessage(Exception ex)
        {
            string retVal = ex.Message;
            if (ex.InnerException != null) retVal += " Inner exception: " + ex.InnerException.Message;
            return retVal;
        }

        #endregion

        #region WPF UI interface

        public static double GetRowHeightAbsValue(Grid grid, int iRow, double totalHeight)
        {
            double cntStars = grid.RowDefinitions.Sum(r => r.Height.Value);
            return grid.RowDefinitions[iRow].Height.Value / cntStars * totalHeight;
        }

        public static bool IsAppVerticalLayout
        {
            get
            {
                double appWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
                double appHeight = (double)AppLib.GetAppGlobalValue("screenHeight");
                return (appWidth < appHeight);
            }
        }

        // преобразовать TimeSpan в строку
        public static string GetAppStringTS(TimeSpan tsTimerValue)
        {
            string sFormat = (tsTimerValue.Days > 0d) 
                ? @"d\.hh\:mm\:ss" 
                : ((tsTimerValue.Hours > 0d) ? @"hh\:mm\:ss" : @"mm\:ss");

            string retVal = tsTimerValue.ToString(sFormat);
            // отрицательное время
            if (tsTimerValue.Ticks < 0) retVal = "-" + retVal;

            return retVal;
        }
        // преобразовать строку в TimeSpan
        internal static TimeSpan GetTSFromString(string tsString)
        {
            TimeSpan ts = TimeSpan.Zero;
            TimeSpan.TryParse(tsString, out ts);
            return ts;
        }

        #endregion

    }  // class
}
