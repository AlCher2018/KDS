using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;


namespace IntegraLib
{
    // служебный класс для CRUD-методов к данным (Create-Read-Update-Delete)

    public static class DBContext
    {
        private static int _commandTimeout = 2;   // execute command timeout - 2 seconds
        private static string _configConnString;
        private static string _errMsg;

        public static string ErrorMessage { get { return _errMsg; } }

        #region private methods

        private static string getConnString()
        {
            return _configConnString;
        }
        private static SqlConnection getConnection()
        {
            string connString = getConnString();
            if (connString == null) return null;

            SqlConnection retVal = null;
            try
            {
                retVal = new SqlConnection(connString);
            }
            catch (Exception e)
            {
                showMsg("Ошибка создания подключения к БД: " + e.Message);
            }
            return retVal;
        }


        // открыть подключение к БД
        private static bool openDB(SqlConnection conn)
        {
            if (conn == null) return false;
            try
            {
                conn.Open();
                return true;
            }
            catch (Exception e)
            {
                showMsg("Ошибка открытия подключения к БД: " + e.Message);
                return false;
            }
        }
        // обертка для закрыть подключение
        private static void closeDB(SqlConnection conn)
        {
            if ((conn == null) || (conn.State == ConnectionState.Closed)) return;

            try
            {
                conn.Close();
            }
            catch (Exception e)
            {
                showMsg("Ошибка закрытия подключения к БД: " + e.Message);
            }
        }

        private static void showMsg(string msg)
        {
            //MessageBox.Show(msg, "Ошибка доступа к данным", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //FlyDoc.Forms.MainForm.SendMail(@"asu@kc.epicentrk.com", "Error!", "Упс, помилка!\nНа комп'ютері: " + System.Environment.MachineName + " З користувачем: " + System.Environment.UserName + " сталася наступна помилка:\n\n" + msg);
            _errMsg = msg;
        }

        #endregion

        #region Public methods
        public static void ReadConnectionString(string configConnStringName)
        {
            try
            {
                _configConnString = ConfigurationManager.ConnectionStrings[configConnStringName].ConnectionString;
                _configConnString = _configConnString.Replace(Environment.NewLine, "");

                // get provider connection string if connection string contains EF metadata
                if (_configConnString.StartsWith("metadata", StringComparison.OrdinalIgnoreCase))
                {
                    _configConnString = _configConnString.Split('"')[1];
                }

                // таймаут подключения к серверу MS SQL - 10 сек. По умолчанию - 15 секунд.
                if (!_configConnString.Contains("Connection Timeout")) _configConnString += "; Connection Timeout=10";
            }
            catch (Exception e)
            {
                showMsg("Ошибка получения строки подключения к БД из config-файла: " + e.Message);
            }
        }


        // получить DataTable из SELECT-запроса
        public static DataTable GetQueryTable(string queryString)
        {
            SqlConnection conn = getConnection();
            if (conn == null) return null;

            DataTable retVal = null;
            if (openDB(conn))
            {
                SqlTransaction st = conn.BeginTransaction(IsolationLevel.ReadUncommitted);
                try
                {
                    SqlDataAdapter da = new SqlDataAdapter(queryString, conn);
                    da.SelectCommand.CommandTimeout = _commandTimeout;
                    da.SelectCommand.Transaction = st;
                    retVal = new DataTable();
                    da.Fill(retVal);
                    st.Commit();
                }
                catch (Exception ex)
                {
                    string errMsg = string.Format("Ошибка выполнения запроса MS SQL Server-у: запрос - {0}, ошибка - {1}", queryString, ex.Message);
                    showMsg(errMsg);
                    retVal = null;
                }
                finally
                {
                    st.Dispose();
                    closeDB(conn);
                }
            }

            return retVal;
        }

        // метод, который выполняет SQL-запрос, не возвращающий данные, напр. вставка или удаление строк
        public static int Execute(string sqlText)
        {
            int retVal = -1;
            SqlConnection conn = getConnection();
            if (conn == null) return retVal;

            if (openDB(conn))
            {
                SqlCommand sc = conn.CreateCommand();
                sc.CommandText = sqlText;
                sc.CommandTimeout = _commandTimeout;

                SqlTransaction st = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    retVal = sc.ExecuteNonQuery();
                    st.Commit();
                }
                catch (Exception ex)
                {
                    showMsg("Ошибка выполнения команды в MS SQL Server: " + ex.Message);
                }
                finally
                {
                    st.Dispose();
                    closeDB(conn);
                }
            }

            return retVal;
        }
        #endregion

        public static int GetLastInsertedId()
        {
            DataTable dt = GetQueryTable("SELECT @@IDENTITY");
            var retVal = dt.Rows[0][0];
            return (retVal == null) ? 0 : (int)retVal;
        }


        #region User
        // получить всех пользователей
        //public static DataTable GetUsers()
        //{
        //    string sqlText = "SELECT * FROM vwUsers";
        //    return GetQueryTable(sqlText);
        //}

        // получить настройки пользователя
        //public static DataRow GetUserConfig(string PC, string UserName)
        //{
        //    string sqlText = string.Format("SELECT * FROM Access WHERE (PC='{0}') AND (UserName='{1}')", PC, UserName);
        //    DataTable dt = GetQueryTable(sqlText);
        //    return ((dt == null) || (dt.Rows.Count == 0)) ? null : dt.Rows[0];
        //}

        //public static bool InsertUser(User user, out int newId)
        //{
        //    string sqlText = string.Format("INSERT INTO Access (PC, UserName, Department, Notes, Schedule, Phone, Config, ApprovedNach, ApprovedSB, ApprovedDir, Mail) VALUES ('{0}', '{1}', {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, '{10}'); SELECT @@IDENTITY",
        //        user.PC, user.UserName, user.DepartmentId,
        //        (user.AllowNote) ? 1 : 0, (user.AllowSchedule) ? 1 : 0, (user.AllowPhonebook) ? 1 : 0, (user.AllowConfig) ? 1 : 0, (user.AllowApprovedNach) ? 1 : 0,
        //        (user.AllowApproverSB) ? 1 : 0, (user.AllowApproverDir) ? 1 : 0, user.enterMail);
        //    DataTable dt = GetQueryTable(sqlText);
        //    newId = Convert.ToInt32(dt.Rows[0][0]);
        //    return (newId > 0);
        //}
        //public static bool UpdateUser(User user)
        //{
        //    string sqlText = string.Format("UPDATE Access SET PC = '{1}', UserName = '{2}', Department = {3}, Notes = {4}, Schedule = {5}, Phone = {6}, Config = {7}, ApprovedNach = {8}, ApprovedSB = {9}, ApprovedDir = {10}, Mail = '{11}' WHERE (Id = {0})",
        //        user.Id, user.PC, user.UserName, user.DepartmentId,
        //        (user.AllowNote) ? 1 : 0, (user.AllowSchedule) ? 1 : 0, (user.AllowPhonebook) ? 1 : 0, (user.AllowConfig) ? 1 : 0, (user.AllowApprovedNach) ? 1 : 0,
        //        (user.AllowApproverSB) ? 1 : 0, (user.AllowApproverDir) ? 1 : 0, user.enterMail);
        //    return Execute(sqlText);
        //}
        //public static bool DeleteUser(int Id)
        //{
        //    string sqlText = string.Format("DELETE FROM Access WHERE (Id = {0})", Id);
        //    return Execute(sqlText);
        //}
        #endregion

    }  // class DBContext
}