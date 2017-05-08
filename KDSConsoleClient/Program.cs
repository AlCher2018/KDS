using System;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using AppKDS;
using KDSConsoleClient.ServiceReference1;

namespace KDSConsoleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "CLIENT";

            KDSServiceClient client = new KDSServiceClient();

            //testDepGroups(client);

            // получить словари
            //   групп отделов
            Dictionary<int, DepartmentGroupModel> depGroups = client.GetDepartmentGroups();
            //   отделов
            Dictionary<int, DepartmentModel> deps = client.GetDepartments();

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
                        OrderModel om = orders.FirstOrDefault(o => o.Id==id);
                        if (om != null)
                            Console.WriteLine("id: {0}; Number {1}; hallName {2}; dishes count: {3}", om.Id, om.Number, om.HallName, om.Dishes.Count);
                    }
                }
            }  // while

            Console.Read();
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
    }  // class

}
