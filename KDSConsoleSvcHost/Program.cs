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
            Uri tcp_base_adr = new Uri("net.tcp://localhost:8000/");

            using (ServiceHost host = new ServiceHost(typeof(KDSService.KDSService), tcp_base_adr))
            {
                host.AddServiceEndpoint(typeof(KDSService.IKDSService), new NetTcpBinding(), "KDSService");

                host.Open();

                DisplayHostInfo(host);

                Console.WriteLine("Для завершения нажмите Enter"); Console.ReadKey();
                host.Close();
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
