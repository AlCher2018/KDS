using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KDSService;


namespace KDSServiceConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            KDSService.KDSService svc = new KDSService.KDSService();

            Console.WriteLine("press Enter for exit"); Console.ReadKey();
        }
    }
}
