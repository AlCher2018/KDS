﻿using KDSWPFClient.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KDSWPFClient.View
{
    /// <summary>
    /// Interaction logic for DishDelimeterPanel.xaml
    /// </summary>
    public partial class DishDelimeterPanel : UserControl
    {

        public int FilingNumber { get; set; }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(DishDelimeterPanel), new PropertyMetadata(""));


        public DishDelimeterPanel()
        {
            InitializeComponent();

            this.Loaded += DishDelimeterPanel_Loaded;

            double fontSize = (double)AppLib.GetAppGlobalValue("ordPnlDishDelimiterFontSize", 20d);
            double fontScale = AppLib.GetAppSetting("AppFontScale").ToDouble();
            if (fontScale == 0d) fontScale = 1.0d;

            fontSize *= fontScale;
            this.tbDelimText.FontSize = fontSize;

        }

        private void DishDelimeterPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (FilingNumber == 1)
            {
                this.tbDelimText.Foreground = Brushes.Red;
            }
        }

    }  // class
}
