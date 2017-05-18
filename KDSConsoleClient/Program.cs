using System;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using AppKDS;
using KDSConsoleClient.ServiceReference1;
using System.Timers;
using System.Configuration;


namespace KDSConsoleClient
{
    public class Person
    {
        public int Age { get; set; }
        public string Name { get; set; }

        public int IntField { get { return 2 + 3; } }

        public Person ShallowCopy()
        {
            return (Person)this.MemberwiseClone();
        }
    }


    class Program
    {
        private static Timer _timer;
        private static AppDataProvider dataProv;

        static void Main(string[] args)
        {
            //foreach (var item in ConfigurationManager.AppSettings)
            //{
            //    Console.WriteLine(item.ToString());
            //}
            //string[] aStr = ConfigurationManager.AppSettings.GetValues("appSet1");
            //string s = string.Join("; ", aStr);
            //Console.WriteLine(s);

            string s = ConfigurationManager.AppSettings.Get("appSet1");
            
            // Open App.Config of executable
            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            // Add an Application Setting.
            config.AppSettings.Settings.Remove("appSet1");
            config.AppSettings.Settings.Add("appSet1", "555");
            // Save the configuration file.
            config.Save(ConfigurationSaveMode.Modified);
            // Force a reload of a changed section.
            ConfigurationManager.RefreshSection("appSettings");
            Console.WriteLine(ConfigurationManager.AppSettings.Get("appSet1"));

            Console.Read();
            // Create an instance of Person and assign values to its fields.
            //Person p1 = new Person();
            //p1.Age = 42;
            //p1.Name = "Sam";

            //// Perform a shallow copy of p1 and assign it to p2.
            //Person p2 = p1.ShallowCopy();

            //// Display values of p1, p2
            //Console.WriteLine("Original values of p1 and p2:");
            //Console.WriteLine("   p1 instance values: ");
            //DisplayValues(p1);
            //Console.WriteLine("   p2 instance values:");
            //DisplayValues(p2);

            Console.Title = "CLIENT";
            return;

            KDSServiceClient client = new KDSServiceClient();
            dataProv = new AppDataProvider(client);

            _timer = new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();
            
            Console.Read();

            _timer.Stop();
            client.Close();
        }

        public static void DisplayValues(Person p)
        {
            Console.WriteLine("      Name: {0:s}, Age: {1:d}", p.Name, p.Age);
//            Console.WriteLine("      Value: {0:d}", p.IdInfo.IdNumber);
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
