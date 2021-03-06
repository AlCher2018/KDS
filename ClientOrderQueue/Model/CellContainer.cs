﻿using ClientOrderQueue.Lib;
using IntegraLib;
using IntegraWPFLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClientOrderQueue.Model
{
    public class CellContainer: Border
    {
        #region fields & properties
        private AppOrder _appOrder;
        public AppOrder AppOrder { get { return _appOrder; } }

        private double _width, _height;

        private Brush[] _brushes;
        public Brush[] PanelBrushes { set { _brushes = value; } }

        private string[] _titleLangs;
        public string[] TitleLangs { set { _titleLangs = value; } }

        private string[][] _statusLangs;
        public string[][] StatusLangs { set { _statusLangs = value; } }

        // время приготовления заказа
        private Timer _timer;
        private string[] _waitTextLangs;
        private bool _isShowOrderEstimateTime;
        public bool IsShowOrderEstimateTime { set { _isShowOrderEstimateTime = value; } }
        private bool _isShowClientName;
        public bool IsShowClientName { set { _isShowClientName = value; } }

        private double _orderNumberFontSize;
        public double OrderNumberFontSize { set { _orderNumberFontSize = value; } }

        private string _statusReadyImageFile;
        public string StatusReadyImageFile { set { _statusReadyImageFile = value; } }
        #endregion

        private double _dMinSize;
        private Grid _mainGrid;
        private Path _delimLine;
        private TextBlock _tbStatusTitle, _tbStatusName;
        private TextBlock _tbWaitText, _tbWaitTime;
        private Run _tbNumber;
        private Image _imgStatusReady;
        private bool _isVisible;

        public bool CellVisible { get { return _isVisible; } }


        public CellContainer(double width, double height)
        {
            this.Loaded += CellContainer_Loaded;

            _width = width; _height = height;
            _dMinSize = Math.Min(_width, _height);

            _isVisible = false;
            base.Visibility = Visibility.Collapsed;
            base.CornerRadius = new System.Windows.CornerRadius(0.1 * _dMinSize);
            base.Margin = new System.Windows.Thickness(0.03 * _dMinSize);

        }

        private void CellContainer_Loaded(object sender, RoutedEventArgs e)
        {
            // здесь, т.к. часть полей инициализируется через свойства класса, ПОСЛЕ конструктора
            if (_isShowOrderEstimateTime)
            {
                _waitTextLangs = (string[])WpfHelper.GetAppGlobalValue("PanelWaitText");
                _timer = new Timer() { Interval = 1000d };
                _timer.Elapsed += _timer_Elapsed;
            }
            if (_orderNumberFontSize == 0) _orderNumberFontSize = 0.3d * _dMinSize;

            createElements();
        }

        #region create elements
        private void createElements()
        {
            // создание контейнера для данных
            double[] rowsHeight = new double[2];
            if (_isShowOrderEstimateTime) { rowsHeight[0] = 0.9d; rowsHeight[1] = 1d; }
            else { rowsHeight[0] = 1d; rowsHeight[1] = 1d; }
            // главный контейнер
            _mainGrid = new Grid();
            _mainGrid.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(rowsHeight[0], System.Windows.GridUnitType.Star) });
            _mainGrid.RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(rowsHeight[1], System.Windows.GridUnitType.Star) });
            //double hRow1 = AppLib.GetRowHeightAbsValue(_mainGrid, 0, _height);
            //double hRow2 = AppLib.GetRowHeightAbsValue(_mainGrid, 1, _height);

            createRow1();
            createRowDelimiter();
            createRow2();

            this.Child = _mainGrid;
        }


        // первая строка панели: номер заказа и строка ожидания
        private void createRow1()
        {
            double d1 = 0.06 * _dMinSize, d2 = 0.01 * _dMinSize;
            Thickness gridMargin = new Thickness(d1, d2, d1, 0d);

            //    c текстом ожидания - панель с двумя строками, номером и временем ожидания
            if (_isShowOrderEstimateTime)
            {
                StackPanel grid1 = new StackPanel();
                grid1.Margin = gridMargin;
                // номер заказа
                Border brd = new Border() { Background = Brushes.Transparent };
                TextBlock tbNum = getOrderNumberTextBlock("Arial", false);
                brd.Child = tbNum;
                grid1.Children.Add(brd);

                // текст ожидания
                DockPanel dpnl = new DockPanel() {
                    VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -0.3 * d1, 0, 0)
                };
                d1 = 0.12d * _dMinSize;  // шрифт текста
                dpnl.LastChildFill = true;
                _tbWaitText = new TextBlock() { HorizontalAlignment = HorizontalAlignment.Left, FontSize = d1 };
                DockPanel.SetDock(_tbWaitText, Dock.Left);
                dpnl.Children.Add(_tbWaitText);
                _tbWaitTime = new TextBlock() { HorizontalAlignment = HorizontalAlignment.Center, FontSize = d1 };
                DockPanel.SetDock(_tbWaitTime, Dock.Left);
                dpnl.Children.Add(_tbWaitTime);
                grid1.Children.Add(dpnl);

                Grid.SetRow(grid1, 0);
                _mainGrid.Children.Add(grid1);
            }

            //    только номер заказа
            else
            {
                TextBlock tbNum = getOrderNumberTextBlock("Impact", false);
                tbNum.Margin = gridMargin;
                Grid.SetRow(tbNum, 0);
                _mainGrid.Children.Add(tbNum);
            }
        }

        // подчеркнуть номер заказа
        private void createRowDelimiter()
        {
            _delimLine = new Path();
            _delimLine.Data = new LineGeometry(new System.Windows.Point(0, 0), new System.Windows.Point(_width, 0));
            _delimLine.StrokeThickness = 2d;
            _delimLine.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
            Grid.SetRow(_delimLine, 0);
            _mainGrid.Children.Add(_delimLine);
        }

        // вторая строка панели: имя клиента, состояние заказа и изображение готовности
        private void createRow2()
        {
            // справа изображение, первым, чтобы его мог перекрывать текст
            Grid grdStatus = new Grid();
            if (!_statusReadyImageFile.IsNull())
            {
                _imgStatusReady = new Image();
                _imgStatusReady.Source = ImageHelper.GetBitmapImage(_statusReadyImageFile);
                _imgStatusReady.Stretch = Stretch.Uniform;
                _imgStatusReady.Width = 0.32d * _width;
                _imgStatusReady.VerticalAlignment = VerticalAlignment.Center;
                _imgStatusReady.HorizontalAlignment = HorizontalAlignment.Right;
                _imgStatusReady.Margin = new Thickness(0,0,0.08 * _dMinSize,0);
                grdStatus.Children.Add(_imgStatusReady);
            }

            // слева - вертикальный стек с двумя полями
            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(0.06*_dMinSize,0,0,0);
            panel.VerticalAlignment = VerticalAlignment.Center;
            panel.HorizontalAlignment = HorizontalAlignment.Left;

            _tbStatusTitle = new TextBlock() { FontSize = 0.12 * _dMinSize };
            _tbStatusTitle.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(_tbStatusTitle);

            _tbStatusName = new TextBlock() { FontSize = 0.15 * _dMinSize, FontWeight = FontWeights.Bold };
            _tbStatusName.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(_tbStatusName);
            grdStatus.Children.Add(panel);

            Grid.SetRow(grdStatus, 1);
            _mainGrid.Children.Add(grdStatus);
        }

        private TextBlock getOrderNumberTextBlock(string fontFamilyName, bool isBold)
        {
            TextBlock tbNum = new TextBlock()
            {
                VerticalAlignment = VerticalAlignment.Center,
            };
            tbNum.Inlines.Add(new Run() {
                Text = "№ ", FontSize = 0.8d * _orderNumberFontSize,
                FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal
            });
            _tbNumber = new Run()
            {
                FontSize = _orderNumberFontSize,
                FontWeight = isBold ? FontWeights.Bold: FontWeights.Normal,
                FontFamily = new FontFamily(fontFamilyName)   // Arial Black, Impact
            };
            tbNum.Inlines.Add(_tbNumber);

            return tbNum;
        }

        #endregion

        /// <summary>
        /// Обновление информации на панели заказа
        /// </summary>
        /// <param name="number">Номер заказа</param>
        /// <param name="langId">1-украинский, 2-русский, 3-английский</param>
        /// <param name="statusId">0-готовится, 1-готово, 2-забрано</param>
        public void SetOrderData(AppOrder appOrder)
        {
            _appOrder = appOrder;

            int langId = _appOrder.Order.LanguageTypeId,
                statusId = _appOrder.Order.QueueStatusId;

            _tbNumber.Text = _appOrder.Order.Number.ToString();
            if (_tbNumber.FontSize != _orderNumberFontSize) _tbNumber.FontSize = _orderNumberFontSize;

            if (base.Background != _brushes[statusId])
            {
                base.Background = _brushes[statusId];
                _delimLine.Stroke = getDarkerBrush(_brushes[statusId]);
            }

            int acceptLang = (langId == 1) ? 1 : (langId == 2) ? 0 : 2;

            // в заголовке статуса показывать или заголовок статуса(для соотв.языка), или наименование клиента
            _tbStatusTitle.Text = (_isShowClientName) ? _appOrder.Order.ClientName : _titleLangs[acceptLang];

            if (_isShowOrderEstimateTime)
            {
                _tbWaitText.Text = _waitTextLangs[acceptLang];

                if (!_timer.Enabled)
                {
                    _timer.Enabled = true;
                    updateWaitTimer();
                }
            }

            _tbStatusName.Text = _statusLangs[statusId][acceptLang];

            if (_imgStatusReady != null) _imgStatusReady.Visibility = (statusId == 1) ? Visibility.Visible : Visibility.Collapsed;

            if (base.Visibility != Visibility.Visible)
            {
                _isVisible = true;
                base.Visibility = Visibility.Visible;
            }

        }

        private Brush getDarkerBrush(Brush brush)
        {
            if (brush is SolidColorBrush)
            {
                float darkKoef = 0.6f; // the less the darker
                Color col = (brush as SolidColorBrush).Color;
                Color col1 = Color.Multiply(col, darkKoef); col1.A = 255;
                return new SolidColorBrush(col1);
            }
            else
                return Brushes.Gray;
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Enabled = false;

            this.Dispatcher.Invoke(updateWaitTimer);

            _timer.Enabled = true;
        }

        private void updateWaitTimer()
        {
            TimeSpan ts = (_appOrder.IsExistOrderEstimateDT 
                ? _appOrder.OrderCookingBaseDT - DateTime.Now 
                : DateTime.Now - _appOrder.OrderCookingBaseDT);
            ts = getRoundedTimeSpan(ts, 1d);

            _tbWaitTime.Text = AppLib.GetAppStringTS(ts);
            //Debug.Print(_tbWaitTime.Text);
        }

        public void Clear()
        {
            _appOrder = null;
            _isVisible = false;
            base.Visibility = Visibility.Collapsed;
            if (_isShowOrderEstimateTime)
            {
                if (_timer.Enabled) _timer.Enabled = false;
                _tbWaitText.Text = "";_tbWaitTime.Text = "";
            }
        }

        private TimeSpan getRoundedTimeSpan(TimeSpan ts, double divider)
        {
            //переводим такты в секунды
            double sec = (double)ts.Ticks / 10000000d;

            //округляем секунды
            var newSec = Math.Round(sec / divider) * divider;

            TimeSpan retVal = TimeSpan.FromSeconds(newSec);
            return retVal;
        }

    }  // class
}
