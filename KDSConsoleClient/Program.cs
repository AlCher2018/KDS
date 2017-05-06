using KDSConsoleClient.ServiceReference1;
using KDSService.AppModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace KDSConsoleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            //ChannelFactory<KDSService.IKDSService> factory = new ChannelFactory<KDSService.IKDSService>(new NetTcpBinding(), new EndpointAddress("net.tcp://localhost:8000/KDSService"));

            //KDSService.IKDSService channel = factory.CreateChannel();

            //while (true)
            //{
            //    OrderCltModel[] orders = channel.GetArrayOrdersForClient();
            //    Console.WriteLine("Заказов - " + orders.Length);

            //    string sBuf = Console.ReadLine();
            //    if (string.IsNullOrEmpty(sBuf)) break;
            //}
            //factory.Close();

            using (KDSServiceClient client = new KDSServiceClient())
            {
                int i = client.GetOrdersCount();
                Console.WriteLine(i.ToString());

                OrderCltModel[] orders = client.GetArrayOrdersForClient();
                Console.WriteLine("Заказов - " + orders.Length);

            }
            Console.Read();
        }
    }
}
