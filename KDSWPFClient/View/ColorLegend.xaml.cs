﻿using KDSWPFClient.Lib;
using KDSWPFClient.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;


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

            // размещение окна
            KDSWPFClient.MainWindow mainWin = (KDSWPFClient.MainWindow)Application.Current.MainWindow;
            Point p1 = mainWin.PointToScreen(new Point(mainWin.brdAdmin.ActualWidth, 0d));
            Point p2 = mainWin.PointToScreen(new Point(mainWin.vbxOrders.ActualWidth, mainWin.vbxOrders.ActualHeight));
            this.Left = p1.X; this.Top = p1.Y;
            //this.Width = (p2.X - p1.X) / 2d; 


            // источник данных
            Dictionary<string, BrushesPair> appBrushes = BrushHelper.AppBrushes;

            // собрать кисти в список для легенды
            List<BrushesPair> context = new List<BrushesPair>();
            foreach (BrushesPair item in appBrushes.Values)
            {
                if (!item.Name.StartsWith("~")) context.Add(item);
            }

            lstLegend.ItemsSource = context;
        }

        private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            this.Close();
        }
    }  // class
}
