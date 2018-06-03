using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using IntegraLib;
using IntegraWPFLib;

namespace ClientOrderQueue.Lib
{
    public static class AppLib
    {
        // общий логгер
        public static NLog.Logger AppLogger;

        static AppLib()
        {
        }

        #region app logger
        // логгер приложения
        public static string InitAppLogger()
        {
            string retVal = null;
            try
            {
                AppLogger = NLog.LogManager.GetLogger("fileLogger");
                if (AppLogger.IsTraceEnabled == false) throw new Exception("Ошибка конфигурирования логгера. Проверьте настройки логгера в файле ClientOrderQueue.exe.config");
            }
            catch (Exception ex)
            {
                retVal = ex.Message;
            }
            return retVal;
        }

        // отладочные сообщения
        public static void WriteLogTraceMessage(string msg)
        {
            if (WpfHelper.GetAppGlobalBool("IsWriteTraceMessages") && AppLogger.IsTraceEnabled)
                AppLogger.Trace(msg ?? "null");
        }
        public static void WriteLogTraceMessage(string format, params object[] args)
        {
            if (WpfHelper.GetAppGlobalBool("IsWriteTraceMessages") && AppLogger.IsTraceEnabled)
                AppLogger.Trace(format, args);
        }

        // сообщения о действиях пользователя
        public static void WriteLogUserAction(string msg)
        {
            if (WpfHelper.GetAppGlobalBool("IsLogUserAction") && AppLogger.IsTraceEnabled) AppLogger.Trace("userAct: " + msg);
        }
        public static void WriteLogUserAction(string format, params object[] paramArray)
        {
            if (WpfHelper.GetAppGlobalBool("IsLogUserAction") && AppLogger.IsTraceEnabled) AppLogger.Trace("userAct: " + format, paramArray);
        }

        public static void WriteLogInfoMessage(string msg)
        {
            if (AppLogger.IsInfoEnabled) AppLogger.Info(msg ?? "null");
        }
        public static void WriteLogInfoMessage(string format, params object[] args)
        {
            if (AppLogger.IsInfoEnabled) AppLogger.Info(format, args);
        }

        public static void WriteLogErrorMessage(string msg)
        {
            if (AppLogger.IsErrorEnabled) AppLogger.Error(msg ?? "null");
        }
        public static void WriteLogErrorMessage(string format, params object[] args)
        {
            if (AppLogger.IsErrorEnabled) AppLogger.Error(format, args);
        }
        public static void WriteLogErrorShortMessage(Exception ex)
        {
            if (AppLogger.IsErrorEnabled) AppLogger.Error(ErrorHelper.GetShortErrMessage(ex));
        }

        #endregion

        internal static bool CheckDBConnection(Type dbType)
        {
            AppLib.WriteLogInfoMessage("Проверка доступа к базе данных...");
            string strConn = CfgFileHelper.GetDBConnectionStringFromConfigFile(dbType.Name);
            AppLib.WriteLogInfoMessage(" - строка подключения из config-файла: " + strConn);

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
                ConnectRetryCount = 1,
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
                AppLib.WriteLogErrorMessage(" - ошибка доступа к БД: " + ex.Message);
            }
            finally
            {
                testConn.Close();
                testConn = null;
            }

            AppLib.WriteLogTraceMessage(" - проверка доступа к базе данных - " + ((retVal) ? "READY" : "ERROR!!!"));
            return retVal;
        }

        #region this App

        // преобразовать TimeSpan в строку
        public static string GetAppStringTS(TimeSpan tsTimerValue)
        {
            string sFormat = (tsTimerValue.Days != 0d) 
                ? @"d\.hh\:mm\:ss" 
                : ((tsTimerValue.Hours > 0d) ? @"hh\:mm\:ss" : @"mm\:ss");

            string retVal = tsTimerValue.ToString(sFormat);
            // отрицательное время
            if (tsTimerValue.Ticks < 0) retVal = "-" + retVal;

            return retVal;
        }
        // преобразовать строку в TimeSpan
        internal static TimeSpan GetTSFromString(string tsString)
        {
            TimeSpan ts = TimeSpan.Zero;
            TimeSpan.TryParse(tsString, out ts);
            return ts;
        }

        #endregion

    }  // class
}
