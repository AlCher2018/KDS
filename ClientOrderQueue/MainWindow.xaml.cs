using ClientOrderQueue.Lib;
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


        public MainWindow()
        {
            InitializeComponent();

            string statusReadyAudioFile = AppLib.GetFullFileName(AppLib.GetAppSetting("AudioPath"), AppLib.GetAppSetting("StatusReadyAudioFile"));
            if (System.IO.File.Exists(statusReadyAudioFile))
            {
                simpleSound = new System.Media.SoundPlayer(statusReadyAudioFile);
            }

            this.Loaded += MainWindow_Loaded;
            setAppLayout();

            createGridContainers(G15);
            createGridContainers(G24);

            updateTimer.Tick += new EventHandler(updateTimerTimer_Tick);
            updateTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            updateTimer.Start();
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
                    CellContainer cc = new CellContainer(cellWidth, cellHeight);
                    Grid.SetRow(cc, i); Grid.SetColumn(cc, j);
                    grid.Children.Add(cc);
                }

        }  // method

        #region update timer

        private void updateTimerTimer_Tick(object sender, EventArgs e)
        {
            loadItems();
        }

        private void loadItems()
        {
            List<Order> orders = getOrders();
            if ((orders == null) || (orders.Count == 0)) return;

            if (orders.Count <= 15)
            {

                setGridVisibility(G24, Visibility.Collapsed);
                setGridVisibility(G15 , Visibility.Visible);
            }
            else
            {
                setGridVisibility(G15, Visibility.Collapsed);
                setGridVisibility(G24, Visibility.Visible);
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
                using (KDSContext db = new KDSContext())
                {
                    retVal = db.Order.OrderBy(o => o.Number).Where(o => o.QueueStatusId < 2).ToList();
                }
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage(string.Format("{0}\n{1}", ex.Message, ex.StackTrace));
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
            }
            else
            {
                bgImageFile = AppLib.GetFullFileName(bgImageFile, "bg 3hor 1920x1080 background.png");
            }

            setBackgroundImage(bgImageFile); // фон
        }

        private void setLogoImage()
        {
            string logoFile = AppLib.GetAppSetting("LogoImage");
            if (logoFile != null)
            {
                logoFile = AppLib.GetFullFileName(AppLib.GetAppSetting("ImagesPath"), logoFile);
                double d1 = 0.2 * brdTitle.ActualHeight;
                imgLogo.Source = ImageHelper.GetBitmapImage(logoFile);
                imgLogo.Margin = new Thickness(0, d1, d1, d1);
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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            setLogoImage(); // Logo image
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
