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
using System.Data.SqlClient;
using KDSWPFClient.ServiceReference1;
using System.Xml.Linq;
using System.Windows.Media;
using KDSWPFClient.ViewModel;
using KDSWPFClient.View;
using System.Reflection;
using IntegraLib;

namespace KDSWPFClient.Lib
{

    public static class AppLib
    {
        // общий логгер
        public static NLog.Logger _appLogger;

        static AppLib()
        {
        }

        public static void InitAppLogger()
        {
            // логгер приложения
            _appLogger = NLog.LogManager.GetLogger("appLogger");
        }

        #region app logger
        // отладочные сообщения
        // стандартные действия службы
        public static void WriteLogTraceMessage(string msg)
        {
            if ((bool)AppPropsHelper.GetAppGlobalValue("IsWriteTraceMessages", false) && !msg.IsNull()) _appLogger.Trace(msg);
        }
        public static void WriteLogTraceMessage(string format, params object[] args)
        {
            if ((bool)AppPropsHelper.GetAppGlobalValue("IsWriteTraceMessages", false)) _appLogger.Trace(format, args);
        }

        // подробная информация о преобразованиях списка заказов, полученных клиентом от службы
        public static void WriteLogOrderDetails(string msg)
        {
            if ((bool)AppPropsHelper.GetAppGlobalValue("IsWriteTraceMessages", false) 
                && (bool)AppPropsHelper.GetAppGlobalValue("TraceOrdersDetails", false))
                _appLogger.Trace(msg);
        }
        public static void WriteLogOrderDetails(string format, params object[] paramArray)
        {
            if ((bool)AppPropsHelper.GetAppGlobalValue("IsWriteTraceMessages", false)
                && (bool)AppPropsHelper.GetAppGlobalValue("TraceOrdersDetails", false))
            {
                string msg = string.Format(format, paramArray);
                _appLogger.Trace(msg);
            }
        }

        // сообщения о действиях клиента
        public static void WriteLogClientAction(string msg)
        {
            if ((bool)AppPropsHelper.GetAppGlobalValue("IsLogClientAction", false))
                _appLogger.Trace("cltAct|" + msg);
        }
        public static void WriteLogClientAction(string format, params object[] paramArray)
        {
            if ((bool)AppPropsHelper.GetAppGlobalValue("IsLogClientAction", false))
                _appLogger.Trace("cltAct|" + string.Format(format, paramArray));
        }

        public static void WriteLogInfoMessage(string msg)
        {
            if (_appLogger.IsInfoEnabled && !msg.IsNull()) _appLogger.Info(msg);
        }
        public static void WriteLogInfoMessage(string format, params object[] args)
        {
            if (_appLogger.IsInfoEnabled) _appLogger.Info(format, args);
        }

        public static void WriteLogErrorMessage(string msg)
        {
            if (_appLogger.IsErrorEnabled && !msg.IsNull()) _appLogger.Error(msg);
        }
        public static void WriteLogErrorMessage(string format, params object[] args)
        {
            if (_appLogger.IsErrorEnabled) _appLogger.Error(format, args);
        }

        #endregion

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


        public static bool SeeHardware(string fileName, string cpu)
        {
            if (fileName.IsNull()) return false;

            XElement doc = getInitFileXML(fileName);
            if (doc == null) return false;

            string proccessors = doc.Descendants("Cpu").Attributes("Key").First<XAttribute>().Value;

            return (proccessors == cpu);
        }


        private static XElement getInitFileXML(string fileName)
        {
            string result = Password.DecryptFileToString(fileName);

            if ((result == null) || (result.StartsWith("ERROR")))
            {
                AppLib.WriteLogErrorMessage(result);
                return null;
            }

            XElement retVal = null;
            try
            {
                retVal = XElement.Parse(result);
            }
            catch (Exception)
            {
            }

            return retVal;
        }

        internal static string GetShortErrMessage(Exception ex)
        {
            string retVal = ex.Message;
            if (ex.InnerException != null) retVal += " Inner exception: " + ex.InnerException.Message;
            return retVal;
        }

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
                double appWidth = (double)AppPropsHelper.GetAppGlobalValue("screenWidth");
                double appHeight = (double)AppPropsHelper.GetAppGlobalValue("screenHeight");
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
            if ((dishes == null) || (dishes.Count == 0)) return retVal;

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

        // узнать, в каком состоянии находятся ВСЕ БЛЮДА заказа отображаемых на данном КДСе цехов
        public static StatusEnum GetStatusAllDishesOwnDeps(List<OrderDishViewModel> dishes)
        {
            if ((dishes == null) || (dishes.Count == 0)) return StatusEnum.None;

            int statId = -1;
            AppDataProvider dataProvider = (AppDataProvider)AppPropsHelper.GetAppGlobalValue("AppDataProvider");
            foreach (OrderDishViewModel modelDish in dishes)
            {
                if (dataProvider.Departments[modelDish.DepartmentId].IsViewOnKDS)
                {
                    if (statId == -1) statId = modelDish.DishStatusId;
                    else if (statId != modelDish.DishStatusId) return StatusEnum.None;
                }
            }

            return (StatusEnum)statId;
        }

        // принадлежит ли переданный Ид цеха разрешенным цехам на этом КДСе
        internal static bool IsDepViewOnKDS(int depId, AppDataProvider dataProvider = null)
        {
            if (dataProvider == null) dataProvider = (AppDataProvider)AppPropsHelper.GetAppGlobalValue("AppDataProvider");

            return dataProvider.Departments[depId].IsViewOnKDS;
        }

    }  // class
}
