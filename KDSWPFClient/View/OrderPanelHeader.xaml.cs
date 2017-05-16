using System;
using System.Collections.Generic;
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
    /// Interaction logic for OrderPanelHeader.xaml
    /// </summary>
    public partial class OrderPanelHeader : UserControl
    {
        public OrderPanelHeader()
        {
            InitializeComponent();
            this.Width = 250d;
        }

        //protected override Size ArrangeOverride(Size arrangeBounds)
        //{
        //    return base.ArrangeOverride(arrangeBounds);
        //}

        //protected override Size MeasureOverride(Size constraint)
        //{
        //    return base.MeasureOverride(constraint);
        //}

    }// class 
}
