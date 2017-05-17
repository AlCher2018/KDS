using System;
using System.Timers;
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
        // ctor
        public OrderPanel()
        {
            InitializeComponent();
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
