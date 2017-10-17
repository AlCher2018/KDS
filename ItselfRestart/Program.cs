using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ItselfRestart
{
    class Program
    {
        public static NLog.Logger _appLogger, _appLogger1;
        const string LoremIpsum = @"Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

        static void Main(string[] args)
        {
            Console.Title = "Itself Restart Console Arrlication";

            // <targets async="true">
            // лог по размеру файла
            // archiveAboveSize = "1000" archiveFileName = "Logs/KDSClient_${shortdate}_{###}.log" archiveNumbering = "Sequence"
            // лог по дате
            // archiveEvery = "Minute" archiveFileName = "Logs/KDSClient ${shortdate}_{##}.txt" archiveNumbering = "Sequence"

            _appLogger = NLog.LogManager.GetLogger("appLogger");
            _appLogger1 = NLog.LogManager.GetLogger("appLogger1");
            DateTime dt1;
            TimeSpan ts;

            dt1 = DateTime.Now;
            for (int i = 0; i < 600; i++)
            {
                _appLogger.Trace(LoremIpsum);
                Thread.Sleep(5000);
            }
            ts = DateTime.Now - dt1;
            Console.WriteLine("цикличный архив - " + ts.ToString());

            //dt1 = DateTime.Now;
            //for (int i = 0; i < 500; i++)
            //{
            //    _appLogger1.Trace(LoremIpsum);
            //    Thread.Sleep(50);
            //}
            //ts = DateTime.Now - dt1;
            //Console.WriteLine("в один файл - " + ts.ToString());



            Console.WriteLine("\nPress any key for restart... (or Q for quit)");
            ConsoleKeyInfo kInfo = Console.ReadKey();

            //if (kInfo.Key != ConsoleKey.Q)
            //{
            //    ProcessStartInfo pInfo = new ProcessStartInfo();
            //    //pInfo.Arguments = string.Format("/C \"{0}\"", System.Reflection.Assembly.GetExecutingAssembly().Location);
            //    //pInfo.FileName = "cmd.exe";
            //    pInfo.FileName = System.Reflection.Assembly.GetExecutingAssembly().Location;
            //    Process.Start(pInfo);

            //    Process curProcess = Process.GetCurrentProcess();
            //    curProcess.Kill();
            //}
        }


    }  // class
}
