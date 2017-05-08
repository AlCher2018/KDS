using KDSService.AppModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace KDSConsoleSvcHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "SERVICE";

            ServiceHost host = new ServiceHost(typeof(KDSService.KDSServiceClass));
            host.Open();

            DisplayHostInfo(host);
            Console.WriteLine("Служба готова к приему сообщений.");
            Console.WriteLine("Для завершения нажмите Enter");

            //KDSService.KDSServiceClass service = new KDSService.KDSServiceClass();
            //Dictionary<int, DepartmentModel> dg = service.GetDepartments();

            Console.ReadKey();
            host.Close(); host = null;
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
