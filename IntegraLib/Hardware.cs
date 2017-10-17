using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Xml.Linq;


namespace IntegraLib
{
	public static class Hardware
	{
		public static string getCPUID()
		{
			string retVal = "Lorem ipsum dolor sit amet";  // рыба для ошибочного cpuId
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

        // in Mb
        public static int getAvailableRAM()
        {
            int retVal = 0;

            // class get memory size in kB
            System.Management.ManagementObjectSearcher mgmtObjects = new System.Management.ManagementObjectSearcher("Select * from Win32_OperatingSystem");
            foreach (var item in mgmtObjects.Get())
            {
                //System.Diagnostics.Debug.Print("FreePhysicalMemory:" + item.Properties["FreeVirtualMemory"].Value);
                //System.Diagnostics.Debug.Print("FreeVirtualMemory:" + item.Properties["FreeVirtualMemory"].Value);
                //System.Diagnostics.Debug.Print("TotalVirtualMemorySize:" + item.Properties["TotalVirtualMemorySize"].Value);
                retVal = (Convert.ToInt32(item.Properties["FreeVirtualMemory"].Value)) / 1024;
            }
            return retVal;
        }

        public static string getMBInfo()
        {
            string retVal = "";
            try
            {
                foreach (ManagementBaseObject mo in (new ManagementObjectSearcher("Select * From Win32_BaseBoard")).Get())
                {
                    retVal = (mo["Product"] ?? "").ToString() + ";" + (mo["SerialNumber"] ?? "").ToString();
                }
                foreach (ManagementBaseObject mo in (new ManagementObjectSearcher("Select UUID From Win32_ComputerSystemProduct")).Get())
                {
                    if (retVal.Length > 0) retVal += ";";
                    retVal += (mo["UUID"] ?? "").ToString();
                }
            }
            catch (Exception)
            {
            }
            return retVal;
        }

        public static string getHDDInfo()
        {
            string retVal = "";
            try
            {
                foreach (ManagementBaseObject mo in (new ManagementObjectSearcher("Select Model, SerialNumber From Win32_DiskDrive")).Get())
                {
                    if (retVal.Length > 0) retVal += ";";
                    retVal += (mo["Model"] ?? "").ToString() + ";" + (mo["SerialNumber"] ?? "").ToString();
                }
            }
            catch (Exception)
            {
            }
            return retVal;
        }

    }  // class
}