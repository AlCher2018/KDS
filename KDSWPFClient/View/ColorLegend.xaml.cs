using IntegraLib;
using IntegraWPFLib;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;


namespace KDSWPFClient.View
{
    /// <summary>
    /// Interaction logic for ColorLegend.xaml
    /// </summary>
    public partial class ColorLegend : Window
    {
        public ColorLegend()
        {
            InitializeComponent();

            // источник данных
            Dictionary<string, BrushesPair> appBrushes = BrushHelper.AppBrushes;

            // собрать кисти в список для легенды
            bool isUseReadyConfirm = (bool)WpfHelper.GetAppGlobalValue("UseReadyConfirmedState", false);
            List<BrushesPair> context = new List<BrushesPair>();
            foreach (KeyValuePair<string, BrushesPair> item in appBrushes)
            {
                if (!item.Value.Name.StartsWith("~"))
                {
                    if (item.Key.StartsWith(OrderStatusEnum.ReadyConfirmed.ToString()))
                    {
                        if (isUseReadyConfirm) context.Add(item.Value);
                    }
                    else
                        context.Add(item.Value);
                }
            }

            lstLegend.ItemsSource = context;
        }

        protected override void OnActivated(EventArgs e)
        {
            // размещение окна
            KDSWPFClient.MainWindow mainWin = (KDSWPFClient.MainWindow)Application.Current.MainWindow;
            if (mainWin != null)
            {
                Point p1 = mainWin.PointToScreen(new Point(mainWin.brdAdmin.ActualWidth, 0d));
                Point p2 = mainWin.PointToScreen(new Point(mainWin.vbxOrders.ActualWidth, mainWin.vbxOrders.ActualHeight));
                if (this.Left != p1.X) this.Left = p1.X;
                if (this.Top != p1.Y) this.Top = p1.Y;
                //this.Width = (p2.X - p1.X) / 2d; 
            }

            base.OnActivated(e);
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.Hide();
            e.Handled = true;
        }

    }  // class
}
