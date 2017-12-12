using KDSWPFClient.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KDSWPFClient.View
{
#if notUserControl
    public class DishDelimeterPanel : Border
    {
        // признаки поведения элемента
        //   не отрывать от следующего
        public bool DontTearOffNext { get; set; }


        // CTOR
        public DishDelimeterPanel(double width, Brush foreground, Brush background, string text)
        {
            this.Width = width;
            this.Background = background;
            this.BorderBrush = Brushes.DarkBlue;
            this.BorderThickness = new Thickness(1.0d);

            double fontSize = Convert.ToDouble(WpfHelper.GetAppGlobalValue("ordPnlDishDelimiterFontSize", 20d));
            double fontScale = Convert.ToDouble(WpfHelper.GetAppGlobalValue("AppFontScale", 1.0d));
            if (fontScale == 0d) fontScale = 1.0d;
            fontSize *= fontScale;

            TextBlock tblDelimText = new TextBlock()
            {
                TextAlignment = TextAlignment.Center,
                Foreground = foreground,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold, FontStretch = FontStretches.Expanded,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 3),
                FontSize = fontSize,
                Text = text
            };

            this.Child = tblDelimText;
        }

    }  // end class DishDelimeterPanel 
#endif
}
