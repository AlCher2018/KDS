using ClientOrderQueue.Lib;
using ClientOrderQueue.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClientOrderQueue
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<AppOrder> _appOrders;

        private System.Media.SoundPlayer simpleSound = null;
        private System.Timers.Timer _updateTimer;

        private Brush[] _cellBrushes;

        private HashSet<int> _unUsedDeps;

        public MainWindow()
        {
            InitializeComponent();

            _appOrders = new List<AppOrder>();
            _unUsedDeps = (HashSet<int>)AppLib.GetAppGlobalValue("UnusedDepartments");

            // кисти заголовка окна
            brdTitle.Background = (SolidColorBrush)AppLib.GetAppGlobalValue("WinTitleBackground");
            tbMainTitle.Foreground = (SolidColorBrush)AppLib.GetAppGlobalValue("WinTitleForeground");

            // кисти для панелей заказов
            _cellBrushes = (Brush[])AppLib.GetAppGlobalValue("PanelBackgroundBrushes");

            string statusReadyAudioFile = AppLib.GetFullFileName(AppLib.GetAppSetting("AudioPath"), AppLib.GetAppSetting("StatusReadyAudioFile"));
            if (System.IO.File.Exists(statusReadyAudioFile))
            {
                simpleSound = new System.Media.SoundPlayer(statusReadyAudioFile);
            }

            this.Loaded += MainWindow_Loaded;
            setAppLayout();

            _updateTimer = new System.Timers.Timer();
            _updateTimer.Elapsed += updateTimer_Tick;
            _updateTimer.Interval = 1000d;
            _updateTimer.Start();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            setLayoutAfterLoaded(); // Logo image

            createGridContainers(G15);
            createGridContainers(G24);
        }


        private void createGridContainers(Grid grid)
        {
            Size mainGridSize = getMainGridSize();
            int rowsCount = grid.RowDefinitions.Count, colsCount = grid.ColumnDefinitions.Count;

            double cellWidth = mainGridSize.Width / (double)colsCount, 
                cellHeight = mainGridSize.Height / (double)rowsCount;

            for (int i = 0; i < rowsCount; i++)
                for (int j = 0; j < colsCount; j++)
                {
                    CellContainer cc = new CellContainer(cellWidth, cellHeight)
                    {
                        PanelBrushes = _cellBrushes,
                        TitleLangs = (string[])AppLib.GetAppGlobalValue("StatusTitle"),
                        StatusLangs = (string[][])AppLib.GetAppGlobalValue("StatusLang"),
                        IsShowOrderEstimateTime = (bool)AppLib.GetAppGlobalValue("IsShowOrderEstimateTime"),
                        IsShowClientName = (bool)AppLib.GetAppGlobalValue("IsShowClientName"),
                        OrderNumberFontSize = (double)AppLib.GetAppGlobalValue("OrderNumberFontSize", 0),
                        StatusReadyImageFile = (string)AppLib.GetAppGlobalValue("StatusReadyImageFile")
                    };

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

            updateAppOrders(orders);

            if (orders.Count <= 15)
            {
                fillCells(G15);
                setGridVisibility(G24, Visibility.Collapsed);
                setGridVisibility(G15 , Visibility.Visible);
            }
            else
            {
                fillCells(G24);
                setGridVisibility(G15, Visibility.Collapsed);
                setGridVisibility(G24, Visibility.Visible);
            }
        }

        private void updateAppOrders(List<Order> orders)
        {
            // признак того, что появился заказ в статусе ГОТОВ и надо проиграть мелодию
            bool isCooked = false;  

            int[] dbIds = orders.Select(o => o.Id).ToArray();
            // удалить выданные заказы
            int[] delIds = _appOrders.Select(o => o.Id).Except(dbIds).ToArray();
            _appOrders.RemoveAll(o => delIds.Contains(o.Id));
            // добавить новые
            double estDT = (double)AppLib.GetAppGlobalValue("OrderEstimateTime", 0d);
            AppOrder curAppOrd;
            foreach (Order dbOrd in orders)
            {
                curAppOrd = _appOrders.FirstOrDefault(ao => (ao.Id == dbOrd.Id));
                if (curAppOrd == null)
                {
                    AppOrder newAppOrder = new AppOrder() { Id = dbOrd.Id, Order = dbOrd };
                    newAppOrder.IsExistOrderEstimateDT = (estDT != 0d);
                    newAppOrder.OrderCookingBaseDT = (estDT == 0d) ? dbOrd.CreateDate : dbOrd.CreateDate.AddMinutes(estDT);
                    // новый заказ сразу в статусе ГОТОВ
                    if (dbOrd.QueueStatusId == 1) isCooked = true;

                    _appOrders.Add(newAppOrder);
                }
                else
                {
                    // заказ поменял статус
                    if ((dbOrd.QueueStatusId == 1) && (curAppOrd.Order.QueueStatusId != dbOrd.QueueStatusId)) isCooked = true;
                    curAppOrd.Order = dbOrd;
                }
            }

            if ((isCooked) && (simpleSound != null)) simpleSound.Play();

            // сортировка orderby o.CreateDate ascending, o.Number ascending
            _appOrders = _appOrders.OrderBy(o => o.Order.CreateDate).ThenBy(o => o.Order.Number).ToList();
        }

        private void hideCells(Grid grid)
        {
            CellContainer cc;
            for (int i = 0; i < grid.Children.Count; i++)
            {
                cc = (CellContainer)G15.Children[i];
                if (cc.CellVisible) cc.Clear();
                else break;
            }
        }

        private void fillCells(Grid grid)
        {
            int rowCount = grid.RowDefinitions.Count;
            int colCount = grid.ColumnDefinitions.Count;
            int listIndex;
            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < colCount; j++)
                {
                    listIndex = (i * colCount) + j;
                    CellContainer cc = (CellContainer)grid.Children[listIndex];
                    if (listIndex < _appOrders.Count)
                    {
                        cc.SetOrderData(_appOrders[listIndex]);
                    }
                    else if (cc.AppOrder != null) cc.Clear();
                }
            }
        }

        private void setGridVisibility(Grid grid, Visibility visi)
        {
            if (grid.Visibility != visi) grid.Visibility = visi;
        }

        private List<Order> getOrders()
        {
            List<Order> retVal = null;
            try
            {
                AppLib.WriteLogTraceMessage("Получаю заказы для очереди...");
                using (KDSContext db = new KDSContext())
                {
                    db.Database.Connection.Open();
                    if (db.Database.Connection.State == System.Data.ConnectionState.Open)
                    {
                        List<Order> dbOrders = db.vwOrderQueue.ToList();

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

                string msg = string.Format("  - получено {0} заказов", retVal.Count);
                if (retVal.Count > 0)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    foreach (Order item in retVal)
                    {
                        if (sb.Length > 0) sb.Append(",");
                        sb.Append(item.Id);
                    }
                    msg += " (ids " + sb.ToString() + ")";
                }
                AppLib.WriteLogTraceMessage(msg);
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorShortMessage(ex);
            }

            return retVal;
        }

#endregion

#region set elements

        private void setAppLayout()
        {
            string bgImageFile = AppLib.GetAppSetting("ImagesPath");

            if (AppLib.IsAppVerticalLayout)
            {
                bgImageFile = AppLib.GetFullFileName(bgImageFile, "bg 3ver 1080x1920 background.png");
                // пересоздать кол-во строк и столбцов
                transposeGrid(G15);
                transposeGrid(G24);
            }
            else
            {
                bgImageFile = AppLib.GetFullFileName(bgImageFile, "bg 3hor 1920x1080 background.png");
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
            double fontSize = ((AppLib.IsAppVerticalLayout) ? 0.4 : 0.5) * brdTitle.ActualHeight;
            tbMainTitle.FontSize = fontSize;

            // logo image
            string logoFile = AppLib.GetAppSetting("LogoImage");
            if (logoFile != null)
            {
                logoFile = AppLib.GetFullFileName((string)AppLib.GetAppGlobalValue("ImagesPath", ""), logoFile);
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
            string opacity = AppLib.GetAppSetting("MenuBackgroundBrightness");
            if (opacity != null)
            {
                backgroundImage.Opacity = opacity.ToDouble();
            }
        }

        private Size getMainGridSize()
        {
            double gridW=0, gridH=0;
            gridW = (double)AppLib.GetAppGlobalValue("screenWidth");
            gridH = AppLib.GetRowHeightAbsValue(mainGrid, 1, (double)AppLib.GetAppGlobalValue("screenHeight"));

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
