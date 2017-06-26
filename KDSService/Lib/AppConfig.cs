using System;
using System.Configuration;
using System.Linq;
using System.Reflection;

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