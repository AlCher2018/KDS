using IntegraLib;
using KDSService.AppModel;
using KDSService.DataSource;
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

namespace KDSService.Lib
{
    // служебный статический класс для работы со словарем свойств и журналом сообщений
    public static class AppEnv
    {
        // логгер
        private static Logger _logger;

        public static TimeSpan TimeOfAutoCloseYesterdayOrders
        {
            get
            {
                var v1 = AppProperties.GetProperty("TimeOfAutoCloseYesterdayOrders");
                return ((v1 == null) ? TimeSpan.Zero : (TimeSpan)v1);
            }
        }

        public static string LoggerInit()
        {
            return initLogger("fileLogger");
        }

        public static bool AppInit(out string errMsg)
        {
            errMsg = null;

            // прочитать настройки из config-файла во внутренний словарь
            putAppConfigParamsToAppProperties();

            // вывести в лог настройки из config-файла
            WriteLogInfoMessage("Настройки из config-файла: " + CfgFileHelper.GetAppSettingsFromConfigFile());

            // доступная память
            WriteLogInfoMessage("Доступная память: {0} Mb", AppEnvironment.getAvailableRAM());

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


        // сложить настройки из config-файла в словарь настроек приложения
        private static void putAppConfigParamsToAppProperties()
        {
            NameValueCollection cfg = ConfigurationManager.AppSettings;
            string value;

            // имя службы MS SQL Service, флаг перезапуска sql-службы
            /*  <!-- наименование службы MS SQL Server, как в services.msc -->
                <add key = "MSSQLServiceName" value = "MSSQLSERVER" />
                <!-- флаг перезапуска sql-службы -->
                <add key = "MSSQLServiceRestartEnable" value = "false" />
            */
            value = cfg["MSSQLServiceRestartEnable"];
            AppProperties.SetProperty("MSSQLServiceRestartEnable", (value == null) ? false : value.ToBool());
            value = cfg["MSSQLServiceName"];
            AppProperties.SetProperty("MSSQLServiceName", (value == null) ? MSSQLService.Controller.ServiceName : value);

            // режим сортировки заказов
            string ordersSortMode = "Desc";
            value = cfg["SortOrdersByCreateDate"];
            if ((value != null) && (value.Equals("Asc", StringComparison.OrdinalIgnoreCase))) ordersSortMode = "Asc";
            AppProperties.SetProperty("SortOrdersByCreateDate", ordersSortMode);

            if ((value = cfg["IsWriteTraceMessages"]) != null)
                AppProperties.SetProperty("IsWriteTraceMessages", value.ToBool());
            if ((value = cfg["TraceOrdersDetails"]) != null)
                AppProperties.SetProperty("TraceOrdersDetails", value.ToBool());
            if ((value = cfg["IsLogClientAction"]) != null)
                AppProperties.SetProperty("IsLogClientAction", value.ToBool());
            if ((value = cfg["TraceQueryToMSSQL"]) != null)
                AppProperties.SetProperty("TraceQueryToMSSQL", value.ToBool());
            

            // время ожидания в состоянии ГОТОВ (время, в течение которого официант должен забрать блюдо), в секундах
            AppProperties.SetProperty("ExpectedTake", cfg["ExpectedTake"].ToInt());

            // читать ли из БД выданные блюда
            AppProperties.SetProperty("IsReadTakenDishes", cfg["IsReadTakenDishes"].ToBool());
            // использовать ли двухэтапный переход в состояние ГОТОВ/ подтверждение состояния ГОТОВ (повар переводит, шеф-повар подтверждает)
            AppProperties.SetProperty("UseReadyConfirmedState", cfg["UseReadyConfirmedState"].ToBool());
            // Время, в СЕКУНДАХ, автоматического перехода из Готово в ПодтвГотово, при включенном ПодтвГотово (UseReadyConfirmedState = true). Если отсутствует или равно 0, то автоматического перехода не будет.
            AppProperties.SetProperty("AutoGotoReadyConfirmPeriod", cfg["AutoGotoReadyConfirmPeriod"].ToInt());

            // учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
            AppProperties.SetProperty("TakeCancelledInAutostartCooking", cfg["TakeCancelledInAutostartCooking"].ToBool());

            value = cfg["TimeOfAutoCloseYesterdayOrders"];
            TimeSpan ts = TimeSpan.Zero;
            if (value != null)
            {
                if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out ts)) ts = TimeSpan.Zero;
            }
            AppProperties.SetProperty("TimeOfAutoCloseYesterdayOrders", ts);

            value = cfg["MidnightShiftShowYesterdayOrders"];
            AppProperties.SetProperty("MidnightShiftShowYesterdayOrders", ((value.IsNull()) ? 0d : value.ToDouble()));

