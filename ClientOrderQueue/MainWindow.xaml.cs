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

        public MainWindow()
        {
            InitializeComponent();

            setCellBrushes();
            _cookingIds = new List<int>();

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

        private void setCellBrushes()
        {
            _cellBrushes = new CellBrushes[2] { new CellBrushes(), new CellBrushes() };
            App app = (App)Application.Current;

            // cooking
            _cellBrushes[0].Background = (Brush)app.Resources["statusCookingBrush"];
            if (_cellBrushes[0].Background == null) _cellBrushes[0].Background = Brushes.Orange;
            _cellBrushes[0].DelimLine = new SolidColorBrush(Color.FromRgb(218, 151, 88));

            // cooked
            _cellBrushes[1].Background = (Brush)app.Resources["statusCookedBrush"];
            if (_cellBrushes[1].Background == null) _cellBrushes[1].Background = Brushes.Lime;
            _cellBrushes[1].DelimLine = new SolidColorBrush(Color.FromRgb(97, 210, 67));
        }

        private void createGridContainers(Grid grid)
        {
            Size mainGridSize = getMainGridSize();
            int rowsCount = grid.RowDefinitions.Count, colsCount = grid.ColumnDefinitions.Count;

            double cellWidth = mainGridSize.Width / (double)colsCount, 
                cellHeight = mainGridSize.Height / (double)rowsCount;

            bool isShowWaitText = ((double)AppLib.GetAppGlobalValue("OrderReadyTime") != 0d);
            bool isShowClientName = (bool)AppLib.GetAppGlobalValue("IsShowClientName");

            for (int i = 0; i < rowsCount; i++)
                for (int j = 0; j < colsCount; j++)
                {
                    CellContainer cc = new CellContainer(cellWidth, cellHeight, _cellBrushes, isShowWaitText, isShowClientName);
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
                        Order o = orders[listIndex];
                        cc.SetOrderData(o.Number, o.LanguageTypeId, o.QueueStatusId);

                        if (o.QueueStatusId == 0) s0.Add(o.Id);
                        else if ((o.QueueStatusId == 1) && (isCooked == false) && (_cookingIds.Contains(o.Id))) isCooked = true;
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
                using (KDSContext db = new KDSContext())
                {
                    db.Database.Connection.Open();
                    if (db.Database.Connection.State == System.Data.ConnectionState.Open)
                    {
                        List<Order> dbOrders = (from o in db.vwOrderQueue
                                                orderby o.CreateDate ascending, o.Number ascending
                                                select o).ToList();
                        retVal = dbOrders;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLib.WriteLogShortErrorMessage(ex);
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


    }  // class
}
