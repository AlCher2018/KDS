using System;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
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

            List<OrderModel> orders = client.GetOrders();
            Console.WriteLine("Заказов " + orders.Count);

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
