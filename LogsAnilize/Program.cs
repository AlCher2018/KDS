using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LogsAnilize
{
    class Program
    {
        private static string gsFind = "- result (depId/count):";
        private static string[] gsSeparator= new string[] { "; " };
        private static int pizzaCookCount=-1;

        private static string gStr1="", gStr2="";

        static void Main(string[] args)
        {
            string path = @"d:\Integra-Its\KDS\_проблемы\2018-03-15 Молино-12. Заявка 10774 в SD\логи службы";
            if (path.EndsWith("\\") == false) path += "\\";

            FileInfo fInfo;
            //StreamReader reader;
            string[] reader;
            List<string> outLines = new List<string>();
            Encoding fileEncoding = Encoding.GetEncoding(1251);

            string[] files = Directory.GetFiles(path);
            int lastCount = -1;
            foreach (string filePath in files)
            {
                fInfo = new FileInfo(filePath);
                //reader = File.ReadAllLines(filePath, Encoding.Default);
                reader = File.ReadAllLines(filePath, fileEncoding);
                gStr1 = $"[{fInfo.Name}]: {reader.Length.ToString()}";
                Console.WriteLine(gStr1); outLines.Add(gStr1);

                DateTime dtStart = DateTime.Now;
                foreach (string line in reader)
                {
                    procLine(line);
                    if (lastCount != pizzaCookCount)
                    {
                        Console.WriteLine(line); outLines.Add(line);
                        lastCount = pizzaCookCount;
                    }
                }
                Console.WriteLine($" - proc time: {(DateTime.Now-dtStart).ToString()}");
            }
            File.WriteAllLines(path + "linesToAnalize.txt", outLines.ToArray(), fileEncoding);

            Console.Write("\n\nPress any key..."); Console.ReadKey();
        }

        private static void procLine(string line)
        {
            string s1;
            if (line.Contains(gsFind))
            {
                s1 = line.Substring(line.IndexOf(gsFind) + gsFind.Length);
                string[] sa1 = s1.Split(gsSeparator, StringSplitOptions.RemoveEmptyEntries);
                int iBuf;
                foreach (string item in sa1)
                {
                    if (item.StartsWith("10/") && int.TryParse(item.Substring(3), out iBuf))
                    {
                        pizzaCookCount = iBuf;
                        break;
                    }
                }
            }
        }

    }  // class
}
