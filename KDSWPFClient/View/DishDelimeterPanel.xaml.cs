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
#if notUserControl == false
    /// <summary>
    /// Interaction logic for DishDelimeterPanel.xaml
    /// </summary>
    public partial class DishDelimeterPanel : UserControl
    {
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(DishDelimeterPanel), new PropertyMetadata(""));

        public DishDelimeterPanelTypeEnum DelimeterType { get; set; }



        // признаки поведения элемента
        public bool DontTearOffNext;

        public DishDelimeterPanel(double width, Brush foreground, Brush background, string text)
        {
            InitializeComponent();

            double fontSize = Convert.ToDouble(WpfHelper.GetAppGlobalValue("ordPnlDishDelimiterFontSize", 20d));
            double fontScale = Convert.ToDouble(WpfHelper.GetAppGlobalValue("AppFontScale", 1.0d));
            if (fontScale == 0d) fontScale = 1.0d;

            fontSize *= fontScale;
            this.tbDelimText.FontSize = fontSize;

        }

    }  // class
#endif
}
