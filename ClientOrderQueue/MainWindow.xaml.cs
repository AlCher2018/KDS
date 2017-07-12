using ClientOrderQueue.Lib;
using ClientOrderQueue.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClientOrderQueue
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Media.SoundPlayer simpleSound = null;
        private DispatcherTimer updateTimer = new DispatcherTimer();

        private CellBrushes[] _cellBrushes;
        private List<int> _cookingIds;

        private HashSet<int> _unUsedDeps;

        public MainWindow()
        {
            InitializeComponent();

            _cookingIds = new List<int>();
            _unUsedDeps = (HashSet<int>)AppLib.GetAppGlobalValue("UnusedDepartments");

            // кисти заголовка окна
            brdTitle.Background = (SolidColorBrush)AppLib.GetAppGlobalValue("WinTitleBackground");
            tbMainTitle.Foreground = (SolidColorBrush)AppLib.GetAppGlobalValue("WinTitleForeground");

            // кисти для панелей заказов
            _cellBrushes = (CellBrushes[])AppLib.GetAppGlobalValue("PanelBackgroundBrushes");

            string statusReadyAudioFile = AppLib.GetFullFileName(AppLib.GetAppSetting("AudioPath"), AppLib.GetAppSetting("StatusReadyAudioFile"));
            if (System.IO.File.Exists(statusReadyAudioFile))
            {
                simpleSound = new System.Media.SoundPlayer(statusReadyAudioFile);
            }

            this.Loaded += MainWindow_Loaded;
            setAppLayout();

            createGridContainers(G15);
            createGridContainers(G24);

            updateTimer.Tick += new EventHandler(updateTimer_Tick);
            updateTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            updateTimer.Start();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            setLayoutAfterLoaded(); // Logo image
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
                        OrderReadyMinute = (double)AppLib.GetAppGlobalValue("OrderReadyTime"),
                        IsShowClientName = (bool)AppLib.GetAppGlobalValue("IsShowClientName"),
                        OrderNumberFontSize = (double)AppLib.GetAppGlobalValue("OrderNumberFontSize", 0),
                        StatusReadyImageFile = (string)AppLib.GetAppGlobalValue("StatusReadyImageFile")
                    };

                    Grid.SetRow(cc, i); Grid.SetColumn(cc, j);
                    grid.Children.Add(cc);
                }

        }  // method

        #region update timer

        private void updateTimer_Tick(object sender, EventArgs e)
        {
            updateTimer.Stop();
#if DEBUG
            DateTime dt1 = DateTime.Now;
#endif
            loadItems();

#if DEBUG
            System.Diagnostics.Debug.Print("-- " + DateTime.Now.Subtract(dt1).TotalMilliseconds);
#endif
            updateTimer.Start();
        }

        private void loadItems()
        {
            List<Order> orders = getOrders();

            if ((orders == null) || (orders.Count == 0))
            {
                hideCells(G15); hideCells(G24);
                return;
            }

            if (orders.Count <= 15)
            {
                fillCells(G15, orders);
                setGridVisibility(G24, Visibility.Collapsed);
                setGridVisibility(G15 , Visibility.Visible);
            }
            else
            {
                fillCells(G24, orders);
                setGridVisibility(G15, Visibility.Collapsed);
                setGridVisibility(G24, Visibility.Visible);
            }
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

        private void fillCells(Grid grid, List<Order> orders)
        {
            // коллекция Id заказов в состоянии приготовления (QueueStatusId == 0)
            List<int> s0 = new List<int>();  
            bool isCooked = false;  // признак того, что готовящийся заказ изменил статус на ГОТОВ и надо проиграть мелодию

            int rowCount = grid.RowDefinitions.Count;
            int colCount = grid.ColumnDefinitions.Count;
            int listIndex;
            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < colCount; j++)
                {
                    listIndex = (i * colCount) + j;
                    CellContainer cc = (CellContainer)grid.Children[listIndex];
                    if (listIndex < orders.Count)
                    {
                        Order curOrder = orders[listIndex];
                        cc.SetOrderData(curOrder);

                        if (curOrder.QueueStatusId == 0)
                            s0.Add(curOrder.Id);
                        else if ((curOrder.QueueStatusId == 1) 
                                && (isCooked == false) 
                                && (_cookingIds.Contains(curOrder.Id)))
                            isCooked = true;
                    }
                    else
                        cc.Clear();
                }
            }
            _cookingIds = s0;
            if ((isCooked) && (simpleSound != null)) simpleSound.Play();
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
                        List<Order> dbOrders = (from o in db.vwOrderQueue
                                                orderby o.CreateDate ascending, o.Number ascending
                                                select o).ToList();

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
