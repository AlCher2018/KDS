using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using KDSService.AppModel;
using KDSService.Lib;
using KDSConsoleSvcHost.AppModel;


namespace KDSConsoleSvcHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "KDS SERVICE";

            // инициализация приложения
            Console.WriteLine("Инициализация приложения...");
            string errMsg = null;
            if (AppEnv.AppInit(out errMsg) == false)
            {
                if (errMsg != null) Console.WriteLine("Ошибка инициализации приложения: " + errMsg);
                exitWithPrompt(1);
            }

            // открыть канал для приема сообщений
            KDSService.KDSServiceClass service = null;
            ServiceHost host = null;
            try
            {
                // параметры канала считываются из app.config
                // создает сервис при первом обращении
                //host = new ServiceHost(typeof(KDSService.KDSServiceClass));

                service = new KDSService.KDSServiceClass();

                host = new ServiceHost(service);
                //host.OpenTimeout = TimeSpan.FromMinutes(10);  // default 1 min
                //host.CloseTimeout = TimeSpan.FromMinutes(1);  // default 10 sec

                host.Open();
                DisplayHostInfo(host);
                WriteHostInfoToLog(host);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка открытия канала сообщений: " + ex.Message);
                AppEnv.WriteLogErrorMessage("Ошибка открытия канала сообщений: {0}{1}\tTrace: {2}",ex.Message, Environment.NewLine, ex.StackTrace);
                exitWithPrompt(2);
            }

            AppEnv.WriteLogInfoMessage("Служба готова к приему сообщений.");
            Console.WriteLine("\nСлужба готова к приему сообщений.\nДля завершения нажмите Enter\n");

            Console.ReadKey();

            if (service != null) { service.Dispose(); service = null; }
            if (host != null) { host.Close(); host = null; }

            AppEnv.WriteLogInfoMessage("**** Завершение работы приложения ****");
        }

        private static void exitWithPrompt(int exitCode)
        {
            Console.WriteLine("\nAbnormal program termination.\nPress any key for exit.");
            Console.ReadKey();
            Environment.Exit(exitCode);
        }

        private static void WriteHostInfoToLog(ServiceHost host)
        {
            foreach (System.ServiceModel.Description.ServiceEndpoint se in host.Description.Endpoints)
            {
                AppEnv.WriteLogInfoMessage("Host Info: address {0}; binding: {1}; contract: {2}", se.Address, se.Binding.Name, se.Contract.Name);
            }
        }

        static void DisplayHostInfo(ServiceHost host)
        {
            Console.WriteLine();
            Console.WriteLine("***** Host Info *****");
            foreach (System.ServiceModel.Description.ServiceEndpoint se in host.Description.Endpoints)
            {
                Console.WriteLine("Address: {0}", se.Address);
                Console.WriteLine("Binding: {0}", se.Binding.Name);
                Console.WriteLine("Contract: {0}", se.Contract.Name);
                Console.WriteLine();
            }
            Console.WriteLine("**********************");
        }


    }  // class
}