            // неиспользуемые цеха
            value = cfg["UnusedDepartments"];
            if (!value.IsNull())  // не Null и не пусто
            {
                HashSet<int> unUsed = new HashSet<int>();
                if (value.Contains(',')) value = value.Replace(',', ';');
                int[] ids = value.Split(';').Select(s => s.Trim().ToInt()).ToArray();
                foreach (int item in ids)
                    if ((item != 0) && !unUsed.Contains(item)) unUsed.Add(item);

                if (unUsed.Count == 0)
                    AppProperties.SetProperty("UnusedDepartments", null);
                else
                    AppProperties.SetProperty("UnusedDepartments", unUsed);
            }
            else
                AppProperties.SetProperty("UnusedDepartments", null);

            // ВНУТРЕННИЕ КОЛЛЕКЦИИ

            // коллекция для хранения готовящегося количества блюд по цехам (направлениям печати)
            AppProperties.SetProperty("dishesQty", new Dictionary<int, decimal>());

            // коллекции для хранения заблокированных от изменения по таймеру заказов и блюд
            AppProperties.SetProperty("lockedOrders", new Dictionary<int, bool>());
            AppProperties.SetProperty("lockedDishes", new Dictionary<int, bool>());
        }


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
                errMsg = ErrorHelper.GetShortErrMessage(ex);
            }

            return retVal;
        }

        #region App logger

        // отладочные сообщения
        // стандартные действия службы
        public static void WriteLogTraceMessage(string msg)
        {
            if (AppProperties.GetBoolProperty("IsWriteTraceMessages")) _logger.Trace(msg);
        }
        public static void WriteLogTraceMessage(string format, params object[] paramArray)
        {
            if (AppProperties.GetBoolProperty("IsWriteTraceMessages")) _logger.Trace(format, paramArray);
        }

        // подробные действия о чтении заказов из БД
        public static void WriteLogOrderDetails(string msg)
        {
            if (AppProperties.GetBoolProperty("IsWriteTraceMessages") && AppProperties.GetBoolProperty("TraceOrdersDetails"))
                _logger.Trace("svcDtl|" + msg);
        }
        public static void WriteLogOrderDetails(string format, params object[] paramArray)
        {
            if (AppProperties.GetBoolProperty("IsWriteTraceMessages") && AppProperties.GetBoolProperty("TraceOrdersDetails"))
            {
                string msg = string.Format(format, paramArray);
                _logger.Trace("svcDtl|" + msg);
            }
        }

        // сообщения о действиях клиента
        public static void WriteLogClientAction(string machineName, string msg)
        {
            if (AppProperties.GetBoolProperty("IsLogClientAction"))
                _logger.Trace(string.Format("clt {0}|{1}", machineName, msg));
        }
        public static void WriteLogClientAction(string machineName, string format, params object[] paramArray)
        {
            if (AppProperties.GetBoolProperty("IsLogClientAction"))
                _logger.Trace("clt {0}|{1}", machineName, string.Format(format, paramArray));
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

        // информация о запросах к MS SQL Server
        public static void WriteLogMSSQL(string msg)
        {
            if (AppProperties.GetBoolProperty("TraceQueryToMSSQL"))
                _logger.Trace("mssql|" + msg);
        }
        public static void WriteLogMSSQL(string format, params object[] paramArray)
        {
            if (AppProperties.GetBoolProperty("TraceQueryToMSSQL"))
            {
                string msg = string.Format(format, paramArray);
                _logger.Trace("mssql|" + msg);
            }
        }
        #endregion

        #region для конкретного приложения

        public static OrderStatusEnum GetStatusEnumFromNullableInt(int? dbIntValue)
        {
            return (OrderStatusEnum)(dbIntValue ?? 0);
        }

        // узнать, в каком состоянии находятся ВСЕ БЛЮДА заказа
        public static OrderStatusEnum oldGetStatusAllDishes(IEnumerable<OrderDish> dishes)
        {
            if ((dishes == null) || (dishes.Count() == 0)) return OrderStatusEnum.None;

            OrderStatusEnum retVal = OrderStatusEnum.None;

            int iLen = Enum.GetValues(typeof(OrderStatusEnum)).Length;
            int dishCount = dishes.Count();

            int[] statArray = new int[iLen];
            HashSet<int> unUsedDeps = (HashSet<int>)AppProperties.GetProperty("UnusedDepartments");

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

        public static OrderStatusEnum GetStatusAllDishes(IEnumerable<OrderDish> dishes)
        {
            if ((dishes == null) || (dishes.Count() == 0)) return OrderStatusEnum.None;

            int statId = -1, curStat;
            HashSet<int> unUsedDeps = (HashSet<int>)AppProperties.GetProperty("UnusedDepartments");

            foreach (OrderDish dish in dishes)
            {
                if ((unUsedDeps != null) && (unUsedDeps.Contains(dish.DepartmentId)))
                {
                }
                else
                {
                    curStat = dish.DishStatusId ?? -1;
                    if (statId == -1) statId = curStat;
                    else if (statId != dish.DishStatusId) return OrderStatusEnum.None;
                }
            }

            return (OrderStatusEnum)statId;
        }

        #endregion

    }  // class
}
