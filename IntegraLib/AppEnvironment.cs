using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegraLib
{
    public static class AppEnvironment
    {

        public static string GetEnvironmentString()
        {
            return string.Format("Environment: machine={0}, user={1}, current directory={2}, OS version={3}, isOS64bit={4}, processor count={5}, free RAM={6} Mb",
                Environment.MachineName, Environment.UserName, Environment.CurrentDirectory, Environment.OSVersion, Environment.Is64BitOperatingSystem, Environment.ProcessorCount, Hardware.getAvailableRAM());
        }

        public static string GetAppFileName()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.ManifestModule.Name;
        }

        public static string GetAppFullFile()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.Location;
        }

        public static string GetAppDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetAppDirectory(string subDir)
        {
            return GetAppDirectory() + subDir + ((subDir.EndsWith("\\")) ? "" : "\\");
        }

        public static string GetFullFileName(string relPath, string fileName)
        {
            return getFullPath(relPath) + fileName;
        }
        private static string getFullPath(string relPath)
        {
            string retVal = relPath;

            if (string.IsNullOrEmpty(relPath))  // путь не указан в конфиге - берем путь приложения
                retVal = GetAppDirectory();
            else if (retVal.Contains(@"\:") == false)  // относительный путь
            {
                retVal = GetAppDirectory() + retVal;
            }
            if (retVal.EndsWith(@"\") == false) retVal += @"\";

            return retVal;
        }

        public static string GetAppVersion()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        public static void RestartApplication(string args = null)
        {
            System.Diagnostics.Process curProcess = System.Diagnostics.Process.GetCurrentProcess();

            System.Diagnostics.ProcessStartInfo pInfo = new System.Diagnostics.ProcessStartInfo();
            //pInfo.Arguments = string.Format("/C \"{0}\"", System.Reflection.Assembly.GetExecutingAssembly().Location);
            //pInfo.FileName = "cmd.exe";
            pInfo.FileName = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (args.IsNull() == false) pInfo.Arguments = args;

            System.Diagnostics.Process.Start(pInfo);

            curProcess.Kill();
        }


    }  // class
}
