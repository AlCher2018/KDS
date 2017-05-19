using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KDSWPFClient.Lib
{
    public static class ConfigHelper
    {
        public static string[] GetDepartmentsUID()
        {
            string sBuf = ConfigurationManager.AppSettings["depUIDs"];
            if (sBuf != null)  return sBuf.Split(',');

            return null;
        }

        public static bool SaveAppSettings(Dictionary<string, string> appSettingsDict, out string errorMsg)
        {
            // Open App.Config of executable
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            try
            {
                errorMsg = null;
                string filename = config.FilePath;

                //Load the config file as an XDocument
                XDocument document = XDocument.Load(filename, LoadOptions.PreserveWhitespace);
                if (document.Root == null)
                {
                    errorMsg = "Document was null for XDocument load.";
                    return false;
                }

                // получить раздел appSettings
                XElement xAppSettings = document.Root.Element("appSettings");
                if (xAppSettings == null)
                {
                    xAppSettings = new XElement("appSettings");
                    document.Root.Add(xAppSettings);
                }

                // цикл по ключам словаря значений
                foreach (KeyValuePair<string, string> item in appSettingsDict)
                {
                    XElement appSetting = xAppSettings.Elements("add").FirstOrDefault(x => x.Attribute("key").Value == item.Key);
                    if (appSetting == null)
                    {
                        //Create the new appSetting
                        xAppSettings.Add(new XElement("add", new XAttribute("key", item.Key), new XAttribute("value", item.Value)));
                    }
                    else
                    {
                        //Update the current appSetting
                        appSetting.Attribute("value").Value = item.Value;
                    }
                }

                //Save the changes to the config file.
                document.Save(filename, SaveOptions.DisableFormatting);
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = "There was an exception while trying to update the config file: " + ex.ToString();
                return false;
            }
        }

    }  // class
}
