using ClientOrderQueue.Lib;
using ClientOrderQueue.Model;
using IntegraLib;
using IntegraWPFLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClientOrderQueue.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // текст запроса по умолчанию
        // поля запроса должны соответствовать полям класса Order из EF
        private const string _readOrdersSQLTextDefault = "SELECT Id, Number, QueueStatusId, LanguageTypeId, CreateDate, ClientName FROM dbo.[Order] WHERE(QueueStatusId IN (0, 1)) AND (OrderStatusId IN (1, 2, 8))";

        private List<AppOrder> _appOrders;

        private System.Media.SoundPlayer simpleSound = null;
        private System.Timers.Timer _updateTimer;

        private Brush[] _cellBrushes;
        private bool _isShowClientName, _isShowCookingTime;
        private double _cookingEstMinutes;
        private HashSet<int> _unUsedDeps;

        private string _readOrdersSQLText;
        private DateTime _currentDate;
        private double _hoursBeforeMidnight = 0d;

        public MainWindow()
        {
            InitializeComponent();

            _appOrders = new List<AppOrder>();
            _unUsedDeps = (HashSet<int>)WpfHelper.GetAppGlobalValue("UnusedDepartments");

            // кисти заголовка окна
            brdTitle.Background = (SolidColorBrush)WpfHelper.GetAppGlobalValue("WinTitleBackground");
            tbMainTitle.Foreground = (SolidColorBrush)WpfHelper.GetAppGlobalValue("WinTitleForeground");

            // кисти для панелей заказов
            _cellBrushes = (Brush[])WpfHelper.GetAppGlobalValue("PanelBackgroundBrushes");

            string statusReadyAudioFile = AppEnvironment.GetFullFileName(CfgFileHelper.GetAppSetting("AudioPath"), CfgFileHelper.GetAppSetting("StatusReadyAudioFile"));
            if (System.IO.File.Exists(statusReadyAudioFile))
            {
                simpleSound = new System.Media.SoundPlayer(statusReadyAudioFile);
            }

            _isShowClientName = (bool)WpfHelper.GetAppGlobalValue("IsShowClientName");
            _isShowCookingTime = (bool)WpfHelper.GetAppGlobalValue("IsShowOrderEstimateTime");
            _cookingEstMinutes = (double)WpfHelper.GetAppGlobalValue("OrderEstimateTime", 0d);

            this.Loaded += MainWindow_Loaded;
            setAppLayout();

            _updateTimer = new System.Timers.Timer(1000d);
            _updateTimer.AutoReset = true;
            _updateTimer.Elapsed += updateTimer_Tick;
            _updateTimer.Start();

            _currentDate = DateTime.Now.Date;
            _hoursBeforeMidnight = Convert.ToDouble(WpfHelper.GetAppGlobalValue("MidnightShiftShowYesterdayOrders", 0d));
            _readOrdersSQLText = getReadOrdersSQLText();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SplashScreenLib.Splasher.CloseSplash();

            setLayoutAfterLoaded(); // Logo image

            createGridContainers(G15);
            createGridContainers(G24);
        }


        private void createGridContainers(Grid grid)
        {
            Size mainGridSize = getMainGridSize();
            int rowsCount = grid.RowDefinitions.Count, colsCount = grid.ColumnDefinitions.Count;

            string stateReadyImageFile = (string)WpfHelper.GetAppGlobalValue("StatusReadyImageFile");

            double cellWidth = mainGridSize.Width / (double)colsCount, 
                cellHeight = mainGridSize.Height / (double)rowsCount;

            for (int i = 0; i < rowsCount; i++)
                for (int j = 0; j < colsCount; j++)
                {
                    // постоянные свойства панели
                    OrderPanel1 cc = new OrderPanel1()
                    {
                        Visibility = Visibility.Hidden,
                        BackBrushes = _cellBrushes,
                        MarginKoefStr = (string)WpfHelper.GetAppGlobalValue("MarginKoefStr"),
                        IsShowClientName = _isShowClientName,
                        IsShowCookingTime = _isShowCookingTime,

                        TitleLangs = (string)WpfHelper.GetAppGlobalValue("StatusTitle"),
                        CookingTimeTitleLangs = (string)WpfHelper.GetAppGlobalValue("PanelWaitText"),
                        Status1Langs = (string)WpfHelper.GetAppGlobalValue("Status1Langs"),
                        Status2Langs = (string)WpfHelper.GetAppGlobalValue("Status2Langs"),
                        Status3Langs = (string)WpfHelper.GetAppGlobalValue("Status3Langs")
                    };
                    if (stateReadyImageFile != null) cc.StateReadyImagePath = stateReadyImageFile;

                    //CellContainer cc = new CellContainer(cellWidth, cellHeight)
                    //{
                    //    PanelBrushes = _cellBrushes,
                    //    TitleLangs = (string[])AppLib.GetAppGlobalValue("StatusTitle"),
                    //    StatusLangs = (string[][])AppLib.GetAppGlobalValue("StatusLang"),
                    //    IsShowOrderEstimateTime = (bool)AppLib.GetAppGlobalValue("IsShowOrderEstimateTime"),
                    //    IsShowClientName = (bool)AppLib.GetAppGlobalValue("IsShowClientName"),
                    //    OrderNumberFontSize = (double)AppLib.GetAppGlobalValue("OrderNumberFontSize", 0),
                    //    StatusReadyImageFile = (string)AppLib.GetAppGlobalValue("StatusReadyImageFile")
                    //};

                    Grid.SetRow(cc, i); Grid.SetColumn(cc, j);
                    grid.Children.Add(cc);
                }

        }  // method

        #region update timer

        private void updateTimer_Tick(object sender, System.Timers.ElapsedEventArgs e)
        {
            _updateTimer.Stop();
#if DEBUG
            DateTime dt1 = DateTime.Now;
#endif
            this.Dispatcher.Invoke(loadItems);

#if DEBUG
            System.Diagnostics.Debug.Print("-- " + DateTime.Now.Subtract(dt1).TotalMilliseconds);
#endif
            _updateTimer.Start();
        }

        private void loadItems()
        {
            List<Order> orders = getOrders();

            if ((orders == null) || (orders.Count == 0))
            {
                hideCells(G15); hideCells(G24);
                return;
            }

            if (updateAppOrders(orders))
            {
                if (orders.Count <= 15)
                {
                    fillCells(G15);
                    setGridVisibility(G24, Visibility.Collapsed);
                    setGridVisibility(G15, Visibility.Visible);
                }
                else
                {
                    fillCells(G24);
                    setGridVisibility(G15, Visibility.Collapsed);
                    setGridVisibility(G24, Visibility.Visible);
                }
            }
        }

        // возвращает признак того, что были сделаны какие-то изменения во внутренней коллекции заказов
        private bool updateAppOrders(List<Order> orders)
        {
            bool retVal = false;
            // признак того, что появился заказ в статусе ГОТОВ и надо проиграть мелодию
            bool isCooked = false;

            int[] dbIds = orders.Select(o => o.Id).ToArray();
            // удалить выданные заказы
            int[] delIds = _appOrders.Select(o => o.Id).Except(dbIds).ToArray();
            if (delIds.Length > 0)
            {
                foreach (int item in delIds) _appOrders.RemoveAll(o => o.Id == item);
                AppLib.WriteLogTraceMessage("- app-orders remove(ids): " + string.Join(",", delIds));
                retVal = true;
            }

            // добавить новые
            double estDT = (double)WpfHelper.GetAppGlobalValue("OrderEstimateTime", 0d);
            string ordersAdded = null;
            AppOrder curAppOrd;
            foreach (Order dbOrd in orders)
            {
                curAppOrd = _appOrders.FirstOrDefault(ao => (ao.Id == dbOrd.Id));
                // add new
                if (curAppOrd == null)
                {
                    AppOrder newAppOrder = new AppOrder() { Id = dbOrd.Id, Order = dbOrd };
                    // удалить граничные пробелы из имени клиента
                    if (!newAppOrder.Order.ClientName.IsNull()) newAppOrder.Order.ClientName = newAppOrder.Order.ClientName.Trim();
                    //newAppOrder.IsExistOrderEstimateDT = (estDT != 0d);
                    //newAppOrder.OrderCookingBaseDT = (estDT == 0d) ? dbOrd.CreateDate : dbOrd.CreateDate.AddMinutes(estDT);
                    // новый заказ сразу в статусе ГОТОВ
                    if (dbOrd.QueueStatusId == 1) isCooked = true;

                    _appOrders.Add(newAppOrder);
                    if (ordersAdded == null) ordersAdded = dbOrd.Id.ToString(); else ordersAdded += "," + dbOrd.Id.ToString();
                    retVal = true;
                }

                // update exists
                else
                {
                    // заказ поменял статус
                    if (curAppOrd.Order.QueueStatusId != dbOrd.QueueStatusId)
                    {
                        if (dbOrd.QueueStatusId == 1) isCooked = true; // ready
                        // изменения в лог
                        AppLib.WriteLogTraceMessage("- app-order id {0} changing its state: {1}({2}) to {3}({4})", 
                            dbOrd.Id, curAppOrd.Order.QueueStatusId, (OrderStatusEnum)curAppOrd.Order.QueueStatusId,
                            dbOrd.QueueStatusId, (OrderStatusEnum)dbOrd.QueueStatusId);
                        retVal = true;
                    }

                    curAppOrd.Order = dbOrd;
                }
            }

            // в лог новые заказы
            if (ordersAdded != null) AppLib.WriteLogTraceMessage("- app-orders add(ids): " + ordersAdded);
            // проиграть мелодию
            if ((isCooked) && (simpleSound != null)) simpleSound.Play();

            // если есть изменения
            if (retVal)
            {
                // сортировка orderby o.CreateDate ascending, o.Number ascending
                _appOrders = _appOrders.OrderBy(o => o.Order.CreateDate).ThenBy(o => o.Order.Number).ToList();
            }

            return retVal;
        }

        private void hideCells(Grid grid)
        {
            //CellContainer cc;
            OrderPanel1 cc;
            for (int i = 0; i < grid.Children.Count; i++)
            {
                //cc = (CellContainer)G15.Children[i];
                //if (cc.CellVisible) cc.Clear();
                cc = (OrderPanel1)G15.Children[i];
                if (cc.Visibility == Visibility.Visible) cc.Visibility = Visibility.Hidden;
                else break;
            }
        }

        private void fillCells(Grid grid)
        {
            int rowCount = grid.RowDefinitions.Count;
            int colCount = grid.ColumnDefinitions.Count;
            int listIndex;

            DateTime dtProc = DateTime.Now;
            AppLib.WriteLogTraceMessage("screen updating - START");

            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < colCount; j++)
                {
                    // индекс ячейки в одномерном массиве
                    listIndex = (i * colCount) + j;
                    // ячейка таблицы (объект типа OrderPanel1)
                    OrderPanel1 cc = (OrderPanel1)grid.Children[listIndex];
                    // обновить ячейки данными из набора _appOrders
                    if (listIndex < _appOrders.Count)
                    {
                        cc.OrderNumber = _appOrders[listIndex].Order.Number.ToString();
                        cc.OrderStatus = _appOrders[listIndex].Order.QueueStatusId + 1;
                        cc.OrderLang = _appOrders[listIndex].Order.LanguageTypeId;
                        if (_isShowClientName)
                        {
                            cc.ClientName = (_appOrders[listIndex].Order.ClientName.IsNull()) ? null :_appOrders[listIndex].Order.ClientName.Trim();
                        }
                        if (_isShowCookingTime)
                        {
                            cc.OrderCreateDate = _appOrders[listIndex].Order.CreateDate;
                            cc.CookingEstMinutes = _cookingEstMinutes;
                        }
                        if (cc.Visibility != Visibility.Visible) cc.Visibility = Visibility.Visible;
                    }
                    // прочие ячейки таблицы
                    else
                    {
                        // если панель заказа в ячейке видима, то скрыть ее
                        if (cc.Visibility == Visibility.Visible) cc.Visibility = Visibility.Hidden;
                    }
                }
            }
            AppLib.WriteLogTraceMessage("screen updating - FINISH, " + getFormattedTS(dtProc));
        }

        private void setGridVisibility(Grid grid, Visibility visi)
        {
            if (grid.Visibility != visi) grid.Visibility = visi;
        }

        private List<Order> getOrders()
        {
            DateTime dtProc = DateTime.Now;
            string logMsg = "get DB-orders - ", errMsg= null;
            AppLib.WriteLogTraceMessage(logMsg + "START");

            // сформировать sql-запрос к БД
            if (DateTime.Now.Date != _currentDate)
            {
                _currentDate = DateTime.Now.Date;
                _readOrdersSQLText = getReadOrdersSQLText();
            }

            List <Order> retVal = null;
            try
            {
                using (KDSContext db = new KDSContext())
                {
                    db.Database.Connection.Open();
                    if (db.Database.Connection.State == System.Data.ConnectionState.Open)
                    {
                        List<Order> dbOrders = db.Database.SqlQuery<Order>(_readOrdersSQLText).ToList();
                        //List<Order> dbOrders = db.vwOrderQueue.ToList();

                        // пройтись по всем заказам
                        int iCnt;
                        Dictionary<int, IntWrapper> statesCnt = new Dictionary<int, IntWrapper>();
                        List<Order> delOrd = new List<Order>();
                        foreach (Order order in dbOrders)
                        {
                            foreach (int key in statesCnt.Keys) statesCnt[key].IWValue = 0;
                            // достать из БД блюда и удалить из них позиции для неиспользуемых цехов
                            var dishes = db.Database.SqlQuery<OrderDish>("SELECT Id, DishStatusId, DepartmentId FROM OrderDish WHERE OrderId = " + order.Id.ToString(), order.Id).ToList();
                            iCnt = dishes.Count;
                            foreach (var dish in dishes)
                            {
                                if (_unUsedDeps.Contains(dish.DepartmentId))
                                    iCnt--;
                                else
                                {
                                    if (!statesCnt.ContainsKey(dish.DishStatusId)) statesCnt.Add(dish.DishStatusId, new IntWrapper());
                                    statesCnt[dish.DishStatusId].IWValue += 1;
                                }
                            }
                            if (iCnt == 0) delOrd.Add(order);
                            // узнать общий статус оставшихся блюд
                            //foreach (var key in statesCnt.Keys)
                            //{
                            //    int queueStat = key - 1;
                            //    if ((statesCnt[key].IWValue == iCnt) && (queueStat >= 0) && (order.QueueStatusId != queueStat))
                            //        order.QueueStatusId = queueStat;
                            //}
                        }
                        foreach (Order item in delOrd) dbOrders.Remove(item);

                        retVal = dbOrders;
                    }
                }
            }
            catch (Exception ex)
            {
                errMsg = ex.Message;
                if (ex.InnerException != null) errMsg += "(inner message: " + ex.InnerException.Message + ")";
            }

            logMsg += "FINISH, " + getFormattedTS(dtProc);

            if (errMsg == null)
            {
                logMsg += $", orders count {retVal.Count}";
                if ((retVal != null) && (retVal.Count > 0)) logMsg += "(ids " + string.Join(",", retVal.Select(o => o.Id.ToString())) + ")";
                AppLib.WriteLogTraceMessage(logMsg);
            }
            else
            {
                AppLib.WriteLogErrorMessage(logMsg + " - Error: " + errMsg);
            }

            return retVal;
        }

        private string getReadOrdersSQLText()
        {
            DateTime dateFrom = _currentDate.AddHours(-_hoursBeforeMidnight);
            string retVal = _readOrdersSQLTextDefault + string.Format(" AND (CreateDate >= {0})", dateFrom.ToSQLExpr());

            return retVal;
        }

        #endregion

        private string getFormattedTS(DateTime initDT)
        {
            TimeSpan ts = (DateTime.Now - initDT);

            return ((ts.TotalMilliseconds >= 0.01) 
                ? ts.ToString("hh\\:mm\\:ss\\.fffffff") 
                : "< 0.01 msec");
        }

    #region set elements

    private void setAppLayout()
        {
            string bgImageFile = CfgFileHelper.GetAppSetting("ImagesPath");

            if (WpfHelper.IsAppVerticalLayout)
            {
                bgImageFile = AppEnvironment.GetFullFileName(bgImageFile, "bg 3ver 1080x1920 background.png");
                // пересоздать кол-во строк и столбцов
                transposeGrid(G15);
                transposeGrid(G24);
            }
            else
            {
                bgImageFile = AppEnvironment.GetFullFileName(bgImageFile, "bg 3hor 1920x1080 background.png");
            }

            setBackgroundImage(bgImageFile); // фон
        }

        private void transposeGrid(Grid grid)
        {
            int newColCount = grid.RowDefinitions.Count - 1;
            int newRowCount = grid.ColumnDefinitions.Count + 1;

            grid.RowDefinitions.Clear(); grid.ColumnDefinitions.Clear();

            for (int i = 0; i < newRowCount; i++) grid.RowDefinitions.Add(new RowDefinition());
            for (int i = 0; i < newColCount; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        private void setLayoutAfterLoaded()
        {
            // main title
            double fontSize = ((WpfHelper.IsAppVerticalLayout) ? 0.4 : 0.5) * brdTitle.ActualHeight;
            tbMainTitle.FontSize = fontSize;

            // logo image
            string logoFile = CfgFileHelper.GetAppSetting("LogoImage");
            if (logoFile != null)
            {
                logoFile = AppEnvironment.GetFullFileName((string)WpfHelper.GetAppGlobalValue("ImagesPath", ""), logoFile);
                double d1 = 0.15 * brdTitle.ActualHeight;
                imgLogo.Source = ImageHelper.GetBitmapImage(logoFile);
                imgLogo.Margin = new Thickness(0,d1,0,d1);
            }
        }

        private void setBackgroundImage(string bgImageFile)
        {
            // фон
            backgroundImage.Source = ImageHelper.GetBitmapImage(bgImageFile);
            // яркость фона
            string opacity = CfgFileHelper.GetAppSetting("MenuBackgroundBrightness");
            if (opacity != null)
            {
                backgroundImage.Opacity = opacity.ToDouble();
            }
        }

        private Size getMainGridSize()
        {
            double gridW=0, gridH=0;
            gridW = (double)WpfHelper.GetAppGlobalValue("screenWidth");
            gridH = WpfHelper.GetRowHeightAbsValue(mainGrid, 1, (double)WpfHelper.GetAppGlobalValue("screenHeight"));

            return new Size(gridW, gridH);
        }

        #endregion

        private class OrderDish
        {
            public int Id { get; set; }
            public int DishStatusId { get; set; }
            public int DepartmentId { get; set; }
        }

        private class IntWrapper
        {
            public int IWKey { get; set; }
            public int IWValue { get; set; }

            public IntWrapper()
            {
                IWKey = 0; IWValue = 0;
            }
        }

    }  // class

}
