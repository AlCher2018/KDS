using KDSService.AppModel;
using KDSService.DataSourse;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace KDSService
{
    /// <summary>
    /// 1. Периодический опрос заказов из БД
    /// </summary>

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class KDSServiceClass : IDisposable, IKDSService
    {
        private const double _ObserveTimerInterval = 500;

        // словарь глобальных свойств
        private AppProperties _props;

        // логгер
        private Logger _logger;
        // заказы на стороне службы (с таймерами)
        private OrdersSvcModel _ordersModel;  
        // таймер наблюдения за заказами в БД
        private Timer _observeTimer;


        public KDSServiceClass()
        {
            _logger = LogManager.GetLogger("fileLogger");
            
            Console.WriteLine("конструктор KDSService");

            _props = new AppProperties();
            getAppPropertiesFromConfigFile();

            writeLogInfoMessage("*******  CREATE INSTANCE KDSService  ********");

            // вывести в лог настройки из config-файла
            string cfgValuesString = getConfigString();
            writeLogInfoMessage("Настройки из config-файла: " + cfgValuesString);

            _observeTimer = new Timer(_ObserveTimerInterval);
            _observeTimer.Elapsed += _observeTimer_Elapsed;
            _observeTimer.Start();

            _ordersModel = new OrdersSvcModel();
            // DEBUG
            _observeTimer_Elapsed(null, null);
        }

        public void Dispose()
        {
            writeLogInfoMessage("*******  PURGE KDSService  ********");

            // таймер остановить, отписаться от события и уничтожить
            if (_observeTimer != null)
            {
                if (_observeTimer.Enabled == true) _observeTimer.Stop();
                _observeTimer.Elapsed -= _observeTimer_Elapsed;
                _observeTimer.Dispose();
            }
        }


        private string getConfigString()
        {
            NameValueCollection cfg = ConfigurationManager.AppSettings;
            StringBuilder sb = new StringBuilder();

            putCfgValueToStrBuilder(cfg, sb, "IsWriteTraceMessages");
            putCfgValueToStrBuilder(cfg, sb, "IsLogUserAction");

            return sb.ToString();
        }
        private void putCfgValueToStrBuilder(NameValueCollection cfg, StringBuilder sb, string key)
        {
            string value;
            if ((value = cfg[key]) != null) sb.Append(string.Format("{0}{1}: {2}", (sb.Length == 0 ? "" : "; "), key, value));
        }

        private void getAppPropertiesFromConfigFile()
        {
            NameValueCollection cfg = ConfigurationManager.AppSettings;
            string value;

            if ((value = cfg["IsWriteTraceMessages"]) != null)
                _props.SetProperty("IsWriteTraceMessages", value.ToBool());
            if ((value = cfg["IsLogUserAction"]) != null)
                _props.SetProperty("IsLogUserAction", value.ToBool());
        }

        // *** начало работы и периодический просмотр заказов
        private void _observeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // DEBUG
            _observeTimer.Stop();

            _ordersModel.UpdateOrders();
        }


        // ****  SERVICE CONTRACT  *****
        #region service contract
        public void ChangeStatus(OrderCommand command)
        {
            throw new NotImplementedException();
        }

        public AppModel.DepartmentGroup[] GetDepartmentGroups()
        {
            return ServiceDics.DepGroups.GetClientInstance();
        }

        public AppModel.Department[] GetDepartments()
        {
            return ServiceDics.Departments.GetClientInstance();
        }

        public OrdersCltModel GetOrdersCltModel()
        {
            return _ordersModel.GetClientInstance();
        }

        #endregion

        #region App logger

        private void writeLogTraceMessage(string msg)
        {
            if (_props.GetBoolProperty("IsWriteTraceMessages")) _logger.Trace(" " + msg);
        }

        private void writeLogInfoMessage(string msg)
        {
            _logger.Info(msg);
        }

        private void writeLogErrorMessage(string msg)
        {
            _logger.Error(msg);
        }

        private void writeAppAction(AppActionEnum action, string value = null)
        {
            if (_props.GetBoolProperty("IsLogUserAction"))
            {
                string msg = action.ToString();
                if (value != null) msg += ". " + value;
                writeLogTraceMessage(msg);
            }
        }

        #endregion

    }  // class
}
