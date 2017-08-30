using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ItselfRestart
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Itself Restart Console Arrlication";

            Console.WriteLine("\nPress any key for restart... (or Q for quit)");
            ConsoleKeyInfo kInfo = Console.ReadKey();

            if (kInfo.Key != ConsoleKey.Q)
            {
                ProcessStartInfo pInfo = new ProcessStartInfo();
                //pInfo.Arguments = string.Format("/C \"{0}\"", System.Reflection.Assembly.GetExecutingAssembly().Location);
                //pInfo.FileName = "cmd.exe";
                pInfo.FileName = System.Reflection.Assembly.GetExecutingAssembly().Location;
                Process.Start(pInfo);

                Process curProcess = Process.GetCurrentProcess();
                curProcess.Kill();
            }
        }


    }  // class
}
