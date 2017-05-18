using KDSWPFClient.Lib;
using System;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace KDSWPFClient.View
{
    /// <summary>
    /// Interaction logic for OrderPanel.xaml
    /// </summary>
    public partial class OrderPanel : UserControl
    {
        private double _fontSize;

        // ctor
        public OrderPanel()
        {
            InitializeComponent();

            double fontScale = (double)AppLib.GetAppGlobalValue("AppFontScale");
            double fontSize = (double)AppLib.GetAppGlobalValue("ordPnlDishTblHeaderFontSize"); // 10d
            _fontSize = fontSize * fontScale;

            IEnumerable<TextBlock> tbs = grdTblHeader.Children.OfType<TextBlock>();
            foreach (TextBlock tb in tbs)
            {
                tb.FontSize = _fontSize;
            }

        }

        public void SetHeader(FrameworkElement header)
        {
            this.grdHeader.Children.Add(header);
        }

        public void AddDish(DishPanel dishPanel)
        {
            this.stkDishes.Children.Add(dishPanel);
        }


    }  // class
}
