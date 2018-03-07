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
        private const double leftMarginKoef = 0.15d;

        private double dWidthBase;

        private TextBlock tBlock;
        private Border shadowBorder;
        private Viewbox viewBox;

        public string Text {
            get { return (tBlock==null) ? null : tBlock.Text; }
            set { if (tBlock != null) tBlock.Text = value; }
        }

        private KDSUserStatesSet _statesSet;
        public KDSUserStatesSet StatesSet { get { return _statesSet; } }

        private bool _isEnabled;
        public new bool IsEnabled
        {
            get { return _isEnabled; }
            set { setEnabled(value); }
        }

        // флаг необходимости принудительного вызова метода SetHeight при изменении высоты внешнего контейнера
        public bool IsForceCallSetHeight { get; set; }


        // CTOR
        public AppLeftTabControl(double controlPanelWidth, double height, string text, double topKoef)
        {
            dWidthBase = controlPanelWidth;
            double tabHeight = dWidthBase * (1.0d - leftMarginKoef);

            double dRad = 0.2d * Math.Min(dWidthBase, height);
            CornerRadius corners = new CornerRadius(dRad, 0, 0, dRad);

            Height = height;
            Width = tabHeight;
            BorderBrush = Brushes.DarkGray;
            BorderThickness = new Thickness(2.0d);
            CornerRadius = corners;
            Background = Brushes.Transparent;
            Margin = new Thickness(leftMarginKoef * dWidthBase, topKoef * height, 0, 0);
            base.IsEnabled = true;

            Grid grid = new Grid();

            // канва с вертикальным текстом
            Canvas canvas = new Canvas()
            {
                VerticalAlignment = VerticalAlignment.Bottom
            };
            viewBox = new Viewbox()
            {
                Width = height,
                Height = tabHeight,
                Stretch = Stretch.Uniform
            };
            canvas.Children.Add(viewBox);

            tBlock = new TextBlock()
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0.15 * dWidthBase, 0, 0.15 * dWidthBase, 0.07 * dWidthBase)
            };
            viewBox.Child = tBlock;

            canvas.RenderTransformOrigin = new Point(0, 0);
            canvas.RenderTransform = new RotateTransform(-90.0d);

            grid.Children.Add(canvas);

            // рамка для создания эффекта затемнения
            shadowBorder = new Border() { Background = Brushes.LightGray, CornerRadius = corners };
            grid.Children.Add(shadowBorder);

            this.Child = grid;

            setEnabled(false);
        }


        public void SetHeight(double newHeight)
        {
            viewBox.Width = newHeight;
            this.Height = newHeight;
        }


        public void SetStatesSet(KDSUserStatesSet statesSet)
        {
            _statesSet = statesSet;

            tBlock.Text = statesSet.Name;

            this.Background = _statesSet.BackBrush;
            tBlock.Foreground = _statesSet.FontBrush;
        }


        private void setEnabled(bool value)
        {
            if (shadowBorder == null) return;

            _isEnabled = value;

            if (_isEnabled)
            {
                shadowBorder.Opacity = 0d;
                setWidth(leftMarginKoef);
                tBlock.Margin = new Thickness(0.15 * dWidthBase, 0, 0.15 * dWidthBase, 0.07 * dWidthBase);
            }
            else
            {
                shadowBorder.Opacity = 0.8d;
                setWidth(1.5d * leftMarginKoef);
                tBlock.Margin = new Thickness(0.3 * dWidthBase, 0, 0.3 * dWidthBase, 0.07 * dWidthBase);
            }
        }

        private void setWidth(double newWidthKoef)
        {
            double tabHeight = dWidthBase * (1.0d - newWidthKoef);

            viewBox.Height = tabHeight;
            this.Width = tabHeight;
            this.Margin = new Thickness(newWidthKoef * dWidthBase, this.Margin.Top, 0, 0);
        }

        /*
                         <Border BorderBrush="DarkGray" BorderThickness="2" Height="50" VerticalAlignment="Top" Margin="3" Background="Green" IsEnabled="False">
                    <Grid>
                        <TextBlock Text="QWERTY" TextWrapping="Wrap" Foreground="Yellow" FontWeight="Bold"/>
                        <Border Background="LightGray" Style="{StaticResource leftTabBorderStyle}"/>
                    </Grid>
                </Border>

         
                <Style x:Key="leftTabBorderStyle" TargetType="Border">
            <Style.Triggers>
                <Trigger Property="IsEnabled" Value="True">
                    <Setter Property="Opacity" Value="0"/>
                </Trigger>
                <Trigger Property="IsEnabled" Value="False">
                    <Setter Property="Opacity" Value="0.7"/>
                </Trigger>
            </Style.Triggers>
        </Style>


         */
    }  // class
}
