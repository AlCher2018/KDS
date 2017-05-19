using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace KDSWPFClient.View
{
    public class SetHorizontalOffset
    {
        public static double GetOffset(DependencyObject obj)
        {
            return (double)obj.GetValue(OffsetProperty);
        }

        public static void SetOffset(DependencyObject obj, double value)
        {
            obj.SetValue(OffsetProperty, value);
        }

        // Using a DependencyProperty as the backing store for Offset.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OffsetProperty =
            DependencyProperty.RegisterAttached("Offset", typeof(double),
            typeof(SetHorizontalOffset), new UIPropertyMetadata(0.0, OnOffsetChanged));


        private static void OnOffsetChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ScrollViewer viewer = d as ScrollViewer;

            if (d != null)
            {
                viewer.ScrollToHorizontalOffset((double)e.NewValue);
            }
        }

    }  // class
}
