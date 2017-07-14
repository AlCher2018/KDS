using System;
using System.ServiceModel;


namespace KDSConSvcHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "KDS SERVICE";

            Console.WriteLine("*** Начало работы приложения ***");
            KDSService.KDSServiceClass service = new KDSService.KDSServiceClass();
            
            // 1. Инициализация сервисного класса KDSService
            try
            {
                // config file
                //string cfgFile = @"D:\KDSService.config";
                string cfgFile = AppDomain.CurrentDomain.BaseDirectory + "KDSService.config";
                Console.WriteLine("Инициализация сервисного класса KDSService...");
                service.InitService(cfgFile);
                Console.WriteLine("Инициализация сервисного класса KDSService... Ok");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка инициализации сервисного класса: " + ex.Message);
                exitWithPrompt(1);
            }

            // создать и открыть канал для приема сообщений
            try
            {
                Console.WriteLine("Создание канала для приема сообщений...");
                service.CreateHost();
                DisplayHostInfo(service.ServiceHost);
            }
            catch (Exception ex)
            {
                Console.WriteLine("  ERROR: " + ex.Message);
                exitWithPrompt(2);
            }

            service.StartTimer();

            Console.WriteLine("\nСлужба готова к приему сообщений.\nДля завершения нажмите Enter\n");
            Console.ReadKey();

            if (service != null)
            {
                Console.WriteLine("Закрытие служебного класса KDSService");
                service.Dispose(); service = null;
            }
            Console.WriteLine("*** Завершение работы приложения ***");
        }

        private static void DisplayHostInfo(ServiceHost host)
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

        private static void exitWithPrompt(int exitCode)
        {
            Console.WriteLine("\nAbnormal program termination.\nPress any key for exit.");
            Console.ReadKey();
            Environment.Exit(exitCode);
        }

    }
}
