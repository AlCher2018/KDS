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
            ChannelFactory<KDSService.IKDSService> factory = new ChannelFactory<KDSService.IKDSService>(new NetTcpBinding(), new EndpointAddress("net.tcp://localhost:8000/KDSService"));

            KDSService.IKDSService channel = factory.CreateChannel();

            while (true)
            {
                List<KDSService.AppModel.Order> orders = channel.GetOrders();
                Console.WriteLine("Заказов - " + orders.Count);

                string sBuf = Console.ReadLine();
                if (string.IsNullOrEmpty(sBuf)) break;
            }

            factory.Close();
        }
    }
}
