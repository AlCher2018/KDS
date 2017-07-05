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
        private string[] _titleLangs;
        private string[][] _statusLangs;

        private bool _isShowWaitText;
        private string[] _waitTextLangs;
        private bool _isShowClientName;

        private Grid _gridCell;
        private Path _delimLine;
        private TextBlock _tbStatusTitle, _tbStatusName;
        private Run _tbNumber;
        private Image _imgStatusReady;
        private bool _isVisible;

        private double _fontSize;

        public bool CellVisible { get { return _isVisible; } }

        public CellContainer(double width, double height, CellBrushes[] cellBrushes, bool isShowWaitText, bool isShowClientName)
        {
            _brushes = cellBrushes;

            _titleLangs = (string[])AppLib.GetAppGlobalValue("PanelTitle");
            _statusLangs = (string[][])AppLib.GetAppGlobalValue("StatusLang");

            _isShowWaitText = isShowWaitText;
            if (_isShowWaitText) _waitTextLangs = (string[])AppLib.GetAppGlobalValue("PanelWaitText");
            _isShowClientName = isShowClientName;

            _isVisible = false;

            base.Visibility = Visibility.Collapsed;

            double dMin = Math.Min(width, height);
            _fontSize = (double)AppLib.GetAppGlobalValue("OrderNumberFontSize", 0);
            if (_fontSize == 0) _fontSize = 0.3d * dMin;

            double d1, d2;

            base.CornerRadius = new System.Windows.CornerRadius(0.1 * dMin);
            d1 = 0.03 * dMin;
            base.Margin = new System.Windows.Thickness(d1);

            // создание контейнера для данных
            double[] rowsHeight = new double[2];
            if (_isShowWaitText) {
                rowsHeight[0] = 0.7d; rowsHeight[1] = 1d;
            }
            else {
                rowsHeight[0] = 1d; rowsHeight[1] = 1d;
            }

            _gridCell = new Grid();
            d1 = 0.05 * dMin; d2 = 0.05 * dMin;
            _gridCell.Margin = new Thickness(d1, d2, d1, d2);
            _gridCell.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(rowsHeight[0], System.Windows.GridUnitType.Star) });
            _gridCell.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(rowsHeight[1], System.Windows.GridUnitType.Star) });

            // номер заказа в первой строке
            //    c текстом ожидания - грид с двумя строками, номером и временем ожидания
            if (_isShowWaitText)
            {
                _fontSize *= 0.8d;
                Grid grid1 = new Grid();
                grid1.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(1d, System.Windows.GridUnitType.Star) });
                grid1.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(1d, System.Windows.GridUnitType.Star) });
                TextBlock tbNum = getOrderNumberTextBlock("Arial");
                Grid.SetRow(tbNum, 0);
                grid1.Children.Add(tbNum);

                Grid.SetRow(grid1, 0);
                _gridCell.Children.Add(grid1);
            }
            //    только номер
            else
            {
                TextBlock tbNum = getOrderNumberTextBlock("Impact");
                Grid.SetRow(tbNum, 0);
                _gridCell.Children.Add(tbNum);
            }

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

            string fileName = AppLib.GetFullFileName(
                (string)AppLib.GetAppGlobalValue("ImagesPath", ""), 
                (string)AppLib.GetAppGlobalValue("StatusReadyImage", ""));
            if (!fileName.IsNull())
            {
                _imgStatusReady = new Image();
                _imgStatusReady.Source = ImageHelper.GetBitmapImage(fileName);
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

        private TextBlock getOrderNumberTextBlock(string fontFamilyName)
        {
            TextBlock tbNum = new TextBlock()
            {
                VerticalAlignment = VerticalAlignment.Center,
            };
            tbNum.Inlines.Add(new Run() { Text = "№ ", FontSize = 0.8d * _fontSize });
            _tbNumber = new Run()
            {
                FontSize = _fontSize,
                FontWeight = FontWeights.Normal,
                FontFamily = new FontFamily(fontFamilyName)   // Arial Black, Impact
            };
            tbNum.Inlines.Add(_tbNumber);

            return tbNum;
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
            _tbStatusTitle.Text = _titleLangs[acceptLang];
            _tbStatusName.Text = _statusLangs[statusId][acceptLang];

            if (_imgStatusReady != null) _imgStatusReady.Visibility = (statusId == 1) ? Visibility.Visible : Visibility.Collapsed;

            if (base.Visibility != Visibility.Visible)
            {
                _isVisible = true;
                base.Visibility = Visibility.Visible;
            }
                
        }

    }  // class
}
