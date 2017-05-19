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
using System.Data.SqlClient;


namespace KDSWPFClient.Lib
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
        public static void WriteLogTraceMessage(string msg)
        {
            if (AppLib.GetAppSetting("IsWriteTraceMessages").ToBool() && AppLogger.IsTraceEnabled)
                AppLogger.Trace(msg??"null");
        }

        public static void WriteLogTraceMessage(string format, params object[] args)
        {
            if (AppLib.GetAppSetting("IsWriteTraceMessages").ToBool() && AppLogger.IsTraceEnabled)
                AppLogger.Trace(format, args);
        }

        public static void WriteLogInfoMessage(string msg)
        {
            if (AppLogger.IsInfoEnabled) AppLogger.Info(msg??"null");
        }
        public static void WriteLogInfoMessage(string format, params object[] args)
        {
            if (AppLogger.IsInfoEnabled) AppLogger.Info(format, args);
        }

        public static void WriteLogErrorMessage(string msg)
        {
            if (AppLogger.IsErrorEnabled) AppLogger.Error(msg??"null");
        }
        public static void WriteLogErrorMessage(string format, params object[] args)
        {
            if (AppLogger.IsErrorEnabled) AppLogger.Error(format, args);
        }
        #endregion

        #region system info
        internal static string GetEnvironmentString()
        {
            return string.Format("Environment: machine={0}, user={1}, current directory={2}, OS version={3}, isOS64bit={4}, processor count={5}, free RAM={6} Mb",
                Environment.MachineName, Environment.UserName, Environment.CurrentDirectory, Environment.OSVersion, Environment.Is64BitOperatingSystem, Environment.ProcessorCount, getAvailableRAM());
        }


        // in Mb
        private static int getAvailableRAM()
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


        //public static bool CheckDBConnection(Type dbType)
        //{
        //    string s;
        //    WriteLogInfoMessage("Проверка доступа к базе данных...");

        //    // контекст БД
        //    DbContext dbContext = (DbContext)Activator.CreateInstance(dbType);

        //    SqlConnection dbConn = (SqlConnection)dbContext.Database.Connection;
        //    s = " - строка подключения: " + dbConn.ConnectionString;
        //    Console.WriteLine("\n**** SQL Connection String ****\n{0}\n****", dbConn.ConnectionString);
        //    WriteLogInfoMessage(s);

        //    // создать такое же подключение, но с TimeOut = 1 сек
        //    SqlConnectionStringBuilder confBld = new SqlConnectionStringBuilder(dbConn.ConnectionString);
        //    SqlConnectionStringBuilder testBld = new SqlConnectionStringBuilder()
        //    {
        //        DataSource = confBld.DataSource,
        //        InitialCatalog = confBld.InitialCatalog,
        //        PersistSecurityInfo = confBld.PersistSecurityInfo,
        //        IntegratedSecurity = confBld.IntegratedSecurity,
        //        UserID = confBld.UserID,
        //        Password = confBld.Password,
        //        ConnectRetryCount = 1,
        //        ConnectTimeout = 1
        //    };
        //    SqlConnection testConn = new SqlConnection(testBld.ConnectionString);
        //    bool retVal = false;
        //    try
        //    {
        //        testConn.Open();
        //        retVal = true;
        //    }
        //    catch (Exception ex)
        //    {
        //        AppLib.WriteLogErrorMessage("--- ошибка доступа к БД: " + ex.Message);
        //    }
        //    finally
        //    {
        //        testConn.Close();
        //        testConn = null;
        //    }

        //    WriteLogInfoMessage("Проверка доступа к базе данных... " + ((retVal) ? "READY" : "ERROR!!!"));
        //    return retVal;
        //}

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
        #endregion

        #region app settings
        // получить настройки приложения из config-файла
        public static string GetAppSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
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

        // запись значения в config-файл
        // ConfigurationManager НЕ СОХРАНЯЕТ КОММЕНТАРИИ!!!!
        public static void SaveValueToConfig(string key, string value)
        {
            // Open App.Config of executable
            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            // Add an Application Setting.
            config.AppSettings.Settings.Remove(key);
            config.AppSettings.Settings.Add(key, value);
            // Save the configuration file.
            config.Save(ConfigurationSaveMode.Modified);
            // Force a reload of a changed section.
            ConfigurationManager.RefreshSection("appSettings");
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


        #endregion

    }
}
