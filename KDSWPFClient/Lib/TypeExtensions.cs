using System;
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

}
