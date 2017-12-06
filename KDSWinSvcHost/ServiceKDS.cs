using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceProcess;
using IntegraLib;


namespace KDSWinSvcHost
{
    public partial class ServiceKDS : ServiceBase
    {
        // лог для приложений без UI
//        private string _logFile;
        private string _svcInstallLog;

        KDSService.KDSServiceClass service;

        public ServiceKDS()
        {
            InitializeComponent();

            this.AutoLog = true;
            _svcInstallLog = AppEnvironment.GetFullSpecialFileNameInAppDir("InstallLog", null, true);

#if (DEBUG)
            OnStart(null);
#endif
        }

        protected override void OnStart(string[] args)
        {
            putToSvcLog("*** Запуск Windows-службы КДС ***");

            // ЗАЩИТА PSW-файлом
            pswLib.CheckProtectedResult checkProtectedResult;
            if (pswLib.Hardware.IsCurrentAppProtected("KDSService", out checkProtectedResult) == false)
            {
                putToSvcLog(checkProtectedResult.LogMessage);
                putToSvcLog(checkProtectedResult.CustomMessage);
                Environment.Exit(2);
                return;
            }


            // 1. Инициализация сервисного класса KDSService
            try
            {
                // config file
                string cfgFile = CfgFileHelper.GetAppConfigFile("KDSService");
                putToSvcLog("Инициализация сервисного класса KDSService...");
                service = new KDSService.KDSServiceClass();
                service.InitService(cfgFile);
                putToSvcLog("Инициализация сервисного класса KDSService... Ok");
            }
            catch (Exception ex)
            {
                putToSvcLog("Ошибка инициализации сервисного класса: " + ex.Message);
                exitApplication(1);
            }

            // создать хост, параметры канала считываются из app.config
            try
            {
                putToSvcLog("Создание канала для приема сообщений...");
                service.CreateHost();
            }
            catch (Exception ex)
            {
                putToSvcLog("  ERROR: " + ex.Message);
                exitApplication(2);
            }

            service.StartTimer();

            putToSvcLog("Создание канала для приема сообщений... Ok\n\tСлужба готова к приему сообщений.");
        }


        protected override void OnStop()
        {
            if (service != null)
            {
                service.Dispose(); service = null;
            }
            putToSvcLog("**** Остановка Windows-службы КДС ****");
        }


        private void exitApplication(int exitCode)
        {
            putToSvcLog("Abnormal program termination.");
            Environment.Exit(exitCode);
        }

        private void putToSvcLog(string msg)
        {
            // для консольных приложений
            //Console.WriteLine(msg);

            // для приложений без UI
            if (_svcInstallLog.IsNull()) return;

            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(_svcInstallLog, true);
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
