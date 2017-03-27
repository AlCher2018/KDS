using ClientOrderQueue.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClientOrderQueue
{
    public class CellContainer: Border
    {
        private Brush cookingBrush = null, cookedBrush = null;

        public CellContainer(double width, double height)
        {
            double dMin = Math.Min(width, height);

            base.CornerRadius = new System.Windows.CornerRadius(0.1 * dMin);
            double d1 = 0.03 * dMin;
            base.Margin = new System.Windows.Thickness(d1);

            cookingBrush = (Brush)App.Current.Resources["appCookingBrush"];
            if (cookingBrush == null) cookingBrush = Brushes.Orange;
            cookedBrush = (Brush)App.Current.Resources["appCookedBrush"];
            if (cookedBrush == null) cookedBrush = Brushes.Lime;

            Grid grd = new Grid();
            grd.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(6d, System.Windows.GridUnitType.Star) });
            grd.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(7d, System.Windows.GridUnitType.Star) });
            // номер заказа в первой строке
            TextBlock tbNumber = new TextBlock();
            tbNumber.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            tbNumber.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            tbNumber.Inlines.Add(new Run { Text="№", FontSize= 0.1 * dMin });
            tbNumber.Inlines.Add(new Run { Text="123", FontSize= 0.17 * dMin });
            Grid.SetRow(tbNumber, 0);
            grd.Children.Add(tbNumber);

            // подчеркнуть номер заказа
            Path path = new Path();
            path.Data = new LineGeometry(new System.Windows.Point(0,0), new System.Windows.Point(width,0));
            path.Stroke = new SolidColorBrush(Color.FromRgb(218,151,88));
            path.StrokeThickness = 2d;
            path.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
            Grid.SetRow(path, 0);// Grid.SetRowSpan(path,2);
            grd.Children.Add(path);

            base.Background = cookingBrush;
            this.Child = grd;
        }

        public void Clear()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="number"></param>
        /// <param name="langId">1-украинский, 2-русский, 3-английский</param>
        /// <param name="statusId">0-готовится, 1-готово, 2-забрано</param>
        public void SetOrderData(int number, int langId, int statusId)
        {

        }

    }  // class
}
