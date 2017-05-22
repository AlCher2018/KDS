using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KDSWPFClient.View
{
    /// <summary>
    /// Interaction logic for StateChange.xaml
    /// </summary>
    public partial class StateChange : Window
    {
        public OrderViewModel Order { get; set; }
        public OrderDishViewModel Dish { get; set; }

        public bool IsShowTitle { get; set; }

        private AppViewModelEnum _modelType;
        // разрешенные переходы
        private List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> _allowedStates;


        public StateChange()
        {
            InitializeComponent();

            this.Loaded += StateChange_Loaded;

            IsShowTitle = true;
        }

        private void StateChange_Loaded(object sender, RoutedEventArgs e)
        {
            setModelType();

            _allowedStates = (List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>)AppLib.GetAppGlobalValue("StatesAllowedForMove");

            setWinLayout();
        }


        private void setWinLayout()
        {
            // размеры 
            //this.Width = (double)AppLib.GetAppGlobalValue("screenWidth");
            //this.Height = (double)AppLib.GetAppGlobalValue("screenHeight");
            KDSWPFClient.MainWindow mWin = (KDSWPFClient.MainWindow)App.Current.MainWindow;
            this.Width = mWin.ActualWidth; this.Height = mWin.ActualHeight;
            Point topLeftPoint = AppLib.GetWindowTopLeftPoint(mWin);
            this.Top = topLeftPoint.Y; this.Left = topLeftPoint.X;

            mainGrid.MinWidth = Width / 2d;
            mainGrid.Height = Height / 2d;
            double titleFontSize = 0.05 * mainGrid.Height;

            // title
            #region title
            switch (_modelType)
            {
                case AppViewModelEnum.None:
                    this.Title = "Изменение состояния ----";
                    break;
                case AppViewModelEnum.Order:
                    this.Title = "Изменение состояния ЗАКАЗА № " + Order.Number;
                    break;
                case AppViewModelEnum.Dish:
                    this.Title = "Изменение состояния БЛЮДА \"" + Dish.DishName + "\"";
                    break;
                default:
                    break;
            }

            if (this.IsShowTitle)
            {
                textTitle.Text = base.Title;
                textTitle.FontSize = titleFontSize;
                textTitle.FontStyle = FontStyles.Italic;
                textTitle.Margin = new Thickness(titleFontSize, 0.3 * titleFontSize, titleFontSize, 0.3 * titleFontSize);
            }
            else
            {
                borderTitle.Visibility = Visibility.Collapsed;
            }
            #endregion

            // message
            double messageFontSize = 1.5 * titleFontSize;
            tbMessage.FontSize = messageFontSize;
            tbMessage.Margin = new Thickness(messageFontSize, 0.5*messageFontSize, messageFontSize, 0.5*messageFontSize);
            runOrderNumber.Text = (Order == null) ? "---" : Order.Number.ToString();
            runDishText.Text = (Dish == null) ? "" : ", блюдо \"" + Dish.DishName + "\"";

            // кнопки переходов
            double pnlWidth = pnlStateButtons.ActualWidth;
            double pnlHeight = pnlStateButtons.ActualHeight;
            tbNoAllowedStates.FontSize = 1.5 * messageFontSize;

            // если есть объект приложения (заказ / блюдо), то настроить кнопки переходов в другие состояния
            if ((_modelType == AppViewModelEnum.Order) || (_modelType == AppViewModelEnum.Dish))
            {
                OrderStatusEnum currentState = (OrderStatusEnum)((_modelType == AppViewModelEnum.Order) ? Order.OrderStatusId : (Dish.DishStatusId ?? 0));
                // из разрешенных переходов выбрать переходы для текущего состояния
                List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> allowedStatesForCurrent  = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>(_allowedStates.Where(states => states.Key == currentState));

                if ((allowedStatesForCurrent == null) || (allowedStatesForCurrent.Count == 0))
                {
                    tbNoAllowedStates.Visibility = Visibility.Visible;
                    pnlStateButtons.Visibility = Visibility.Hidden;
                }
                else
                {
                    tbNoAllowedStates.Visibility = Visibility.Hidden;
                    pnlStateButtons.Visibility = Visibility.Visible;
                }
                // есть переходы для текущего состояния - настроить размеры кнопок переходов
                if (pnlStateButtons.Visibility == Visibility.Visible)
                {
                    int statesCount = allowedStatesForCurrent.Count;
                    if (statesCount == 1)
                    {
                        btnState2.Visibility = Visibility.Collapsed;
                        btnState1.Width = 0.7 * pnlWidth; btnState1.Height = 0.7
                    }
                    else if (statesCount == 2)
                    {

                    }
                    else
                    {

                    }
                }
            }


            // кнопка Закрыть
            double btnCloseFontSize = 0.8 * titleFontSize;
            btnClose.FontSize = btnCloseFontSize;
            btnClose.Margin = new Thickness(btnCloseFontSize);
            btnClose.Padding = new Thickness(1.5*btnCloseFontSize, 0.5* btnCloseFontSize, 1.5* btnCloseFontSize, 0.5* btnCloseFontSize);

        }


        private void setModelType()
        {
            if ((Order == null) && (Dish == null))
            {
                _modelType = AppViewModelEnum.None;
            }
            else if (Order != null)
            {
                _modelType = AppViewModelEnum.Order;
            }
            else if (Dish != null)
            {
                _modelType = AppViewModelEnum.Dish;
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private enum AppViewModelEnum
        {
            None, Order, Dish
        }


    }  // class
}
