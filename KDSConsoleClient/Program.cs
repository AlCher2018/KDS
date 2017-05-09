using System;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using AppKDS;
using KDSConsoleClient.ServiceReference1;
using System.Timers;

namespace KDSConsoleClient
{
    class Program
    {
        private static Timer _timer;
        private static AppDataProvider dataProv;

        static void Main(string[] args)
        {
            Console.Title = "CLIENT";

            KDSServiceClient client = new KDSServiceClient();
            dataProv = new AppDataProvider(client);

            _timer = new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();
            
            Console.Read();

            _timer.Stop();
            client.Close();
        }

        private static void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Stop();

            List<OrderModel> orders = dataProv.GetOrders();

            OrderModel om = orders[0];
            Console.WriteLine("id: {0}; Number {1}; hallName {2}; dishes count: {3}", om.Id, om.Number, om.HallName, om.Dishes.Count);
            _timer.Start();
        }

        private static void testDepGroups(KDSServiceClient client)
        {
            Console.WriteLine("ГРУППЫ ОТДЕЛОВ");
            while (true)
            {
                Dictionary<int, DepartmentGroupModel> depGroups = client.GetDepartmentGroups();
                Console.WriteLine("введите Ид группы: ");
                string resp = Console.ReadLine();
                if (resp.IsNull()) break;

                if (resp == "0")
                    Console.WriteLine("групп отделов - {0}", depGroups.Count);
                else
                {
                    int id = Convert.ToInt32(resp);
                    if (depGroups.ContainsKey(id))
                    {
                        DepartmentGroupModel dg = depGroups[id];
                        Console.WriteLine("key {0}, value {{id: {0}, name: {1}}}", dg.Id, dg.Name);
                    }
                    else
                    {
                        Console.WriteLine("нет такого Ид");
                    }
                }
            }
        }

        private static void testOrders(KDSServiceClient client)
        {
            while (true)
            {
                Console.WriteLine("введите Ид заказа: ");
                string resp = Console.ReadLine();
                if (resp.IsNull()) break;
                else
                {
                    List<OrderModel> orders = client.GetOrders();
                    if (resp == "0")
                        Console.WriteLine("Заказов " + orders.Count);
                    else if (resp == "-1")
                    {
                        // all records
                        foreach (OrderModel om in orders)
                        {
                            Console.WriteLine("id: {0}; Number {1}; hallName {2}; dishes count: {3}", om.Id, om.Number, om.HallName, om.Dishes.Count);
                        }
                    }
                    else
                    {
                        int id = Convert.ToInt32(resp);
                        OrderModel om = orders.FirstOrDefault(o => o.Id == id);
                        if (om != null)
                            Console.WriteLine("id: {0}; Number {1}; hallName {2}; dishes count: {3}", om.Id, om.Number, om.HallName, om.Dishes.Count);
                    }
                }
            }  // while
        }


    }  // class

}
