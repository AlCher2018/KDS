using ClientOrderQueue.Lib;
using ClientOrderQueue.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

        private List<int> _cookingIds;

        public MainWindow()
        {
            InitializeComponent();

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

        private void createGridContainers(Grid grid)
        {
            Size mainGridSize = getMainGridSize();
            int rowsCount = grid.RowDefinitions.Count, colsCount = grid.ColumnDefinitions.Count;
            App app = (Application.Current as App);

            double cellWidth = mainGridSize.Width / (double)colsCount, 
                cellHeight = mainGridSize.Height / (double)rowsCount;

            for (int i = 0; i < rowsCount; i++)
                for (int j = 0; j < colsCount; j++)
                {
                    CellContainer cc = new CellContainer(cellWidth, cellHeight, app.cellBrushes, app.statusTitleLang, app.statusLang);
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
            List<int> s0 = new List<int>();
            bool isCooked = false;

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
                    //retVal = db.Order.OrderBy(o => o.Number).Where(o => o.QueueStatusId < 2).ToList();
                    retVal = (from o in db.Order where o.QueueStatusId < 2 orderby o.Number select o).ToList();
                }
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage(string.Format("{0}", ex.Message));
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
                logoFile = AppLib.GetFullFileName(AppLib.GetAppSetting("ImagesPath"), logoFile);
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
