using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Xml.Linq;


namespace KDSWPFClient.Lib
{
	public static class Hardware
	{
		public static string getCPUID()
		{
			string retVal = null;
			try
			{
				foreach (ManagementBaseObject mo in (new ManagementObjectSearcher("Select ProcessorID From Win32_processor")).Get())
				{
					retVal = mo["ProcessorID"].ToString();
				}
			}
			catch (Exception)
			{
			}
			return retVal;
		}

        public static string getMAC()
        {
            string retVal = null, s;
            try
            {
                foreach (ManagementBaseObject mo in (new ManagementObjectSearcher("Select MACAddress From Win32_NetworkAdapter Where NetEnabled=True AND Installed=True AND PhysicalAdapter=true")).Get())
                {
                    s = mo["MACAddress"].ToString();

                    if (retVal.IsNull()) retVal = s;
                    else retVal += ";" + s;
                }
            }
            catch (Exception)
            {
//                Debug.Print(ex.Message);
            }

            return retVal;
        }

        public static bool SeeHardware(string fileName, string cpu)
		{
            XElement doc = getInitFileXML(fileName);
            if (doc == null) return false;
            
			string proccessors = doc.Descendants("Cpu").Attributes("Key").First<XAttribute>().Value;
			
			return (proccessors == cpu);
		}



        private static XElement getInitFileXML(string fileName)
        {
            string result = Password.DecryptFileToString(fileName);

            if ((result == null) || (result.StartsWith("ERROR")))
            {
                AppLib.WriteLogErrorMessage(result);
                return null;
            }

            XElement retVal = null;
            try
            {
                retVal = XElement.Parse(result);
            }
            catch (Exception)
            {
            }

            return retVal;
        }

    }
}