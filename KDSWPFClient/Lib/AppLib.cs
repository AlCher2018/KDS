﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Data;
using System.Data.SqlClient;
using KDSWPFClient.ServiceReference1;
using System.Xml.Linq;
using System.Windows.Media;
using KDSWPFClient.ViewModel;
using KDSWPFClient.View;
using System.Reflection;

namespace KDSWPFClient.Lib
{

    public static class AppLib
    {
        // общий логгер
        public static NLog.Logger AppLogger;
        public static NLog.Logger _dbCommandLogger = null;

        static AppLib()
        {
            // логгер приложения
            AppLogger = NLog.LogManager.GetLogger("appLogger");
        }

        #region app logger

        // общий логгер КДС-клиента и службы
        public static void InitDBCommandLogger()
        {
            // параметр берем сразу из конфига
            string cfgValue = AppLib.GetAppSetting("SingleClientSvcLog");
            if ((cfgValue != null) && cfgValue.ToBool())
            {
                _dbCommandLogger = NLog.LogManager.GetLogger("dbCommandTracer");
            }
        }
        // отладочные сообщения
        public static void WriteLogTraceMessage(string msg)
        {
            if ((bool)AppLib.GetAppGlobalValue("IsWriteTraceMessages", false))
            {
                if (_dbCommandLogger != null)
                    _dbCommandLogger.Info(msg);
                else
                    AppLogger.Trace(msg ?? "null");
            }
        }
        public static void WriteLogTraceMessage(string format, params object[] args)
        {
            if ((bool)AppLib.GetAppGlobalValue("IsWriteTraceMessages", false))
            {
                if (_dbCommandLogger != null)
                    _dbCommandLogger.Info(format, args);
                else
                    AppLogger.Trace(format, args);
            }
        }

        // сообщения о действиях пользователя
        public static void WriteLogUserAction(string msg)
        {
            if ((bool)AppLib.GetAppGlobalValue("IsLogUserAction", false) 
                && AppLogger.IsTraceEnabled)
                AppLogger.Trace("userAct: " + msg);
        }
        public static void WriteLogUserAction(string format, params object[] paramArray)
        {
            if ((bool)AppLib.GetAppGlobalValue("IsLogUserAction", false) && AppLogger.IsTraceEnabled) AppLogger.Trace("userAct: " + format, paramArray);
        }

        public static void WriteLogInfoMessage(string msg)
        {
            if (AppLogger.IsInfoEnabled) AppLogger.Info(msg??"null");
        }
        public static void WriteLogInfoMessage(string format, params object[] args)
        {
            if (AppLogger.IsInfoEnabled) AppLogger.Info(format, args);
        }

        public static void WriteLogErrorMessage(string msg)
        {
            if (AppLogger.IsErrorEnabled) AppLogger.Error(msg??"null");
        }
        public static void WriteLogErrorMessage(string format, params object[] args)
        {
            if (AppLogger.IsErrorEnabled) AppLogger.Error(format, args);
        }

        #endregion

        #region system info
        internal static string GetEnvironmentString()
        {
            return string.Format("Environment: machine={0}, user={1}, current directory={2}, OS version={3}, isOS64bit={4}, processor count={5}, free RAM={6} Mb",
                Environment.MachineName, Environment.UserName, Environment.CurrentDirectory, Environment.OSVersion, Environment.Is64BitOperatingSystem, Environment.ProcessorCount, getAvailableRAM());
        }


        // in Mb
        private static int getAvailableRAM()
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


        //public static bool CheckDBConnection(Type dbType)
        //{
        //    string s;
        //    WriteLogInfoMessage("Проверка доступа к базе данных...");

        //    // контекст БД
        //    DbContext dbContext = (DbContext)Activator.CreateInstance(dbType);

        //    SqlConnection dbConn = (SqlConnection)dbContext.Database.Connection;
        //    s = " - строка подключения: " + dbConn.ConnectionString;
        //    Console.WriteLine("\n**** SQL Connection String ****\n{0}\n****", dbConn.ConnectionString);
        //    WriteLogInfoMessage(s);

