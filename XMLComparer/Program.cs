using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace XMLComparer
{
    class Program
    {
        static void Main(string[] args)
        {
            XDocument docSrc = XDocument.Load("D:\\AppSettingsSrc.config");
            XDocument docDest = XDocument.Load("D:\\AppSettingsDst.config");

            Console.WriteLine("compare XML documents...\n");
            XMLComparer xCmp = new XMLComparer(docSrc, docDest);
            if (xCmp.Compare())
            {
                List<XMLCompareChangeItem> result = xCmp.Changes;
                if (result.Count == 0)
                {
                    Console.WriteLine("изменений нет");
                }
                else
                {
                    foreach (XMLCompareChangeItem item in result)
                    {
                        Console.WriteLine(item.ToString());
                    }
                }
            }
            else
            {
                Console.WriteLine("Error: " + xCmp.ErrorMessage);
            }

            Console.Write("\n\nPress any key..."); Console.ReadKey();
        }
    }
}
