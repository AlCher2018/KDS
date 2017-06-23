using System;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceProcess;



namespace KDSWinSvcHost
{
    public partial class Service1 : ServiceBase
    {
        private ServiceHost _kdsHost;


        public Service1()
        {
            InitializeComponent();

            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            // создать сервисный класс, который будет обслуживать канал
            putToSvcLog("Создание сервисного класса KDSService...");
            putToSvcLog("** System environment ** " + getSystemEnvironment());

            // папка приложения ??? (dll-file)
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            putToSvcLog("Assembly.GetExecutingAssembly().Location = " + assembly.Location);
            putToSvcLog("Assembly.GetExecutingAssembly().ManifestModule.Name = " + assembly.ManifestModule.Name);
            putToSvcLog("AppDomain.CurrentDomain.BaseDirectory = " + AppDomain.CurrentDomain.BaseDirectory);

            KDSService.KDSServiceClass service = null;
            try
            {
                service = new KDSService.KDSServiceClass();
            }
            catch (Exception ex)
            {
                putToSvcLog("Ошибка создания сервисного класса: " + ex.ToString());
                throw;
            }
            putToSvcLog("Создание сервисного класса KDSService... Ok");

            // создать хост, параметры канала считываются из app.config
            putToSvcLog("Создание канала для приема сообщений...");
            try
            {
                if (_kdsHost != null) { _kdsHost.Close(); _kdsHost = null; }
                _kdsHost = new ServiceHost(typeof(KDSService.KDSServiceClass));
                //_kdsHost.OpenTimeout = TimeSpan.FromMinutes(10);  // default 1 min
                //_kdsHost.CloseTimeout = TimeSpan.FromMinutes(1);  // default 10 sec

                _kdsHost.Open();
                writeHostInfoToLog(_kdsHost);
            }
            catch (Exception ex)
            {
                putToSvcLog(string.Format("Ошибка открытия канала сообщений: {0}", ex.ToString()));
                throw;
            }
            putToSvcLog("Создание канала для приема сообщений... Ok\nСлужба готова к приему сообщений.");
        }

        private string getSystemEnvironment()
        {
            string retVal = string.Format("CurrentDirectory: {0}, MachineName: {1}, OSVersion: {2}, UserDomainName: {3}, UserName: {4}", Environment.CurrentDirectory, Environment.MachineName, Environment.OSVersion, Environment.UserDomainName, Environment.UserName);

            return retVal;
        }

        protected override void OnStop()
        {
            if (_kdsHost != null)
            {
                _kdsHost.Close(); _kdsHost = null;
            }
        }


        private void writeHostInfoToLog(ServiceHost host)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (System.ServiceModel.Description.ServiceEndpoint se in host.Description.Endpoints)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine(string.Format("\tHost Info: address {0}; binding: {1}; contract: {2}", se.Address, se.Binding.Name, se.Contract.Name));
            }
            if (sb.Length > 0) this.putToSvcLog("Service endpoints:\n" + sb.ToString());
        }


        private void putToSvcLog(string msg)
        {
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(@"d:\svclog.txt", true);
                msg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ": " + msg;
                sw.WriteLine(msg);
            }
            catch (Exception ex)
            {
                StreamWriter error = new StreamWriter("errors.txt", true);
                error.WriteLine(ex.Message);
                error.Close();
            }
            finally
            {
                sw.Close();
            }
        }


    }  // class
}
