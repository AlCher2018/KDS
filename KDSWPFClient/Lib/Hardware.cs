using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Xml.Linq;


namespace KDSWPFClient.Lib
{
	public static class Hardware
	{
		public static bool AddNewKey(string name, string key)
		{
			string des = Password.Decrypt(name, key);
			if (des == "Ошибка ввода")
			{
				return false;
			}

            XElement doc = getInitFileXML(key);
            if (doc == null) return false;

			doc.Descendants("Cpu").First<XElement>().Attribute("Key").Value = des;
			Password.EncryptStringToFile(doc.ToString(), "E_init.PSW", key);
			return true;
		}

		public static string getCPUID()
		{
			string str;
			string cpuid = "";
			try
			{
				foreach (ManagementBaseObject mo in (new ManagementObjectSearcher("Select ProcessorID From Win32_processor")).Get())
				{
					cpuid = mo["ProcessorID"].ToString();
				}
				str = cpuid;
			}
			catch (Exception)
			{
				str = cpuid;
			}
			return str;
		}

		public static bool SeeHardware(string cpu, string key)
		{
            XElement doc = getInitFileXML(key);
            if (doc == null) return false;
            
			string proccessors = doc.Descendants("Cpu").Attributes("Key").First<XAttribute>().Value;
			string value = doc.Descendants("NumberOrderman").Attributes("Num").First<XAttribute>().Value;
			
			return (proccessors == cpu);
		}


        public static bool SeeOrdermanSn(string key, int id)
		{
			XElement doc = getInitFileXML(key);
            if (doc == null) return false;

			IEnumerable<XElement> orderman = 
				from z in doc.Descendants("Orderman")
				where z.Attributes("sa").First<XAttribute>().Value == id.ToString()
				select z;
			return orderman.Any<XElement>();
        }  // public static bool SeeOrdermanSn(string key, int id)

        private static XElement getInitFileXML(string key)
        {
            string list = Password.DecryptFileToString("E_init.PSW", key);
            if ((list == null) || (list == "ERROR")) return null;

            return XElement.Parse(list);

        } // private static XElement getXInitFile(string key)

    }
}