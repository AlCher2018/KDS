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

        private Grid _gridCell;
        private Path _delimLine;
        private TextBlock _tbStatusTitle, _tbStatusName;
        private Run _tbNumber;
        private Image _imgStatusReady;
        private bool _isVisible;

        private double _fontSize;

        public bool CellVisible { get { return _isVisible; } }

        public CellContainer(double width, double height, CellBrushes[] cellBrushes, string[] statusTitleLang, string[][] statusLang)
        {
            _brushes = cellBrushes;
            _statusTitleLang = statusTitleLang;
            _statusLang = statusLang;
            _isVisible = false;

            base.Visibility = Visibility.Collapsed;

            double dMin = Math.Min(width, height);
            _fontSize = (Application.Current as App).orderNumberFontSize;
            if (_fontSize == 0) _fontSize = 0.3d * dMin;

            double d1, d2;

            base.CornerRadius = new System.Windows.CornerRadius(0.1 * dMin);
            d1 = 0.03 * dMin;
            base.Margin = new System.Windows.Thickness(d1);

            _gridCell = new Grid();
            _gridCell.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(1.5d, System.Windows.GridUnitType.Star) });
            _gridCell.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(1d, System.Windows.GridUnitType.Star) });

            // номер заказа в первой строке
            TextBlock tbNum = new TextBlock() { VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0.06 * dMin, 0, 0, 0)
            };
            tbNum.Inlines.Add(new Run() { Text = "№ ", FontSize = 0.5 * _fontSize });
            _tbNumber = new Run()
            {
                FontSize = _fontSize,
                FontWeight = FontWeights.Normal,
                FontFamily = new FontFamily("Impact")   // Arial Black, Impact
            };
            tbNum.Inlines.Add(_tbNumber);
            Grid.SetRow(tbNum, 0);
            _gridCell.Children.Add(tbNum);

            // подчеркнуть номер заказа
            _delimLine = new Path();
            _delimLine.Data = new LineGeometry(new System.Windows.Point(0,0), new System.Windows.Point(width,0));
            _delimLine.StrokeThickness = 2d;
            _delimLine.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
            Grid.SetRow(_delimLine, 0);
            _gridCell.Children.Add(_delimLine);

            // строка состояния заказа
            Grid grdStatus = new Grid();
            d1 = 0.08 * dMin; d2 = 0.2 * dMin;
            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(d1, 0, 0, 0);
            panel.HorizontalAlignment = HorizontalAlignment.Left;
            panel.VerticalAlignment = VerticalAlignment.Center;
            _tbStatusTitle = new TextBlock() { FontSize = 0.1 * dMin };
            _tbStatusTitle.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(_tbStatusTitle);

            _tbStatusName = new TextBlock() { FontSize = 0.15 * dMin, FontWeight= FontWeights.Bold};
            _tbStatusName.Margin = new Thickness(0,-0.04*dMin,0,0);
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
                _imgStatusReady.Margin = new Thickness(0.04 * dMin);
                grdStatus.Children.Add(_imgStatusReady);
            }

            Grid.SetRow(grdStatus, 1);
            _gridCell.Children.Add(grdStatus);

            this.Child = _gridCell;
        }


        public void Clear()
        {
            _isVisible = false;
            base.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="number">Номер заказа</param>
        /// <param name="langId">1-украинский, 2-русский, 3-английский</param>
        /// <param name="statusId">0-готовится, 1-готово, 2-забрано</param>
        public void SetOrderData(int number, int langId, int statusId)
        {
            if ((number <= 0) || (statusId < 0) || (statusId > 2) || (langId < 1) || (langId > 3))
            {
                Clear(); return;
            }
            
            _tbNumber.Text = number.ToString();
            if (_tbNumber.FontSize != _fontSize) _tbNumber.FontSize = _fontSize;

            base.Background = _brushes[statusId].Background;
            _delimLine.Stroke = _brushes[statusId].DelimLine;

            int acceptLang = (langId == 1) ? 1 : (langId == 2) ? 0 : 2;
            _tbStatusTitle.Text = _statusTitleLang[acceptLang];
            _tbStatusName.Text = _statusLang[statusId][acceptLang];
            _imgStatusReady.Visibility = (statusId == 1) ? Visibility.Visible : Visibility.Collapsed;

            if (base.Visibility != Visibility.Visible)
            {
                _isVisible = true;
                base.Visibility = Visibility.Visible;
            }
                
        }

    }  // class
}
