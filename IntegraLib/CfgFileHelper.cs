using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Linq;

namespace IntegraLib
{
    public static class CfgFileHelper
    {
        // настройки из config-файла
        // Returns:
        //     A System.String that contains the comma-separated list of values associated with
        //     the specified key, if found; otherwise, null.
        public static string GetAppSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public static string GetAppSettingsFromConfigFile()
        {
            return GetAppSettingsFromConfigFile(ConfigurationManager.AppSettings.AllKeys);
        }
        public static string GetAppSettingsFromConfigFile(string appSettingNames)
        {
            if (appSettingNames == null) return null;
            return GetAppSettingsFromConfigFile(appSettingNames.Split(';'));
        }
        public static string GetAppSettingsFromConfigFile(string[] appSettingNames)
        {
            StringBuilder sb = new StringBuilder();
            string sValue;
            foreach (string settingName in appSettingNames)
            {
                sValue = ConfigurationManager.AppSettings[settingName];
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(string.Format("{0}={1}", settingName, (sValue==null) ? "null" : "\"" + sValue + "\""));
            }
            return sb.ToString();
        }

        // запись значения в config-файл
        // ConfigurationManager НЕ СОХРАНЯЕТ КОММЕНТАРИИ!!!!
        //public static void SaveValueToConfig(string key, string value)
        //{
        //    // Open App.Config of executable
        //    System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        //    // Add an Application Setting.
        //    config.AppSettings.Settings.Remove(key);
        //    config.AppSettings.Settings.Add(key, value);
        //    // Save the configuration file.
        //    config.Save(ConfigurationSaveMode.Modified);
        //    // Force a reload of a changed section.
        //    ConfigurationManager.RefreshSection("appSettings");
        //}

        // работа с config-файлом как с XML-документом - сохраняем комментарии
        // параметр appSettingsDict - словарь из ключа и значения (string), которые необх.сохранить в разделе appSettings
        public static bool SaveAppSettings(Dictionary<string, string> appSettingsDict, out string errorMsg)
        {
            // Open App.Config of executable
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            string cfgFilePath = config.FilePath;
            bool isSeparateAppSettings = false;
            string appCfgFile = config.AppSettings.SectionInformation.ConfigSource;
            if (appCfgFile.IsNull() == false)
            {
                isSeparateAppSettings = true;
                cfgFilePath = System.IO.Path.GetDirectoryName(cfgFilePath) + "\\" + appCfgFile;
            }

            try
            {
                errorMsg = null;
                string filename = cfgFilePath;

                //Load the config file as an XDocument
                XDocument document = XDocument.Load(filename, LoadOptions.PreserveWhitespace);
                if (document.Root == null)
                {
                    errorMsg = "Document was null for XDocument load.";
                    return false;
                }

                // получить раздел appSettings
                XElement xAppSettings;
                if (isSeparateAppSettings)   // в отдельном файле
                {
                    xAppSettings = document.Root;
                }
                else    // в App.config
                {
                    xAppSettings = document.Root.Element("appSettings");
                    if (xAppSettings == null)
                    {
                        xAppSettings = new XElement("appSettings");
                        document.Root.Add(xAppSettings);
                    }
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

                // Force a reload of a changed section.
                ConfigurationManager.RefreshSection("appSettings");

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = string.Format("There was an exception while trying to update the config file ({0}): {1}", 
                    cfgFilePath, ex.ToString());
                return false;
            }
        }


        public static bool SaveAppSettings(string key, string value, out string errorMsg)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>() { { key, value } };

            bool retVal = SaveAppSettings(dict, out errorMsg);
            if (retVal == false)
            {
                errorMsg += string.Format(": key={0}, value={1}", key, value);
            }
            return retVal;
        }

        public static SolidColorBrush GetBrushByName(string brushName, string defaultBrushName = null)
        {
            SolidColorBrush retVal = null;

            // кисть задана через RGB
            if (brushName.Contains(";"))
            {
                string[] rgb = brushName.Split(';');
                if (rgb.Length == 3)
                {
                    Color c = Color.FromRgb(Convert.ToByte(rgb[0]), Convert.ToByte(rgb[1]), Convert.ToByte(rgb[2]));
                    retVal = new SolidColorBrush(c);
                }
            }

            // кисть задана именем из перечисления Brushes
            else
            {
                Type t = typeof(Brushes);
                System.Reflection.PropertyInfo[] bProps = t.GetProperties();
                foreach (System.Reflection.PropertyInfo item in bProps)
                {
                    if (item.Name == brushName)
                    {
                        retVal = (SolidColorBrush)item.GetValue(null, null);
                        break;
                    }
                }
            }

            if ((retVal == null) && (defaultBrushName != null)) retVal = GetBrushByName(defaultBrushName);

            return retVal;
        }


    }  // class CfgFileHelper


    // Change default app.config at runtime
    // https://stackoverflow.com/questions/6150644/change-default-app-config-at-runtime/6151688#6151688
    /*
    // the default app.config is used.
    using(AppConfig.Change(tempFileName))
    {
        // the app.config in tempFileName is used
    }
    // the default app.config is used.
    If you want to change the used app.config for the whole runtime of your application, simply put AppConfig.Change(tempFileName) without the using somewhere at the start of your application.
     */
    public abstract class AppConfig : IDisposable
    {
        public static AppConfig Change(string path)
        {
            return new ChangeAppConfig(path);
        }

        public abstract void Dispose();

        private class ChangeAppConfig : AppConfig
        {
            private readonly string oldConfig =
                AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE").ToString();

            private bool disposedValue;

            public ChangeAppConfig(string path)
            {
                AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", path);
                ResetConfigMechanism();
            }

            public override void Dispose()
            {
                if (!disposedValue)
                {
                    AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", oldConfig);
                    ResetConfigMechanism();


                    disposedValue = true;
                }
                GC.SuppressFinalize(this);
            }

            private static void ResetConfigMechanism()
            {
                typeof(ConfigurationManager)
                    .GetField("s_initState", BindingFlags.NonPublic |
                                             BindingFlags.Static)
                    .SetValue(null, 0);

                typeof(ConfigurationManager)
                    .GetField("s_configSystem", BindingFlags.NonPublic |
                                                BindingFlags.Static)
                    .SetValue(null, null);

                typeof(ConfigurationManager)
                    .Assembly.GetTypes()
                    .Where(x => x.FullName ==
                                "System.Configuration.ClientConfigPaths")
                    .First()
                    .GetField("s_current", BindingFlags.NonPublic |
                                           BindingFlags.Static)
                    .SetValue(null, null);
            }
        }
    }

}
