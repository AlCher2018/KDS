using IntegraLib;
using KDSService.AppModel;
using KDSService.DataSource;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
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
    public static class AppLib
    {

        public static TimeSpan TimeOfAutoCloseYesterdayOrders
        {
            get
            {
                var v1 = AppProperties.GetProperty("TimeOfAutoCloseYesterdayOrders");
                return ((v1 == null) ? TimeSpan.Zero : (TimeSpan)v1);
            }
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


        // сложить настройки из config-файла в словарь настроек приложения
        private static void putAppConfigParamsToAppProperties()
        {
            NameValueCollection cfg = ConfigurationManager.AppSettings;
            string value;

            // наименование службы MS SQL Server, как в services.msc
            setGlobalValueFromCfg("MSSQLServiceName", MSSQLService.Controller.ServiceName);
            // флаг перезапуска sql-службы, по умолчанию - false
            setGlobalValueFromCfg("MSSQLServiceRestartEnable", false);
            // уровень совместимости БД (120 - это MS SQL Server 2014)
            setGlobalValueFromCfg("MSSQLServerCompatibleLevel", 0);
            // таймаут выполнения команд в MS SQL, в СЕКУНДАХ
            setGlobalValueFromCfg("MSSQLCommandTimeout", 2);

            // уведомление Одерманов о готовом заказе
            setGlobalValueFromCfg("NoticeOrdermanFeature", false);
            setGlobalValueFromCfg<string>("NoticeOrdermanFolder", null);
            // проверка настроек ф-и уведомления
            if (AppProperties.GetBoolProperty("NoticeOrdermanFeature"))
            {
                string folder = (string)AppProperties.GetProperty("NoticeOrdermanFolder");
                if (folder == null)
                    AppLib.WriteLogInfoMessage("Функция NoticeOrdermanFeature включена, но не указана папка для сохранения файлов-уведомлений NoticeOrdermanFolder");
                else
                {
                    if (!folder.EndsWith(@"\")) AppProperties.SetProperty("NoticeOrdermanFolder", folder + @"\");
                    if (!System.IO.Directory.Exists(folder))
                        AppLib.WriteLogInfoMessage("Функция NoticeOrdermanFeature включена, указана папка для сохранения файлов-уведомлений NoticeOrdermanFolder, но в системе эта папка не существует!");
                }
            }

            // режим сортировки заказов
            string ordersSortMode = "Desc";
            value = cfg["SortOrdersMode"];
            if ((value != null) && (value.Equals("Asc", StringComparison.OrdinalIgnoreCase))) ordersSortMode = "Asc";
            AppProperties.SetProperty("SortOrdersMode", ordersSortMode);

            // время ожидания в состоянии ГОТОВ (время, в течение которого официант должен забрать блюдо), в секундах
            setGlobalValueFromCfg("ExpectedTake", 0);

            // читать ли из БД выданные блюда
            setGlobalValueFromCfg("IsReadTakenDishes", false);
            // использовать ли двухэтапный переход в состояние ГОТОВ/ подтверждение состояния ГОТОВ (повар переводит, шеф-повар подтверждает)
            setGlobalValueFromCfg("UseReadyConfirmedState", false);
            // Время, в СЕКУНДАХ, автоматического перехода из Готово в ПодтвГотово, при включенном ПодтвГотово (UseReadyConfirmedState = true). Если отсутствует или равно 0, то автоматического перехода не будет.
            setGlobalValueFromCfg("AutoGotoReadyConfirmPeriod", 0);

            // учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
            setGlobalValueFromCfg("TakeCancelledInAutostartCooking", false);

            value = cfg["TimeOfAutoCloseYesterdayOrders"];
            TimeSpan ts = TimeSpan.Zero;
            if (value != null)
            {
                if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out ts)) ts = TimeSpan.Zero;
            }
            AppProperties.SetProperty("TimeOfAutoCloseYesterdayOrders", ts);

            setGlobalValueFromCfg("MidnightShiftShowYesterdayOrders", 0d);

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

            // Максимальное количество архивных файлов журнала. По умолчанию, равно 0 (нет ограничения).
            value = cfg["MaxLogFiles"];
            AppProperties.SetProperty("MaxLogFiles", ((value==null) ? 0 : value.ToInt()));

            // отладочные сообщения
            setGlobalValueFromCfg("IsWriteTraceMessages", false);
            setGlobalValueFromCfg("TraceOrdersDetails", false);
            setGlobalValueFromCfg("IsLogClientAction", false);
            setGlobalValueFromCfg("TraceQueryToMSSQL", false);


            // ВНУТРЕННИЕ КОЛЛЕКЦИИ

            // коллекция для хранения готовящегося количества блюд по цехам (направлениям печати)
            AppProperties.SetProperty("dishesQty", new Dictionary<int, decimal>());
        }

        #region App logger
        // логгер
        private static Logger _logger;

        public static string LoggerInit()
        {
            return initLogger("fileLogger");
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

        public static bool LogEnable { get { return _logger != null; } }

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

        #endregion

        private static void setGlobalValueFromCfg<T>(string cfgElementName, T defaultValue, string globVarName = null)
        {
            string sCfgValue = CfgFileHelper.GetAppSetting(cfgElementName);

            AppProperties.SetProperty(((globVarName == null) ? cfgElementName : globVarName),
               (string.IsNullOrEmpty(sCfgValue) ? defaultValue : getValueFromString(ref sCfgValue, typeof(T))));
        }
        private static object getValueFromString(ref string strValue, Type valueType)
        {
            if (valueType == typeof(bool))          return strValue.ToBool();
            else if (valueType == typeof(int))      return strValue.ToInt();
            else if (valueType == typeof(double))   return strValue.ToDouble();
            else                                    return strValue;
        }

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
                    iStatus = dish.DishStatusId;
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
                    curStat = dish.DishStatusId;
                    if (statId == -1) statId = curStat;
                    else if (statId != dish.DishStatusId) return OrderStatusEnum.None;
                }
            }

            return (OrderStatusEnum)statId;
        }

        #endregion

    }  // class
}
