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
    /// Interaction logic for DishPanel.xaml
    /// </summary>
    public partial class DishPanel : UserControl
    {


        public DishPanel()
        {
            InitializeComponent();
        }

        public DishPanel(int index, int filingNumber, string dishName, decimal quantity): this()
        {
            this.tbDishIndex.Text = index.ToString();
            this.tbDishFilingNumber.Text = filingNumber.ToString();
            this.tbDishName.Text = dishName;
            this.tbDishQuantity.Text = quantity.ToString();

            brdMain.Padding = new Thickness(0, 5, 0, 5);
        }

    }  // class
}
