using IntegraLib;
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
            //XDocument docSrc = XDocument.Load("D:\\initSrc.xml");
            //XDocument docDest = XDocument.Load("D:\\initDst.xml");

            if (ConfigXMLConverter.IsValidConfigFile(docSrc))
            {
                Console.WriteLine("converting to app params XML...");
                docSrc = ConfigXMLConverter.ConvertConfigToXML(docSrc);
                docDest = ConfigXMLConverter.ConvertConfigToXML(docDest);
            }

            Console.WriteLine("compare XML documents...\n");
            IntegraLib.XMLComparer xCmp = new IntegraLib.XMLComparer(docSrc, docDest);
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

                    Console.WriteLine("\nchanging...");
                    if (xCmp.Update())
                    {
                        Console.WriteLine("Change SUCCESSFULL!");
                    }
                    else if (xCmp.ErrorMessage != null)
                    {
                        Console.WriteLine("error: " + xCmp.ErrorMessage);
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
