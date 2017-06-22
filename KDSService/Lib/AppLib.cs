using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Data.Entity;
using System.Data.SqlClient;

namespace KDSService
{
    public static class AppLib
    {
        #region bitwise
        public static void SetBit(ref int bitMask, int bit)
        {
            bitMask |= (1 << bit);
        }
        public static void ClearBit(ref int bitMask, int bit)
        {
            bitMask &= ~(1 << bit);
        }
        public static bool IsSetBit(int bitMask, int bit)
        {
            int val = (1 << bit);
            return (bitMask & val) == val;
        }
        #endregion

        #region system info
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

        internal static bool CheckDBConnection(Type dbType)
        {
            //AppLib.WriteLogTraceMessage("- проверка доступа к базе данных...");

            // контекст БД
            DbContext dbContext = (DbContext)Activator.CreateInstance(dbType);

            SqlConnection dbConn = (SqlConnection)dbContext.Database.Connection;
            //AppLib.WriteLogTraceMessage("-- строка подключения: " + dbConn.ConnectionString);

            // создать такое же подключение, но с TimeOut = 1 сек
            SqlConnectionStringBuilder confBld = new SqlConnectionStringBuilder(dbConn.ConnectionString);
            SqlConnectionStringBuilder testBld = new SqlConnectionStringBuilder()
            {
                DataSource = confBld.DataSource,
                InitialCatalog = confBld.InitialCatalog,
                PersistSecurityInfo = confBld.PersistSecurityInfo,
                IntegratedSecurity = confBld.IntegratedSecurity,
                UserID = confBld.UserID,
                Password = confBld.Password,
                ConnectTimeout = 1
            };
            SqlConnection testConn = new SqlConnection(testBld.ConnectionString);
            bool retVal = false;
            try
            {
                testConn.Open();
                retVal = true;
            }
            catch (Exception ex)
            {
                //AppLib.WriteLogErrorMessage("--- ошибка доступа к БД: " + ex.Message);
            }
            finally
            {
                testConn.Close();
                testConn = null;
            }

            //AppLib.WriteLogTraceMessage("- проверка доступа к базе данных - " + ((retVal) ? "READY" :"ERROR!!!"));
            return retVal;
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

        #endregion

    }  // class

}
