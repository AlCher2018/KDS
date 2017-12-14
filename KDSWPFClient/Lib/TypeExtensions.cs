using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace KDSWPFClient.Lib
{
    public static class UIElementExtensions
    {
        private static Action EmptyDelegate = delegate () { };

        public static void Refresh(this UIElement uiElement)
        {
            uiElement.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
        }
    }  // class UIElementExtensions

    public static class DateTimeExtension
    {
        public static string ToPanelString(this DateTime source)
        {
            string retVal = null;

            if (source.Equals(DateTime.MinValue))
                retVal = "no data";
            
            // показать время и дату создания заказа
            else if (DateTime.Now.Day != source.Day)  
            {
                retVal = source.ToString("dd.MM.yyyy HH:mm:ss");
            }

            // показать только время создания заказа
            else
            {
                retVal = source.ToString("HH:mm:ss");
            }

            return retVal;
        } // ToPanelString

    }  // class DateTimeExtensions
}
