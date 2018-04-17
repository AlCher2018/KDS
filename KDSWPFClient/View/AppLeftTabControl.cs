using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KDSWPFClient.Model;


namespace KDSWPFClient.View
{
    /// <summary>
    /// AppLeftTabControl - класс, представляющий кнопку на боковой, админской панели
    /// </summary>

    public class AppLeftTabControl: Border
    {
        private const double _leftMarginKoef = 0.15d;

        private double _dWidthBase;
        private TextBlock _tBlock;
        private Border _shadowBorder;
        private Grid _grid;
        private Viewbox _viewBox;

        public string Text {
            get { return (_tBlock==null) ? null : _tBlock.Text; }
            set { if (_tBlock != null) _tBlock.Text = value; }
        }

        private KDSUserStatesSet _statesSet;
        public KDSUserStatesSet StatesSet { get { return _statesSet; } }

        private bool _isEnabled;
        public new bool IsEnabled
        {
            get { return _isEnabled; }
            set { setEnabled(value); }
        }


        // CTOR
        public AppLeftTabControl(double width, double height, string text, double topKoef)
        {
            this.Height = height;
            _dWidthBase = width;
            this.Width = width * (1.0d - _leftMarginKoef);

            double dRad = 0.15d * Math.Min(this.Width, this.Height);
            CornerRadius corners = new CornerRadius(dRad, 0, 0, dRad);

            BorderBrush = Brushes.DarkGray;
            BorderThickness = new Thickness(2, 2, 0, 2);
            CornerRadius = corners;
            Background = Brushes.Transparent;
            HorizontalAlignment = HorizontalAlignment.Right;
            Margin = new Thickness(0, topKoef * height, 0, 0);
            base.IsEnabled = true;

            _grid = new Grid();
            _grid.SetBinding(Grid.WidthProperty, new System.Windows.Data.Binding() { Source = this, Path = new PropertyPath("Width") });
            _grid.SetBinding(Grid.HeightProperty, new System.Windows.Data.Binding() { Source = this, Path = new PropertyPath("Height") });

            _viewBox = new Viewbox();
            _tBlock = new TextBlock()
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
            };
            _viewBox.Child = _tBlock;

            _grid.Children.Add(_viewBox);
            this.Child = _grid;

            // рамка для создания эффекта затемнения
            _shadowBorder = new Border() { Background = Brushes.LightGray, CornerRadius = corners };
            _grid.Children.Add(_shadowBorder);

            setEnabled(false);
        }


        public void SetSizeAndTextOrientation(double width, double height, bool isRenderMargin)
        {
            this.Height = height;
            _dWidthBase = width;
            this.Width = width * (1.0d - _leftMarginKoef);
            bool isVert = (this.Width <= this.Height);
            this.UpdateLayout();

            // вертикальный текст
            if (isVert)
            {
                _viewBox.Width = this.Height; _viewBox.Height = this.Width;

                _viewBox.VerticalAlignment = VerticalAlignment.Bottom;
                _viewBox.HorizontalAlignment = HorizontalAlignment.Left;
                _viewBox.RenderTransformOrigin = new Point(0, 1);
                _viewBox.RenderTransform = new RotateTransform(-90);
            }

            // горизонтальный текст
            else
            {
                _viewBox.Width = this.Width; _viewBox.Height = this.Height;

                _viewBox.RenderTransform = null;
                _viewBox.VerticalAlignment = VerticalAlignment.Center;
                _viewBox.HorizontalAlignment = HorizontalAlignment.Center;
            }

            if (isRenderMargin) RenderMargin();
        }

        public void RenderMargin()
        {
            this.UpdateLayout();
            
            // вертикальный текст
            if (this.Width <= this.Height)
            {
                // L-смещение по горизонтали, чем больше d3, тем правее, d3 лежит между 0 и 1.
                // (если L=0, то текст будет у левой границы админ.панели, если L=1, то текст будет у правой границы)
                // R-для устанения эффекта обрезания при отрисовке горизонтального текста в узком вертикальном контейнере
                // R = ширине viewBox
                double d1 = (_viewBox.Height - _tBlock.ActualHeight) / 2d;
                if (_tBlock.Text.Contains(Environment.NewLine)) d1 /= 1.5d;
                _viewBox.Margin = new Thickness(_viewBox.Height - d1, 0, -_viewBox.Width, 0);

                // отступ самого текста внутри viewBox, чтобы текст не касался краев границы
                d1 = 0.06 * this.Height;
                if (_tBlock.Text.Contains(Environment.NewLine)) d1 *= 1.75d;
                _tBlock.Margin = new Thickness(d1, 0, d1, 0);
            }
            // горизонтальный текст
            else
            {
                _viewBox.Margin = new Thickness(0);
                _tBlock.Margin = new Thickness(0.06 * this.Width, 0, 0.06 * this.Width, 0);
            }
        }

        public void SetStatesSet(KDSUserStatesSet statesSet)
        {
            _statesSet = statesSet;

            _tBlock.Text = statesSet.Name;

            this.Background = _statesSet.BackBrush;
            _tBlock.Foreground = _statesSet.FontBrush;
        }


        private void setEnabled(bool value)
        {
            if (_shadowBorder == null) return;

            _isEnabled = value;

            if (_isEnabled)
            {
                _shadowBorder.Opacity = 0d;
                setWidth(_leftMarginKoef);
            }
            else
            {
                _shadowBorder.Opacity = 0.8d;
                setWidth(1.5d * _leftMarginKoef);
            }
        }

        private void setWidth(double newWidthKoef)
        {
            this.Width = _dWidthBase * (1.0d - newWidthKoef);
        }

    }  // class
}
