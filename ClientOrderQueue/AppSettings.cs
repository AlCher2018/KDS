using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ClientOrderQueue
{
    public static class AppSettings
    {
        public static Brush BackgroundColor;

        static AppSettings()
        {
            // dark pink     R="122" G="34" B="104" A="255"
            BackgroundColor = new SolidColorBrush(Color.FromArgb(255, 122, 34, 104));
        }
    }
}
