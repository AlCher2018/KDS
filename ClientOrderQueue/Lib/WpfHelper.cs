using IntegraLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClientOrderQueue.Lib
{

    #region type extensions classes
    public static class UIElementExtensions
    {
        private static Action EmptyDelegate = delegate () { };

        public static void Refresh(this UIElement uiElement)
        {
            uiElement.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
        }
    }  // class UIElementExtensions
    #endregion


    public static class WpfHelper
    {
        #region свойства приложения в WPF форме
        // получить глобальное значение приложения из его свойств
        public static object GetAppGlobalValue(string key, object defaultValue = null)
        {
            IDictionary dict = System.Windows.Application.Current.Properties;
            if (dict == null) return null;

            if (dict.Contains(key) == false) return defaultValue;
            else return dict[key];
        }

        public static bool GetAppGlobalBool(string key, bool defaultValue = false)
        {
            
            return Convert.ToBoolean(GetAppGlobalValue(key));
        }

        // установить глобальное значение приложения (в свойствах приложения)
        public static void SetAppGlobalValue(string key, object value)
        {
            IDictionary dict = System.Windows.Application.Current.Properties;
            if (dict.Contains(key) == false)  // если еще нет значения в словаре
            {
                dict.Add(key, value);   // то добавить
            }
            else    // иначе - изменить существующее
            {
                dict[key] = value;
            }
        }
        #endregion

        #region windows funcs
        // установить размер окна (параметр) в размеры главного окна приложения
        public static void SetWinSizeToMainWinSize(Window win)
        {
            Window mWin = App.Current.MainWindow;

            // размеры
            if (win.Width != mWin.ActualWidth) win.Width = mWin.ActualWidth;
            if (win.Height != mWin.ActualHeight) win.Height = mWin.ActualHeight;

            // положение
            Point topLeftPoint = WpfHelper.GetWindowTopLeftPoint(mWin);
            if (win.Top != topLeftPoint.Y) win.Top = topLeftPoint.Y;
            if (win.Left != topLeftPoint.X) win.Left = topLeftPoint.X;
        }

        public static Point GetWindowTopLeftPoint(Window window)
        {
            double left, top;
            if (window.WindowState == WindowState.Maximized)
            {
                var leftField = typeof(Window).GetField("_actualLeft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                left = (double)leftField.GetValue(window);
            }
            else
                left = window.Left;

            if (window.WindowState == WindowState.Maximized)
            {
                var leftField = typeof(Window).GetField("_actualTop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                top = (double)leftField.GetValue(window);
            }
            else
                top = window.Top;

            return new Point(left, top);
        }


        public static bool IsAppVerticalLayout
        {
            get
            {
                double appWidth = (double)WpfHelper.GetAppGlobalValue("screenWidth");
                double appHeight = (double)WpfHelper.GetAppGlobalValue("screenHeight");
                return (appWidth < appHeight);
            }
        }

        public static bool IsOpenWindow(string typeName, string objName = null)
        {
            bool retVal = false;
            foreach (Window win in Application.Current.Windows)
            {
                if ((win.GetType().Name.Equals(typeName)) && (string.IsNullOrEmpty(objName) ? true : win.Name.Equals(objName)))
                {
                    retVal = (win.Visibility == Visibility.Visible);
                    break;
                }
            }

            return retVal;
        }

        // закрыть все открытые окна, кроме главного окна
        // проще перечислить, какие надо закрывать, а какие прятать
        public static void CloseChildWindows()
        {
            foreach (Window win in Application.Current.Windows)
            {
                Type winType = win.GetType();
                if (winType.Name == "MainWindow") continue;
                PropertyInfo pInfo = winType.GetProperty("Host");
                if (pInfo == null) win.Close();
            }  // for each
        }  // method

        #endregion


        public static FrameworkElement FindVisualParent(FrameworkElement elementFrom, Type findType, string elementName)
        {
            if (elementFrom == null) return null;

            DependencyObject parent = elementFrom;

            bool isContinue; string sName;
            while (parent != null)
            {
                if (!(parent is DependencyObject)) break;

                isContinue = false;
                sName = (parent as FrameworkElement).Name;

                if ((findType != null) && !parent.GetType().Equals(findType)) isContinue = true;
                if (!isContinue && !elementName.IsNull() && !sName.IsNull() && !elementName.Equals(sName)) isContinue = true;

                if (isContinue)
                {
                    parent = VisualTreeHelper.GetParent(parent);

                    if (parent == null) break;
                    if ((parent is Window) || (parent is Page)) { parent = null; break; }
                }
                else
                {
                    break;
                }
            }

            return (parent == null) ? null : parent as FrameworkElement;
        }


        #region draw&measure funcs

        public static double GetRowHeightAbsValue(Grid grid, int iRow, double totalHeight = 0d)
        {
            if (totalHeight == 0d) totalHeight = grid.Height;

            double cntStars = grid.RowDefinitions.Sum(r => r.Height.Value);
            return grid.RowDefinitions[iRow].Height.Value / cntStars * totalHeight;
        }

        public static void AssignFontSizeByMeasureHeight(TextBlock tbAssigning, Size measuredSize, double requiredHeight, bool isSubtractSideMarginsAsFontSize)
        {
            double initWidth = measuredSize.Width;
            if (isSubtractSideMarginsAsFontSize) measuredSize.Width = initWidth - 2d * tbAssigning.FontSize;

            tbAssigning.Measure(measuredSize);
            while (tbAssigning.DesiredSize.Height > requiredHeight)
            {
                tbAssigning.FontSize *= 0.9;
                if (isSubtractSideMarginsAsFontSize) measuredSize.Width = initWidth - 2d * tbAssigning.FontSize;

                tbAssigning.Measure(measuredSize);
            }

            if ((requiredHeight / tbAssigning.DesiredSize.Height) > 1.5)
            {
                tbAssigning.FontSize *= 1.1;
            }
        }

        // вернуть кисть из ресурса приложения
        // если ключа нет, то в зависимости от параметра isDefaultWhite возвращается белая (для фона) или черная (для текста) кисть
        public static Brush GetAppResourcesBrush(string resKey, bool isDefaultWhite = true)
        {
            ResourceDictionary resDict = Application.Current.Resources;
            object resVal = resDict[resKey];

            Brush retVal;
            if (!(resVal is Brush) || (resVal == null))
                retVal = new SolidColorBrush(isDefaultWhite ? Colors.White : Colors.Black);
            else
                retVal = (Brush)resVal;

            return retVal;
        }

        public static Brush GetBrushFromRGBString(string rgb)
        {
            string[] sArr = rgb.Split(',');
            if (sArr.Count() != 3) return new SolidColorBrush(Color.FromRgb(0, 0, 0));

            byte r = 0, g = 0, b = 0;
            byte.TryParse(sArr[0], out r);
            byte.TryParse(sArr[1], out g);
            byte.TryParse(sArr[2], out b);

            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        #endregion

    }  // class
}
