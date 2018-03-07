using IntegraLib;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace KDSService.DataSource
{
    // служебный класс для CRUD-методов к данным (Create-Read-Update-Delete)
    // методы записи/чтения из/в БД - экземплярные и запускать во вторичных потоках
    // 2018-03-05 Добавлена возможность работать с уровнем совместимости БД
    //    Можно установить CommandTimeout при выполнении запросов к БД
    //    Использованы явные транзакции с возможностью установить уровень изоляции транзакции
    //    DBContext может быть Enabled или Disabled
    //    При ошибках вызывается Action<string> OnDBErrorAction и можно задать статическое свойство IsThrowExceptionOnError - выбрасывать ли исключение при ошибках. По умолчанию, false.
    public class DBContext : IDisposable
    {
        #region static members
        private static string _configConStringName = "KDSEntities";
        public static string ConfigConnectionStringName
        {
            get { return _configConStringName; }
            set { _configConStringName = value; }
        }

        private static IFormatProvider _numericFormatter;

        private static Dictionary<int, string> _mssqlCompatibleLevels;

        public static bool Enable { get; set; }

        public static Action<string> OnBeforeExecute;
        public static Action<string> OnDBErrorAction;
        public static bool IsThrowExceptionOnError { get; set; }

        // execute command timeout
        private static int _commandTimeout;
        public static int CommandTimeout
        {
            get { return _commandTimeout; }
            set { _commandTimeout = value; }
        }

        public static string LastErrorText { get; set; }


        // static CTOR
        static DBContext()
        {
            Enable = true;
            IsThrowExceptionOnError = false;
            _commandTimeout = 2;   // execute command timeout - 2 seconds

            _numericFormatter = FormatProviderHelper.DotFormatter();

            _mssqlCompatibleLevels = new Dictionary<int, string>()
            {
                {80, "SQL Server 2000"},
                {90, "SQL Server 2005"},
                {100, "SQL Server 2008"},
                {110, "SQL Server 2012"},
                {120, "SQL Server 2014"},
                {130, "SQL Server 2016"},
                {140, "SQL Server 2017"},
            };
        }

        private static string getConnectionString()
        {
            return ConfigurationManager.ConnectionStrings[_configConStringName].ConnectionString;
        }

        public static string ConnectionString { get { return getConnectionString(); } }

        #endregion

        private string _connString;

        private string _errMsg;
        public string ErrMsg { get { return _errMsg; } }

        private SqlConnection _conn;

        public DBContext()
        {
            DBContext.LastErrorText = null;
            setConnection();
        }

        #region base funcs
        private void setConnection()
        {
            try
            {
                _connString = DBContext.getConnectionString();
                // получить Connection
                try
                {
                    _conn = new SqlConnection(_connString);
                }
                catch (Exception e)
                {
                    errorAction("Ошибка создания подключения к БД: " + e.Message);
                }
            }
            catch (Exception e)
            {
                errorAction("Ошибка получения строки подключения к БД из config-файла: " + e.Message);
            }
        }

        // открыть подключение к БД
        public bool Open()
        {
            if (DBContext.Enable == false)
            {
                errorAction("DBContext.Enable is FALSE!");
                return false;
            }

            if (_conn == null) return false;
            try
            {
                if (_conn.State == ConnectionState.Broken) _conn.Close();
                if (_conn.State == ConnectionState.Closed) _conn.Open();
                return true;
            }
            catch (Exception e)
            {
                errorAction("Ошибка открытия подключения к БД: " + e.Message);
                return false;
            }
        }

        // получить DataTable из SELECT-запроса
        public DataTable GetQueryTable(string sqlText, IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted)
        {
            OnBeforeExecute?.Invoke(sqlText);

            DataTable retVal = null;
            if (Open())
            {
                SqlTransaction st = _conn.BeginTransaction(isolationLevel);

                SqlDataAdapter da = new SqlDataAdapter(sqlText, _conn);
                da.SelectCommand.CommandTimeout = _commandTimeout;
                da.SelectCommand.Transaction = st;

                retVal = new DataTable();
                try
                {
                    da.Fill(retVal);
                    st.Commit();
                }
                catch (Exception ex)
                {
                    st.Rollback();
                    errorAction(ex.Message);
                    retVal = null;
                }
                finally
                {
                    da.Dispose();
                    st.Dispose();
                }
            }

            return retVal;
        }

        // метод, который выполняет SQL-запрос, не возвращающий данные, напр. вставка или удаление строк
        public int ExecuteCommand(string sqlText, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, bool inTransaction = true)
        {
            OnBeforeExecute?.Invoke(sqlText);

            int retVal = 0;
            if (Open())
            {
                SqlTransaction st = null;
                if (inTransaction) st = _conn.BeginTransaction(isolationLevel);

                SqlCommand sc = _conn.CreateCommand();
                sc.CommandTimeout = _commandTimeout;
                if (inTransaction) sc.Transaction = st;
                sc.CommandType = CommandType.Text;
                sc.CommandText = sqlText;

                try
                {
                    retVal = sc.ExecuteNonQuery();
                    if (inTransaction) st.Commit();
                }
                catch (Exception ex)
                {
                    if (inTransaction) st.Rollback();
                    errorAction(ex.Message);
                }
                finally
                {
                    sc.Dispose();
                    if (inTransaction) st.Dispose();
                }
            }
            return retVal;
        }

        public object ExecuteScalar(string sqlText, IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted)
        {
            OnBeforeExecute?.Invoke(sqlText);

            object retVal = null;
            if (Open())
            {
                SqlTransaction st = _conn.BeginTransaction(isolationLevel);

                SqlCommand sc = _conn.CreateCommand();
                sc.CommandTimeout = _commandTimeout;
                sc.Transaction = st;
                sc.CommandType = CommandType.Text;
                sc.CommandText = sqlText;

                try
                {
                    retVal = sc.ExecuteScalar();
                    st.Commit();
                }
                catch (Exception ex)
                {
                    st.Rollback();
                    errorAction(ex.Message);
                    retVal = false;
                }
                finally
                {
                    sc.Dispose();
                    st.Dispose();
                }
            }

            return retVal;
        }

        private void errorAction(string msg)
        {
            _errMsg = msg;
            // и статическое свойство
            DBContext.LastErrorText = _errMsg;

            // различные способы обработки ошибки
            OnDBErrorAction?.Invoke(_errMsg);
            if (IsThrowExceptionOnError) throw new Exception(_errMsg);
        }


        public void Dispose()
        {
            Close();
            if (_conn != null) _conn.Dispose();
        }

        public bool Close()
        {
            if (_conn == null) return false;
            try
            {
                if (_conn.State != ConnectionState.Closed) _conn.Close();
                return true;
            }
            catch (Exception e)
            {
                errorAction("Ошибка закрытия подключения к БД: " + e.Message);
                return false;
            }
        }

        #endregion

        #region schema
        public List<DBTableColumn> GetSchemaColumns(string tableName)
        {
            List<DBTableColumn> retVal = new List<DBTableColumn>();
            if (this.Open())
            {
                try
                {
                    // For the array, 0-member represents Catalog; 1-member represents Schema; 
                    // 2-member represents Table Name; 3-member represents Column Name. 
                    DataTable dt = _conn.GetSchema("Columns", new string[4] { null, null, tableName, null });
                    //printSchemaColumns(dt);
                    if (dt != null)
                    {
                        DBTableColumn col;
                        foreach (DataRow row in dt.Rows)
                        {
                            col = new DBTableColumn();

                            col.Name = (row.IsNull("COLUMN_NAME") ? "NULL" : Convert.ToString(row["COLUMN_NAME"]));
                            col.IsNullable = (row.IsNull("IS_NULLABLE") ? true : row["IS_NULLABLE"].ToString().ToBool());
                            col.TypeName = (row.IsNull("DATA_TYPE") ? "NULL" : Convert.ToString(row["DATA_TYPE"]));
                            col.MaxLenght = (row.IsNull("CHARACTER_MAXIMUM_LENGTH") ? -1 : Convert.ToInt32(row["CHARACTER_MAXIMUM_LENGTH"]));

                            retVal.Add(col);
                        }
                    }
                }
                catch (Exception ex)
                {
                    string errMsg = string.Format("Ошибка получения метаданных для таблицы [{0}]: {1}", tableName, ex.Message);
                    errorAction(errMsg);
                    retVal = null;
                }
                finally
                {
                    Close();
                }
            }

            return retVal;
        }

        private static void printSchemaColumns(DataTable dt)
        {
            Debug.Print("index\tname\ttype");
            int i = 0;
            foreach (DataColumn col in dt.Columns)
            {
                Debug.Print("{0}\t{1}\t{2}", i++, col.ColumnName, col.DataType.Name);
            }
        }

        /*
TABLE COLUMNS
    // For the array, 0-member represents Catalog; 1-member represents Schema; 
    // 2-member represents Table Name; 3-member represents Column Name. 
    DataTable dt = conn.GetSchema("Columns", new string[4] {null, null, tableName, null });
index name              type
0	TABLE_CATALOG       String
1	TABLE_SCHEMA        String
2	TABLE_NAME          String
3	COLUMN_NAME         String
4	ORDINAL_POSITION    Int32
5	COLUMN_DEFAULT      String
6	IS_NULLABLE         String
7	DATA_TYPE           String
8	CHARACTER_MAXIMUM_LENGTH Int32
9	CHARACTER_OCTET_LENGTH  Int32
10	NUMERIC_PRECISION       Byte
11	NUMERIC_PRECISION_RADIX Int16
12	NUMERIC_SCALE           Int32
13	DATETIME_PRECISION      Int16
14	CHARACTER_SET_CATALOG   String
15	CHARACTER_SET_SCHEMA    String
16	CHARACTER_SET_NAME      String
17	COLLATION_CATALOG       String
18	IS_SPARSE               Boolean
19	IS_COLUMN_SET           Boolean
20	IS_FILESTREAM           Boolean
*/
        #endregion

        #region private static async methods
        private static async Task<DataTable> _getQueryTableAsync(string sqlText)
        {
            return await Task.Run(() =>
            {
                DataTable retVal = null;
                using (DBContext db = new DBContext())
                {
                    retVal = db.GetQueryTable(sqlText);
                }
                return retVal;
            });
        }

        private static async Task<int> _executeCommandAsync(string sqlText)
        {
            return await Task.Run(() =>
            {
                int retVal;
                using (DBContext db = new DBContext())
                {
                    retVal = db.ExecuteCommand(sqlText);
                }
                return retVal;
            });
        }

        private static async Task<object> _executeScalarAsync(string sqlText)
        {
            return await Task.Run(() =>
            {
                object retVal;
                using (DBContext db = new DBContext())
                {
                    retVal = db.ExecuteScalar(sqlText);
                }
                return retVal;
            });
        }

        #endregion

        #region static wrappers above async methods
        // получить DataTable из SELECT-запроса
        public static DataTable GetQueryTableAsync(string sqlText)
        {
            if (Enable == false) return null;

            DataTable retVal = null;
            // асинхронно подключиться к БД и выполнить запрос
            Task<DataTable> task = _getQueryTableAsync(sqlText);
            try
            {
                retVal = task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                OnDBErrorAction?.Invoke($"GetQueryTableAsync({sqlText}): {ex.Message}");
                if (DBContext.IsThrowExceptionOnError) throw;
            }
            return retVal;
        }

        // метод, который выполняет SQL-запрос, не возвращающий данные, напр. вставка или удаление строк
        public static int ExecuteCommandAsync(string sqlText)
        {
            if (Enable == false) return 0;

            int retVal = 0;
            // асинхронно подключиться к БД и выполнить запрос
            try
            {
                retVal = _executeCommandAsync(sqlText).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                OnDBErrorAction?.Invoke($"GetQueryTableAsync({sqlText}): {ex.Message}");
                if (DBContext.IsThrowExceptionOnError) throw;
            }
            return retVal;
        }

        // получить одно значение из SELECT-запроса (первая строка, первое поле)
        public static object ExecuteScalarAsync(string sqlText)
        {
            if (Enable == false) return null;

            object retVal = null;
            // асинхронно подключиться к БД и выполнить запрос
            Task<object> task = _executeScalarAsync(sqlText);
            try
            {
                retVal = task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                OnDBErrorAction?.Invoke($"GetQueryTableAsync({sqlText}): {ex.Message}");
                if (DBContext.IsThrowExceptionOnError) throw;
            }
            return retVal;
        }
        #endregion

        #region public static methods
        // проверка доступа к БД
        public static bool CheckDBConnection(string getCountTableName, out string outMsg)
        {
            outMsg = null;
            bool retVal = false;
            using (DBContext db = new DBContext())
            {
                if (getCountTableName != null)
                {
                    string sqlText = $"SELECT Count(*) FROM [{getCountTableName}]";
                    int cnt = (int)db.ExecuteScalar(sqlText);
                    outMsg = $"table [{getCountTableName}] has {cnt.ToString()} records.";
                }
                else
                {
                    outMsg = "DB has opened successful.";
                }
                retVal = true;
            }

            return retVal;
        }

        // попытаться открыть подключение к БД и закрыть его
        public static bool CheckDBConnectionAlt()
        {
            bool retVal = false;
            string connString = DBContext.getConnectionString();
            if (connString == null) return retVal;

            // установить ConnectTimeout в 2 сек
            SqlConnectionStringBuilder confBld = new SqlConnectionStringBuilder(connString);
            SqlConnectionStringBuilder testBld = new SqlConnectionStringBuilder()
            {
                DataSource = confBld.DataSource,
                InitialCatalog = confBld.InitialCatalog,
                PersistSecurityInfo = confBld.PersistSecurityInfo,
                IntegratedSecurity = confBld.IntegratedSecurity,
                UserID = confBld.UserID,
                Password = confBld.Password,
                ConnectTimeout = 2
            };
            SqlConnection testConn = new SqlConnection(testBld.ConnectionString);
            try
            {
                testConn.Open();
                System.Threading.Thread.Sleep(500);
                testConn.Close();
                retVal = true;
            }
            catch (Exception)
            {
            }
            if (testConn != null) testConn.Dispose();
            testConn = null;

            return retVal;
        }


        public static string GetDBName()
        {
            return getDBName();
        }

        private static string getDBName()
        {
            string connString = getConnectionString();
            if (connString == null) return null;

            return (new SqlConnectionStringBuilder(connString)).InitialCatalog;
        }

        // возвращает кол-во записей из таблицы tableName
        public static int GetRowsCount(string tableName)
        {
            int retVal = 0;
            string sqlText = $"SELECT Count(*) FROM [{tableName}]";
            using (DBContext db = new DBContext())
            {
                retVal = (int)db.ExecuteScalar(sqlText);
            }
            return retVal;
        }

        public static int GetLastInsertedId()
        {
            int retVal = 0;
            string sqlText = "SELECT @@IDENTITY";
            using (DBContext db = new DBContext())
            {
                retVal = (int)db.ExecuteScalar(sqlText);
            }
            return retVal;
        }

        public static int GetLastAffectedRowCount()
        {
            int retVal = 0;
            string sqlText = "SELECT @@ROWCOUNT";
            using (DBContext db = new DBContext())
            {
                retVal = (int)db.ExecuteScalar(sqlText);
            }
            return retVal;
        }

        // получить список пар Id, Name из справочника
        public static List<Tuple<int, string>> GetPairIdNameList(string sqlText)
        {
            List<Tuple<int, string>> retVal = new List<Tuple<int, string>>();
            using (DBContext db = new DBContext())
            {
                using (DataTable dt = db.GetQueryTable(sqlText))
                {
                    if (dt != null)
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            retVal.Add(new Tuple<int, string>(Convert.ToInt32(row[0]), Convert.ToString(row[1])));
                        }
                    }
                }
            }
            return ((retVal.Count == 0) ? null : retVal);
        }

        private static Dictionary<int, string> GetPairIdNameDict(string sqlText)
        {
            Dictionary<int, string> retVal = new Dictionary<int, string>();
            using (DBContext db = new DBContext())
            {
                using (DataTable dt = db.GetQueryTable(sqlText))
                {
                    if (dt != null)
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            retVal.Add(Convert.ToInt32(row[0]), (row.IsNull(1) ? null : Convert.ToString(row[1])));
                        }
                    }
                }
            }
            return ((retVal.Count == 0) ? null : retVal);
        }

        #endregion

        #region ms sql server compatible level
        public static int GetDBCompatibleLevel()
        {
            string dbName = getDBName();
            string sqlText = string.Format("SELECT compatibility_level FROM sys.databases where Name = '{0}'", dbName);

            int retVal = 0;
            using (DBContext db = new DBContext())
            {
                retVal = Convert.ToInt32(db.ExecuteScalar(sqlText));
            }

            return retVal;
        }

        public static bool SetDBCompatibleLevel(int dbCompatibleLevel)
        {
            string dbName = getDBName();
            if (dbName == null) return false;

            string sqlText = string.Format("ALTER DATABASE {0} SET COMPATIBILITY_LEVEL = {1}", dbName, dbCompatibleLevel.ToString());

            bool retVal = false;
            using (DBContext db = new DBContext())
            {
                int iResult = db.ExecuteCommand(sqlText, IsolationLevel.Snapshot);
                if ((iResult > 0) || db.ErrMsg.IsNull()) retVal = true;
            }

            return retVal;
        }

        public static bool IsValidCompatibleLevel(int compatibleLevel)
        {
            return _mssqlCompatibleLevels.ContainsKey(compatibleLevel);
        }

        public static string getSQLServerNameByCompatibleLevel(int dbCompatibleLevel)
        {
            string retVal;
            if (_mssqlCompatibleLevels.ContainsKey(dbCompatibleLevel))
                retVal = _mssqlCompatibleLevels[dbCompatibleLevel];
            else
                retVal = "unknown MS SQL Server compatible level: " + dbCompatibleLevel.ToString();

            return retVal;
        }

        #endregion


        #region entities
        public static int ExecuteDMLAndGetAffectedRowCount(string dmlText)
        {
            string sqlText = dmlText + "; SELECT @@ROWCOUNT";
            return getIntValueFromSQLText(sqlText);
        }

        public static int InsertRecordAndReturnNewId(string dmlText)
        {
            string sqlText = dmlText + "; SELECT @@IDENTITY";
            return getIntValueFromSQLText(sqlText);
        }

        // ищет запись в таблице tableName по условию where и, если запись найдена, то возвращает значение поля Id из таблицы, иначе возвращает 0
        public static int FindEntityByWhere(string tableName, string where)
        {
            string sqlText = $"SELECT Id FROM [{tableName}] WHERE ({where})";
            return getIntValueFromSQLText(sqlText);
        }

        private static int getIntValueFromSQLText(string sqlText)
        {
            int retVal = 0;
            using (DBContext db = new DBContext())
            {
                object oValue = db.ExecuteScalar(sqlText);
                if (oValue != null) retVal = Convert.ToInt32(oValue);
            }
            return retVal;
        }

        public static DataRow GetEntityRow(string tableName, int id)
        {
            DataRow retVal = null;
            string sqlText = $"SELECT * FROM [{tableName}] WHERE ([Id]={id.ToString()})";
            using (DBContext db = new DBContext())
            {
                using (DataTable dt = db.GetQueryTable(sqlText))
                {
                    if ((dt != null) && (dt.Rows.Count > 0)) retVal = dt.Rows[0];
                }
            }
            return retVal;
        }
        #endregion

        public static string ConvertToSQLString(object value)
        {
            if ((value == null) || (value.GetType().Equals(typeof(System.DBNull))))
                return "NULL";
            else if (value is string)
                return string.Format("'{0}'", value.ToString());
            else if (value is bool)
                return string.Format("{0}", ((bool)value ? "1" : "0"));
            else if (value is DateTime)
                //return string.Format("CONVERT(datetime, '{0}', 20)", ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss.fff"));
                return "'" + ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss.fff") + "'";
            else if (value is float)
                //преобразование числа с точкой
                return ((float)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            else if (value is double)
                return ((double)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            else if (value is decimal)
                return ((decimal)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            else
                return value.ToString();
        }


    }  // class DBContext

    public class DBTableColumn
    {
        public string Name { get; set; }
        public bool IsNullable { get; set; }
        public string TypeName { get; set; }
        public int MaxLenght { get; set; }
    }

}
