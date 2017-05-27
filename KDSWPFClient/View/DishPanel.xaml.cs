using KDSWPFClient.Lib;
using KDSWPFClient.ViewModel;
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
        private double _fontSize;

        public DishPanel(OrderDishViewModel dishView)
        {
            InitializeComponent();

            grdDishLine.DataContext = dishView;

            double dishLineMinHeight = (double)AppLib.GetAppGlobalValue("ordPnlDishLineMinHeight");
            base.MinHeight = dishLineMinHeight;
            double fontScale = AppLib.GetAppSetting("AppFontScale").ToDouble();
            double fontSize = (double)AppLib.GetAppGlobalValue("ordPnlDishLineFontSize"); // 12d
            _fontSize = fontSize * fontScale;

            // на уровне всего элемента для всех TextBlock-ов
            this.SetValue(TextBlock.FontSizeProperty, _fontSize);
            // отдельно для некоторых TextBlock-ов
            this.tbDishIndex.FontSize = 0.8 * _fontSize;

            brdMain.Padding = new Thickness(0, 5, 0, 5);

            //IEnumerable<TextBlock> tbs = grdDishLine.Children.OfType<TextBlock>();
            //foreach (TextBlock tb in tbs)
            //{
            //    tb.FontSize = _fontSize;
            //}
        }

        private void root_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void root_MouseUp(object sender, MouseButtonEventArgs e)
        {
            OrderDishViewModel dishView = (OrderDishViewModel)grdDishLine.DataContext;
            if (!dishView.ParentUID.IsNull()) return;  // это ингредиент !!

            OrderViewModel orderView = null;
            FrameworkElement orderPanel = AppLib.FindVisualParent(this, typeof(OrderPanel), null);
            if (orderPanel != null) orderView = (orderPanel as OrderPanel).OrderViewModel;

            StateChange win = new StateChange() { Order = orderView, Dish = dishView };

            win.ShowDialog();

        //    MessageBox.Show(string.Format("dish id {0} - {1}, state {2}",dishView.Id, dishView.DishName, dishView.Status));
        }

    }  // class
}