        //    // создать такое же подключение, но с TimeOut = 1 сек
        //    SqlConnectionStringBuilder confBld = new SqlConnectionStringBuilder(dbConn.ConnectionString);
        //    SqlConnectionStringBuilder testBld = new SqlConnectionStringBuilder()
        //    {
        //        DataSource = confBld.DataSource,
        //        InitialCatalog = confBld.InitialCatalog,
        //        PersistSecurityInfo = confBld.PersistSecurityInfo,
        //        IntegratedSecurity = confBld.IntegratedSecurity,
        //        UserID = confBld.UserID,
        //        Password = confBld.Password,
        //        ConnectRetryCount = 1,
        //        ConnectTimeout = 1
        //    };
        //    SqlConnection testConn = new SqlConnection(testBld.ConnectionString);
        //    bool retVal = false;
        //    try
        //    {
        //        testConn.Open();
        //        retVal = true;
        //    }
        //    catch (Exception ex)
        //    {
        //        AppLib.WriteLogErrorMessage("--- ошибка доступа к БД: " + ex.Message);
        //    }
        //    finally
        //    {
        //        testConn.Close();
        //        testConn = null;
        //    }

        //    WriteLogInfoMessage("Проверка доступа к базе данных... " + ((retVal) ? "READY" : "ERROR!!!"));
        //    return retVal;
        //}

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
                retVal = AppLib.GetAppDirectory();
            else if (retVal.Contains(@"\:") == false)  // относительный путь
            {
                retVal = AppLib.GetAppDirectory() + retVal;
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

        public static Point GetWindowTopLeftPoint(Window window)
        {
            double left, top;
            if (window.WindowState == WindowState.Maximized)
            {
                var leftField = typeof(Window).GetField("_actualLeft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                left = (double)leftField.GetValue(window);
            }
            else
                left = window.Left;

            if (window.WindowState == WindowState.Maximized)
            {
                var leftField = typeof(Window).GetField("_actualTop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                top = (double)leftField.GetValue(window);
            }
            else
                top = window.Top;

            return new Point(left, top);
        }

        #endregion

        #region app settings
        // получить настройки приложения из config-файла
        public static string GetAppSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        // настройки из config-файла
        internal static string GetAppSettingsFromConfigFile()
        {
            return GetAppSettingsFromConfigFile(ConfigurationManager.AppSettings.AllKeys);
        }
        internal static string GetAppSettingsFromConfigFile(string appSettingNames)
        {
            if (appSettingNames == null) return null;
            return GetAppSettingsFromConfigFile(appSettingNames.Split(';'));
        }
        internal static string GetAppSettingsFromConfigFile(string[] appSettingNames)
        {
            StringBuilder sb = new StringBuilder();
            string sValue;
            foreach (string settingName in appSettingNames)
            {
                sValue = ConfigurationManager.AppSettings[settingName];
                if (sValue.IsNull() == false)
                {
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append(settingName + "=" + sValue);
                }
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

                // Force a reload of a changed section.
                ConfigurationManager.RefreshSection("appSettings");

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = "There was an exception while trying to update the config file: " + ex.ToString();
                return false;
            }
        }


        public static bool SaveAppSettings(string key, string value)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>() { { key, value } };
            string errMsg = null;

            bool retVal = SaveAppSettings(dict, out errMsg);
            if (retVal == false)
            {
                AppLib.WriteLogErrorMessage("An error occurred while saving the value ({0}: {1}) to config file: {2}",key, value, errMsg);
            }
            return retVal;
        }

        internal static double GetOrdersPageContentHeight()
        {
            double _screenHeight = (double)AppLib.GetAppGlobalValue("screenHeight");
            double topBotMargin = (double)AppLib.GetAppGlobalValue("dishesPanelTopBotMargin");

             return Math.Floor(_screenHeight - 2d * topBotMargin);
        }


        // получить глобальное значение приложения из его свойств
        public static object GetAppGlobalValue(string key, object defaultValue = null)
        {
            IDictionary dict = Application.Current.Properties;
            if (dict == null) return null;

            if (dict.Contains(key) == false) return defaultValue;
            else return dict[key];
        }

        // установить глобальное значение приложения (в свойствах приложения)
        public static void SetAppGlobalValue(string key, object value)
        {
            IDictionary dict = Application.Current.Properties;
            if (dict.Contains(key) == false)  // если еще нет значения в словаре
            {
                dict.Add(key, value);   // то добавить
            }
            else    // иначе - изменить существующее
            {
                dict[key] = value;
            }
        }

        internal static string GetShortErrMessage(Exception ex)
        {
            string retVal = ex.Message;
            if (ex.InnerException != null) retVal += " Inner exception: " + ex.InnerException.Message;
            return retVal;
        }

        #endregion

        #region WPF UI interface

        public static double GetRowHeightAbsValue(Grid grid, int iRow, double totalHeight = 0d)
        {
            if (totalHeight == 0d) totalHeight = grid.Height;

            double cntStars = grid.RowDefinitions.Sum(r => r.Height.Value);
            return grid.RowDefinitions[iRow].Height.Value / cntStars * totalHeight;
        }

        public static bool IsAppVerticalLayout
        {
            get
            {
                double appWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
                double appHeight = (double)AppLib.GetAppGlobalValue("screenHeight");
                return (appWidth < appHeight);
            }
        }

        public static FrameworkElement FindVisualParent(FrameworkElement elementFrom, Type findType, string elementName)
        {
            if (elementFrom == null) return null;

            DependencyObject parent = elementFrom;

            bool isContinue; string sName;
            while (parent != null)
            {
                if (!(parent is DependencyObject)) break;

                isContinue = false;
                sName = (parent as FrameworkElement).Name;

                if ((findType != null) && !parent.GetType().Equals(findType)) isContinue = true;
                if (!isContinue && !elementName.IsNull() && !sName.IsNull() && !elementName.Equals(sName)) isContinue = true;

                if (isContinue)
                {
                    parent = VisualTreeHelper.GetParent(parent);

                    if (parent == null) break;
                    if ((parent is Window) || (parent is Page)) { parent = null; break; }
                }
                else
                {
                    break;
                }
            }

            return (parent == null) ? null : parent as FrameworkElement;
        }

        public static void AssignFontSizeByMeasureHeight(TextBlock tbAssigning, Size measuredSize, double requiredHeight, bool isSubtractSideMarginsAsFontSize)
        {
            double initWidth = measuredSize.Width;
            if (isSubtractSideMarginsAsFontSize) measuredSize.Width = initWidth - 2d * tbAssigning.FontSize;

            tbAssigning.Measure(measuredSize);
            while (tbAssigning.DesiredSize.Height > requiredHeight)
            {
                tbAssigning.FontSize *= 0.9;
                if (isSubtractSideMarginsAsFontSize) measuredSize.Width = initWidth - 2d * tbAssigning.FontSize;

                tbAssigning.Measure(measuredSize);
            }

            if ((requiredHeight / tbAssigning.DesiredSize.Height) > 1.5)
            {
                tbAssigning.FontSize *= 1.1;
            }
        }

        // вернуть кисть из ресурса приложения
        // если ключа нет, то в зависимости от параметра isDefaultWhite возвращается белая (для фона) или черная (для текста) кисть
        public static Brush GetAppResourcesBrush(string resKey, bool isDefaultWhite = true)
        {
            ResourceDictionary resDict = App.Current.Resources;
            object resVal = resDict[resKey];

            Brush retVal;
            if (!(resVal is Brush) || (resVal == null))
                retVal = new SolidColorBrush(isDefaultWhite ? Colors.White : Colors.Black);
            else
                retVal = (Brush)resVal;

            return retVal;
        }

        public static bool IsOpenWindow(string typeName, string objName = null)
        {
            bool retVal = false;
            foreach (Window win in App.Current.Windows)
            {
                if ((win.GetType().Name.Equals(typeName)) && (string.IsNullOrEmpty(objName) ? true : win.Name.Equals(objName)))
                {
                    retVal = (win.Visibility == Visibility.Visible);
                    break;
                }
            }

            return retVal;
        }

        // закрыть все открытые окна, кроме главного окна
        // проще перечислить, какие надо закрывать, а какие прятать
        public static void CloseChildWindows()
        {
            foreach (Window win in App.Current.Windows)
            {
                Type winType = win.GetType();
                if (winType.Name == "MainWindow") continue;
                PropertyInfo pInfo = winType.GetProperty("Host");
                if (pInfo == null) win.Close();
            }  // for each
        }  // method

        public static Brush GetBrushFromRGBString(string rgb)
        {
            string[] sArr = rgb.Split(',');
            if (sArr.Count() != 3) return new SolidColorBrush(Color.FromRgb(0, 0, 0));

            byte r = 0, g = 0, b = 0;
            byte.TryParse(sArr[0], out r);
            byte.TryParse(sArr[1], out g);
            byte.TryParse(sArr[2], out b);

            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        #endregion


        //  ДЛЯ КОНКРЕТНОГО ПРИЛОЖЕНИЯ

        // преобразовать TimeSpan в строку
        public static string GetAppStringTS(TimeSpan tsTimerValue)
        {
            string retVal = "";

            if (tsTimerValue != TimeSpan.Zero)
            {
                retVal = (tsTimerValue.Days > 0d) ? tsTimerValue.ToString(@"d\.hh\:mm\:ss") : tsTimerValue.ToString(@"hh\:mm\:ss");
                // отрицательное время
                if (tsTimerValue.Ticks < 0) retVal = "-" + retVal;
            }

            return retVal;
        }
        // преобразовать строку в TimeSpan
        internal static TimeSpan GetTSFromString(string tsString)
        {
            TimeSpan ts = TimeSpan.Zero;
            TimeSpan.TryParse(tsString, out ts);
            return ts;
        }

        // установить размер окна (параметр) в размеры главного окна приложения
        internal static void SetWinSizeToMainWinSize(Window win)
        {
            Window mWin = Application.Current.MainWindow;
            // размеры
            if (win.Width != mWin.ActualWidth) win.Width = mWin.ActualWidth;
            if (win.Height != mWin.ActualHeight) win.Height = mWin.ActualHeight;

            // положение
            Point topLeftPoint = AppLib.GetWindowTopLeftPoint(mWin);
            if (win.Top != topLeftPoint.Y) win.Top = topLeftPoint.Y;
            if (win.Left != topLeftPoint.X) win.Left = topLeftPoint.X;
        }

        // ****  РАСЧЕТ РАЗМЕЩЕНИЯ ПАНЕЛЕЙ ЗАКАЗОВ
        internal static void RecalcOrderPanelsLayot()
        {
            string cfgValue;
            int cntCols;
            // размеры элементов панели заказа
            //   кол-во столбцов заказов, если нет в config-е, то сохранить значение по умолчанию
            cfgValue = AppLib.GetAppSetting("OrdersColumnsCount");
            if (cfgValue == null)
            {
                cntCols = 4;   // по умолчанию - 4
                string errMsg;
                AppLib.SaveAppSettings(new Dictionary<string, string>() { { "OrdersColumnsCount", cntCols.ToString() } }, out errMsg);
            }
            else cntCols = cfgValue.ToInt();

            //   ширина столбцов заказов и расстояния между столбцами
            double screenWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
            // wScr = wCol*cntCols + koef*wCol*(cntCols+1) ==> wCol = wScr / (cntCols + koef*(cntCols+1))
            // где, koef = доля поля от ширины колонки
            double koef = 0.15d;
            double colWidth = Math.Floor(screenWidth / (cntCols + koef * (cntCols + 1)));
            double colMargin = Math.Floor(koef * colWidth);  // поле между заказами по горизонтали
            AppLib.SetAppGlobalValue("OrdersColumnWidth", colWidth);
            AppLib.SetAppGlobalValue("OrdersColumnMargin", colMargin);

            //   отступ сверху/снизу для панели заказов
            AppLib.SetAppGlobalValue("dishesPanelTopBotMargin", 20d);
            //   отступ между заказами по вертикали
            AppLib.SetAppGlobalValue("ordPnlTopMargin", 1.5d * colMargin);
        }


        /// <summary>
        /// соединение двух ОТСОРТИРОВАННЫХ массивов
        ///  target - что обновляем (цель/получатель обновления), напр. List<OrderDishViewModel>
        ///  source - чем обновляем (источник/поставщик обновления), напр. List<OrderDishModel> 
        /// </summary>
        /// <typeparam name="T1">Получатель данных, напр. view-объект</typeparam>
        /// <typeparam name="T2">Источник данных, напр. service-объект</typeparam>
        /// <param name="targetList">Список объктов-получателей</param>
        /// <param name="sourceList">Список объектов-источников</param>
        /// <returns></returns>
        internal static bool JoinSortedLists<T1, T2>(List<T1> targetList, List<T2> sourceList) 
            where T1:IJoinSortedCollection<T2>, new() where T2: IContainIDField
        {
            bool retVal = false;
            int index = 0;
            T1 trgObj;
            // в цикле по объектам источника просматриваем целевой список в ТАКОМ ЖЕ ПОРЯДКЕ, сравнивая Ид
            foreach (T2 srcObj in sourceList)
            {
                // в источнике больше элементов, поэтому добавляем в цель
                if (index == targetList.Count)
                {
                    trgObj = new T1();
                    trgObj.FillDataFromServiceObject(srcObj, index + 1);
                    targetList.Add(trgObj);
                    retVal = true;
                }
                // если одинаковые идентификаторы, то просто обновляем целевой объект из источника
                else if (targetList[index].Id == srcObj.Id)
                {
                    trgObj = targetList[index];
                    trgObj.UpdateFromSvc(srcObj);
                    if ((trgObj is IContainInnerCollection) && ((trgObj as IContainInnerCollection).IsInnerListUpdated) && !retVal) retVal = true;
                }
                else
                {
                    // попытаться найти блюдо с таким Ид и переставить его в нужную позицию
                    trgObj = targetList.FirstOrDefault(d => d.Id == srcObj.Id);
                    if (trgObj == null)  // не найдено - ВСТАВЛЯЕМ в нужную позицию
                    {
                        trgObj = new T1();
                        trgObj.FillDataFromServiceObject(srcObj, index + 1);
                        targetList.Insert(index, trgObj);
                        retVal = true;
                    }
                    else  // переставляем и обновляем из источника
                    {
                        targetList.Remove(trgObj);
                        targetList.Insert(index, trgObj);
                        trgObj.UpdateFromSvc(srcObj);
                        if ((trgObj is IContainInnerCollection) && ((trgObj as IContainInnerCollection).IsInnerListUpdated) && !retVal) retVal = true;
                    }
                }
                index++;
            }

            // удалить блюда, которые не пришли от службы
            while (targetList.Count >= (index + 1))
            {
                targetList.RemoveAt(targetList.Count - 1);
                if (!retVal) retVal = true;
            }

            return retVal;
        }  // method

        // узнать, в каком состоянии находятся ВСЕ БЛЮДА заказа
        public static OrderStatusEnum GetStatusAllDishes(List<OrderDishViewModel> dishes)
        {
            OrderStatusEnum retVal = OrderStatusEnum.None;

            int iLen = Enum.GetValues(typeof(OrderStatusEnum)).Length;
            int dishCount = dishes.Count;

            int[] statArray = new int[iLen];

            int iStatus;
            foreach (OrderDishViewModel modelDish in dishes)
            {
                iStatus = modelDish.DishStatusId;
                statArray[iStatus]++;
            }

            for (int i = 0; i < iLen; i++)
            {
                if (statArray[i] == dishCount) { retVal = (OrderStatusEnum)i;break; }
            }

            return retVal;
        }

        // принадлежит ли переданный Ид цеха разрешенным цехам на этом КДСе
        internal static bool IsDepViewOnKDS(int depId, AppDataProvider dataProvider = null)
        {
            if (dataProvider == null) dataProvider = (AppDataProvider)AppLib.GetAppGlobalValue("AppDataProvider");

            return dataProvider.Departments[depId].IsViewOnKDS;
        }

    }  // class
}
