using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KDSWPFClient.View
{
    public class AppLeftTabControl: Border
    {
        private const double leftMarginKoef = 0.25d;

        private TextBlock tBlock;

        public AppLeftTabControl(double controlPanelWidth, double height, string text, double topKoef)
        {
            double dBase = controlPanelWidth;
            double tabHeight = dBase * (1.0d - leftMarginKoef);

            Height = height;
            Width = tabHeight;
            BorderBrush = Brushes.DarkGray;
            BorderThickness = new Thickness(2.0d);
            CornerRadius = new CornerRadius(0.2 * dBase, 0, 0, 0.2 * dBase);
            Background = Brushes.Transparent;
            Margin = new Thickness(leftMarginKoef*dBase, (1 + topKoef) * height, 0, 0);
            ClipToBounds = false;

            Canvas canvas = new Canvas()
            {
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Viewbox vBox = new Viewbox()
            {
                Width = height,
                Height = tabHeight,
                Stretch = Stretch.Uniform
            };
            canvas.Children.Add(vBox);

            tBlock = new TextBlock()
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Height = tabHeight, Width = height
            };
            vBox.Child = tBlock;

            canvas.RenderTransformOrigin = new Point(0, 0);
            canvas.RenderTransform = new RotateTransform(-90.0d);

            this.Child = canvas;
        }

        public void SetText(string newText )
        {
            tBlock.Text = newText;
        }

        public void SetForeground(Brush brush)
        {
            tBlock.Foreground = brush;
        }

    }  // class


    /*
                 <Border x:Name="btnDishStatusFilter" Grid.Row="3"
            BorderBrush="DarkGray" BorderThickness="2"
            PreviewMouseDown="tbDishStatusFilter_MouseDown"
            CornerRadius="{Binding ActualWidth, ElementName=grdUserConfig, ConverterParameter=0.2;0;0;0.2, Converter={StaticResource getCornerRadius}, Mode=OneWay}" 
            Margin="{Binding Path=ActualWidth, ElementName=grdUserConfig, Converter={StaticResource getMargin}, ConverterParameter='0.2;0;0;0', Mode=OneWay}"
            >
        <Canvas RenderTransformOrigin="0,0" VerticalAlignment="Bottom">
            <Viewbox Width="{Binding ElementName=btnDishStatusFilter, Path=ActualHeight, Converter={StaticResource addParamConv}, ConverterParameter=-4}" Height="{Binding ElementName=btnDishStatusFilter, Path=ActualWidth, Converter={StaticResource addParamConv}, ConverterParameter=-4}" Stretch="Uniform">
                <TextBlock x:Name="tbDishStatusFilter" Text="В процессе" TextWrapping="Wrap" FontWeight="Bold" Margin="{Binding ElementName=grdUserConfig, Path=ActualWidth, Mode=OneWay, Converter={StaticResource getMargin}, ConverterParameter=0.15;0;0.15;0.07}"/>
            </Viewbox>
            <Canvas.RenderTransform>
                <RotateTransform Angle="-90"/>
            </Canvas.RenderTransform>
        </Canvas>

    </Border>
 */

}
