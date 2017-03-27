using ClientOrderQueue.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClientOrderQueue.Model
{
    public class CellContainer: Border
    {
        private CellBrushes[] _brushes;
        private string[] _statusTitleLang;
        private string[][] _statusLang;

        private Path _delimLine;
        private TextBlock _tbStatusTitle, _tbStatusName;
        private Image _imgStatusReady;

        public CellContainer(double width, double height, CellBrushes[] cellBrushes, string[] statusTitleLang, string[][] statusLang)
        {
            _brushes = cellBrushes;
            _statusTitleLang = statusTitleLang;
            _statusLang = statusLang;

            double dMin = Math.Min(width, height);
            double d1, d2;

            base.CornerRadius = new System.Windows.CornerRadius(0.1 * dMin);
            d1 = 0.03 * dMin;
            base.Margin = new System.Windows.Thickness(d1);

            Grid grd = new Grid();
            grd.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(6d, System.Windows.GridUnitType.Star) });
            grd.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(7d, System.Windows.GridUnitType.Star) });
            // номер заказа в первой строке
            TextBlock tbNumber = new TextBlock();
            tbNumber.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            tbNumber.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            tbNumber.Margin = new Thickness(0.1*dMin,0,0,0);
            tbNumber.Inlines.Add(new Run { Text="№", FontSize= 0.1 * dMin });
            tbNumber.Inlines.Add(new Run { Text="123", FontSize= 0.15 * dMin });
            Grid.SetRow(tbNumber, 0);
            grd.Children.Add(tbNumber);

            // подчеркнуть номер заказа
            _delimLine = new Path();
            _delimLine.Data = new LineGeometry(new System.Windows.Point(0,0), new System.Windows.Point(width,0));
            _delimLine.StrokeThickness = 2d;
            _delimLine.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
            Grid.SetRow(_delimLine, 0);
            grd.Children.Add(_delimLine);

            // строка состояния заказа
            Grid grdStatus = new Grid();
            d1 = 0.08 * dMin; d2 = 0.2 * dMin;
            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(d1, 0, 0, 0);
            panel.HorizontalAlignment = HorizontalAlignment.Left;
            panel.VerticalAlignment = VerticalAlignment.Center;
            _tbStatusTitle = new TextBlock() { FontSize = 0.07*dMin };
            _tbStatusTitle.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(_tbStatusTitle);

            _tbStatusName = new TextBlock() { FontSize = 0.12*dMin, FontWeight= FontWeights.Bold};
            _tbStatusName.Margin = new Thickness(0,-0.03*dMin,0,0);
            _tbStatusName.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(_tbStatusName);
            grdStatus.Children.Add(panel);

            string sPath = AppLib.GetAppSetting("ImagesPath");
            string sFName = AppLib.GetAppSetting("StatusReadyImage");
            if ((sPath != null) && (sFName != null))
            {
                _imgStatusReady = new Image();
                _imgStatusReady.Source = ImageHelper.GetBitmapImage(AppLib.GetFullFileName(sPath, sFName));
                _imgStatusReady.Stretch = Stretch.Uniform;
                _imgStatusReady.VerticalAlignment = VerticalAlignment.Center;
                _imgStatusReady.HorizontalAlignment = HorizontalAlignment.Right;
                _imgStatusReady.Margin = new Thickness(0, 0.05 * dMin, 0.8*d1, 0.05 * dMin);
                grdStatus.Children.Add(_imgStatusReady);
            }

            Grid.SetRow(grdStatus, 1);
            grd.Children.Add(grdStatus);

            // DEBUG
            base.Background = _brushes[0].Background;
            _delimLine.Stroke = _brushes[0].DelimLine;
            _tbStatusTitle.Text = _statusTitleLang[0];
            _tbStatusName.Text = _statusLang[0][0];

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
