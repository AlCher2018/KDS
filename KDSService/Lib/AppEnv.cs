using KDSService;
using KDSService.AppModel;
using KDSService.DataSource;
using KDSService.Lib;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KDSConsoleSvcHost
{
    // служебный статический класс для работы со словарем свойств и журналом сообщений
    public static class AppEnv
    {
        // словарь глобальных свойств
        private static AppProperties _props;
        // логгер
        private static Logger _logger;
        public static NLog.Logger _dbCommandLogger = null;

        public static TimeSpan TimeOfAutoCloseYesterdayOrders
        {
            get {
                var v = _props.GetProperty("TimeOfAutoCloseYesterdayOrders");
                return (v == null) ? TimeSpan.Zero : (TimeSpan)v;
            }
        }

        public static string LoggerInit()
        {
            //_dbCommandLogger = NLog.LogManager.GetLogger("dbCommandTracer");
            WriteDBCommandMsg("init dbCommandLogger");
            return initLogger("fileLogger");
        }

        public static bool AppInit(out string errMsg)
        {
            errMsg = null;
            _props = new AppProperties();

            // прочитать настройки из config-файла во внутренний словарь
            putAppConfigParamsToAppProperties();

            // вывести в лог настройки из config-файла
            string cfgValuesString = getConfigString();
            WriteLogInfoMessage("Настройки из config-файла: " + cfgValuesString);

            // доступная память
            WriteLogInfoMessage("Доступная память: {0} Mb", GetAvailableRAM());

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
            if (_logger.IsInfoEnabled == false) return "Ошибка инициализации журнала сообщений " + logName;
            else return null;
        }

        #region App properties

        public static void SetAppProperty(string name, object propObj)
        {
            _props.SetProperty(name, propObj);
        }

        public static object GetAppProperty(string name, object defaultValue = null)
        {
            if (_props.ContainsKey(name))
                return _props.GetProperty(name);
            else
                return defaultValue;
        }

        private static string getConfigString()
        {
            NameValueCollection cfg = ConfigurationManager.AppSettings;
            StringBuilder sb = new StringBuilder();

            putCfgValueToStrBuilder(cfg, sb, "IsWriteTraceMessages");
            putCfgValueToStrBuilder(cfg, sb, "IsLogUserAction");
            putCfgValueToStrBuilder(cfg, sb, "ExpectedTake");
            putCfgValueToStrBuilder(cfg, sb, "IsIngredientsIndependent");
            putCfgValueToStrBuilder(cfg, sb, "UseReadyConfirmedState");
            putCfgValueToStrBuilder(cfg, sb, "TakeCancelledInAutostartCooking");
            putCfgValueToStrBuilder(cfg, sb, "TimeOfAutoCloseYesterdayOrders");
            putCfgValueToStrBuilder(cfg, sb, "UnusedDepartments");

            return sb.ToString();
        }
        private static void putCfgValueToStrBuilder(NameValueCollection cfg, StringBuilder sb, string key)
        {
            string value;
            if ((value = cfg[key]) != null) sb.Append(string.Format("{0}{1}: {2}", (sb.Length == 0 ? "" : "; "), key, value));
        }

        // сложить настройки из config-файла в словарь настроек приложения
        private static void putAppConfigParamsToAppProperties()
        {
            NameValueCollection cfg = ConfigurationManager.AppSettings;
            string value;

            if ((value = cfg["IsWriteTraceMessages"]) != null)
                _props.SetProperty("IsWriteTraceMessages", value.ToBool());
            if ((value = cfg["IsLogUserAction"]) != null)
                _props.SetProperty("IsLogUserAction", value.ToBool());

            // IsIngredientsIndependent - режим ингредиента: зависимый от блюда или независимый
            _props.SetProperty("IsIngredientsIndependent", cfg["IsIngredientsIndependent"].ToBool());

            // время ожидания в состоянии ГОТОВ (время, в течение которого официант должен забрать блюдо), в секундах
            _props.SetProperty("ExpectedTake", cfg["ExpectedTake"].ToInt());

            // использовать ли двухэтапный переход в состояние ГОТОВ/ подтверждение состояния ГОТОВ (повар переводит, шеф-повар подтверждает)
            _props.SetProperty("UseReadyConfirmedState", cfg["UseReadyConfirmedState"].ToBool());

            // учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
            _props.SetProperty("TakeCancelledInAutostartCooking", cfg["TakeCancelledInAutostartCooking"].ToBool());

            value = cfg["TimeOfAutoCloseYesterdayOrders"];
            TimeSpan ts = TimeSpan.Zero;
            if (value != null) TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out ts);
            _props.SetProperty("TimeOfAutoCloseYesterdayOrders", ts);

            // неиспользуемые цеха
            HashSet<int> unUsed = new HashSet<int>();
            value = cfg["UnusedDepartments"];
            if (value != null)
            {
                if (value.Contains(',')) value = value.Replace(',', ';');
                var ids = value.Split(';').Select(s => s.ToInt());
                foreach (int item in ids) if (!unUsed.Contains(item)) unUsed.Add(item);
            }
            _props.SetProperty("UnusedDepartments", unUsed);

            _props.SetProperty("UpdatingOrderId", 0);
            _props.SetProperty("UpdatingDishId", 0);
        }

        public static bool SaveAppSettings(string key, string value, out string errorMsg)
        {
            // Open App.Config of executable
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            try
            {
                errorMsg = null;
                string filename = config.FilePath;

                //Load the config file as an XDocument
                XDocument document = XDocument.Load(filename, LoadOptions.PreserveWhitespace);
                if (document.Root == null)
                {
                    errorMsg = "Document was null for XDocument load.";
                    return false;
                }

                // получить раздел appSettings
                XElement xAppSettings = document.Root.Element("appSettings");
                if (xAppSettings == null)
                {
                    xAppSettings = new XElement("appSettings");
                    document.Root.Add(xAppSettings);
                }

                XElement appSetting = xAppSettings.Elements("add").FirstOrDefault(x => x.Attribute("key").Value == key);
                if (appSetting == null)
                {
                    //Create the new appSetting
                    xAppSettings.Add(new XElement("add", new XAttribute("key", key), new XAttribute("value", value)));
                }
                else
                {
                    //Update the current appSetting
                    appSetting.Attribute("value").Value = value;
                }

                //Save the changes to the config file.
                document.Save(filename, SaveOptions.DisableFormatting);

                // Force a reload of a changed section.
                ConfigurationManager.RefreshSection("appSettings");

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = "There was an exception while trying to update the config file: " + ex.ToString();
                return false;
            }
        }

        public static string GetShortErrMessage(Exception ex)
        {
            string retVal = ex.Message;
            if (ex.InnerException != null) retVal += " Inner message: " + ex.InnerException.Message;
            retVal += ex.Source;
            return retVal;
        }

        // 
        #endregion


        // проверка базы данных
        internal static bool CheckDBConnection(Type dbType, out string errMsg)
        {
            string s;
            errMsg = null;

            // контекст БД
            DbContext dbContext = (DbContext)Activator.CreateInstance(dbType);
            
            SqlConnection dbConn = (SqlConnection)dbContext.Database.Connection;
            s = " - строка подключения: " + dbConn.ConnectionString;
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
            }
            finally
            {
                testConn.Close();
                testConn = null;
            }

            return retVal;
        }

        internal static bool CheckAppDBTable(out string errMsg)
        {
            bool retVal = false; errMsg = null;
            try
            {
                using (KDSEntities db = new KDSEntities())
                {
                    if (db.Department== null)
                        AppEnv.WriteLogInfoMessage(" - таблица Department ОТСУТСТВУЕТ!!");
                    else
                        AppEnv.WriteLogInfoMessage(" - таблица Department содержит {0} записей.", db.Department.Count());

                    if (db.OrderStatus == null)
                        AppEnv.WriteLogInfoMessage(" - таблица OrderStatus ОТСУТСТВУЕТ!!");
                    else
                        AppEnv.WriteLogInfoMessage(" - таблица OrderStatus содержит {0} записей.", db.OrderStatus.Count());
                }
                retVal = true;
            }
            catch (Exception ex)
            {
                errMsg = AppEnv.GetShortErrMessage(ex);
            }

            return retVal;
        }

        #region App logger

        // отладочные сообщения
        public static void WriteLogTraceMessage(string msg)
        {
            if (_props.GetBoolProperty("IsWriteTraceMessages")) _logger.Trace(msg);
        }
        public static void WriteLogTraceMessage(string format, params object[] paramArray)
        {
            if (_props.GetBoolProperty("IsWriteTraceMessages")) _logger.Trace(format, paramArray);
        }
        // сообщения о действиях пользователя
        public static void WriteLogUserAction(string msg)
        {
            if (_props.GetBoolProperty("IsLogUserAction")) _logger.Trace("userAct: " + msg);
        }
        public static void WriteLogUserAction(string format, params object[] paramArray)
        {
            if (_props.GetBoolProperty("IsLogUserAction")) _logger.Trace("userAct: " + format, paramArray);
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

        public static void WriteDBCommandMsg(string msg)
        {
            if (_dbCommandLogger != null) _dbCommandLogger.Info(msg);
        }
        public static void WriteDBCommandMsg(string format, params object[] args)
        {
            if (_dbCommandLogger != null) _dbCommandLogger.Info(format, args);
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

        public static string GetAppVersion()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }


        #endregion

        #region для конкретного приложения
        // узнать, в каком состоянии находятся ВСЕ БЛЮДА заказа
        public static OrderStatusEnum GetStatusAllDishes(IEnumerable<OrderDish> dishes)
        {
            OrderStatusEnum retVal = OrderStatusEnum.None;

            int iLen = Enum.GetValues(typeof(OrderStatusEnum)).Length;
            int dishCount = dishes.Count();

            int[] statArray = new int[iLen];
            HashSet<int> unUsedDeps = (HashSet<int>)AppEnv.GetAppProperty("UnusedDepartments");

            int iStatus;
            foreach (OrderDish dish in dishes)
            {
                if ((unUsedDeps != null) && (unUsedDeps.Contains(dish.DepartmentId)))
                {
                    dishCount--;
                }
                else
                {
                    iStatus = (dish.DishStatusId ?? 0);
                    statArray[iStatus]++;
                }
            }

            for (int i = 0; i < iLen; i++)
            {
                if (statArray[i] == dishCount) { retVal = (OrderStatusEnum)i; break; }
            }

            return retVal;
        }

        #endregion

    }  // class
}
