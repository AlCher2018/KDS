using KDSService;
using KDSService.AppModel;
using KDSService.Lib;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSConsoleSvcHost
{
    public static class AppEnv
    {
        // словарь глобальных свойств
        private static AppProperties _props;
        // логгер
        private static Logger _logger;

        // ctor
        public static bool AppInit(out string errMsg)
        {
            errMsg = null;

            errMsg = initLogger("fileLogger");
            if (errMsg.IsNull() == false) return false;

            _props = new AppProperties();

            WriteLogInfoMessage("**** Инициализация приложения ****");

            // вывести в лог настройки из config-файла
            getAppPropertiesFromConfigFile();
            string cfgValuesString = getConfigString();
            WriteLogInfoMessage("Настройки из config-файла: " + cfgValuesString);

            // доступная память
            WriteLogInfoMessage("Доступная память: {0} Mb", GetAvailableRAM());

            // проверить доступность БД
            if (CheckDBConnection(typeof(KDSEntities), out errMsg) == false) return false;

            // сохранить в служебном классе словари из БД
            //    группы отделов
            errMsg = ServiceDics.DepGroups.UpdateFromDB();
            if (errMsg != null)
            {
                WriteLogErrorMessage("Ошибка чтения из БД: " + errMsg);
                return false;
            }
            //    отделы
            errMsg = ServiceDics.Departments.UpdateFromDB();
            if (errMsg != null)
            {
                WriteLogErrorMessage("Ошибка чтения из БД: " + errMsg);
                return false;
            }

            return true;
        }

        private static string initLogger(string logName)
        {
            try
            {
                _logger = LogManager.GetLogger(logName);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            if (_logger.IsInfoEnabled == false) return "Ошибка инициализации журнала трассировки " + logName;
            else return null;
        }

        private static string getConfigString()
        {
            NameValueCollection cfg = ConfigurationManager.AppSettings;
            StringBuilder sb = new StringBuilder();

            putCfgValueToStrBuilder(cfg, sb, "IsWriteTraceMessages");
            putCfgValueToStrBuilder(cfg, sb, "IsLogUserAction");

            return sb.ToString();
        }
        private static void putCfgValueToStrBuilder(NameValueCollection cfg, StringBuilder sb, string key)
        {
            string value;
            if ((value = cfg[key]) != null) sb.Append(string.Format("{0}{1}: {2}", (sb.Length == 0 ? "" : "; "), key, value));
        }

        private static void getAppPropertiesFromConfigFile()
        {
            NameValueCollection cfg = ConfigurationManager.AppSettings;
            string value;

            if ((value = cfg["IsWriteTraceMessages"]) != null)
                _props.SetProperty("IsWriteTraceMessages", value.ToBool());
            if ((value = cfg["IsLogUserAction"]) != null)
                _props.SetProperty("IsLogUserAction", value.ToBool());
        }



        // проверка базы данных
        internal static bool CheckDBConnection(Type dbType, out string errMsg)
        {
            string s;
            errMsg = null;
            WriteLogInfoMessage("Проверка доступа к базе данных...");

            // контекст БД
            DbContext dbContext = (DbContext)Activator.CreateInstance(dbType);

            SqlConnection dbConn = (SqlConnection)dbContext.Database.Connection;
            s = " - строка подключения: " + dbConn.ConnectionString;
            Console.WriteLine("\n**** SQL Connection String ****\n{0}\n****", dbConn.ConnectionString);
            WriteLogInfoMessage(s);

            // создать такое же подключение, но с TimeOut = 2 сек
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
                ConnectTimeout = 2
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
                errMsg = ex.Message;
                WriteLogErrorMessage(" - ошибка доступа к БД: " + ex.Message);
            }
            finally
            {
                testConn.Close();
                testConn = null;
            }

            WriteLogInfoMessage("Проверка доступа к базе данных... " + ((retVal) ? "READY" :"ERROR!!!"));
            return retVal;
        }

        #region App logger

        public static void WriteLogTraceMessage(string msg)
        {
            if (_props.GetBoolProperty("IsWriteTraceMessages")) _logger.Trace(msg);
        }
        public static void WriteLogTraceMessage(string format, params object[] paramArray)
        {
            if (_props.GetBoolProperty("IsWriteTraceMessages")) _logger.Trace(format, paramArray);
        }

        public static void WriteLogInfoMessage(string msg)
        {
            _logger.Info(msg);
        }
        public static void WriteLogInfoMessage(string format, params object[] paramArray)
        {
            _logger.Info(format, paramArray);
        }

        public static void WriteLogErrorMessage(string msg)
        {
            _logger.Error(msg);
        }
        public static void WriteLogErrorMessage(string format, params object[] paramArray)
        {
            _logger.Error(format, paramArray);
        }

        public static void WriteAppAction(AppActionEnum action, string value = null)
        {
            if (_props.GetBoolProperty("IsLogUserAction"))
            {
                string msg = action.ToString();
                if (value != null) msg += ". " + value;
                WriteLogTraceMessage(msg);
            }
        }

        #endregion

        #region system info
        // in Mb
        public static int GetAvailableRAM()
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
            return retVal;  // in Mb
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

        #endregion

    }  // class
}
