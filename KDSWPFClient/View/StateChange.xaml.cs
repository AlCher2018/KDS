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
        }

        private void StateChange_Loaded(object sender, RoutedEventArgs e)
        {
            IsShowTitle = true;
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
                // текущее состояние
                OrderStatusEnum currentState = (OrderStatusEnum)((_modelType == AppViewModelEnum.Order) ? Order.OrderStatusId : (Dish.DishStatusId ?? 0));
                // из разрешенных переходов выбрать переходы для текущего состояния
                List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> allowedStatesForCurrentState  = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>(_allowedStates.Where(states => states.Key == currentState));

                if ((allowedStatesForCurrentState == null) || (allowedStatesForCurrentState.Count == 0))
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
                    int statesCount = allowedStatesForCurrentState.Count;
                    // количество кнопок смены состояния
                    // 1 - одна, большая, 2 - две на стандартную ширину, 3 и более - расширение окна по горизонтали, добавление в StackPanel
                    if (statesCount == 1)
                    {
                        Border btnState = getStateButton(0.7 * pnlWidth, 0.7 * pnlHeight, allowedStatesForCurrentState[0].Value);
                        double horMargin = Math.Floor(0.15 * pnlWidth);
                        btnState.Margin = new Thickness(horMargin, 0.15d * pnlHeight, horMargin, 0.15d * pnlHeight);
                        pnlStateButtons.Children.Add(btnState);
                    }
                    else if (statesCount == 2)
                    {
                        Border btnState = null;
                        foreach (KeyValuePair<OrderStatusEnum, OrderStatusEnum> item in allowedStatesForCurrentState)
                        {
                            btnState = getStateButton(0.35 * pnlWidth, 0.7 * pnlHeight, item.Value);
                            btnState.Margin = new Thickness(0.1d * pnlWidth, 0.15d * pnlHeight, 0.1d * pnlWidth, 0.15d * pnlHeight);
                            pnlStateButtons.Children.Add(btnState);
                        }
                    }
                    else
                    {
                        Border btnState = null;
                        double wBtn = 0.35d * pnlWidth, mBtn = 0.1d * pnlWidth;
                        this.Width = (wBtn + mBtn) * allowedStatesForCurrentState.Count + 0.1d * pnlWidth;
                        foreach (KeyValuePair<OrderStatusEnum, OrderStatusEnum> item in allowedStatesForCurrentState)
                        {
                            btnState = getStateButton(0.35 * pnlWidth, 0.7 * pnlHeight, item.Value);
                            btnState.Margin = new Thickness(0.1d * pnlWidth, 0.15d * pnlHeight, 0.1d * pnlWidth, 0.15d * pnlHeight);
                            pnlStateButtons.Children.Add(btnState);
                        }
                    }
                }
            }


            // кнопка Закрыть
            double btnCloseFontSize = 0.8 * titleFontSize;
            btnClose.FontSize = btnCloseFontSize;
            btnClose.Margin = new Thickness(btnCloseFontSize);
            btnClose.Padding = new Thickness(1.5*btnCloseFontSize, 0.5* btnCloseFontSize, 1.5* btnCloseFontSize, 0.5* btnCloseFontSize);

        }

        private Border getStateButton(double width, double height, OrderStatusEnum eState)
        {
            double cornerRadius = 0.1 * height;

            Border retBorder = new Border()
            {
                Width = width, Height = height, BorderThickness = new Thickness(2d), BorderBrush = Brushes.DarkGray,
                CornerRadius = new CornerRadius(cornerRadius)
            };

            Grid grd = new Grid();
            double hRow = 0.4 * height;
            grd.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(hRow, GridUnitType.Pixel) });
            grd.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0.2*height, GridUnitType.Pixel) });
            grd.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(hRow, GridUnitType.Pixel) });

            TextBlock tbStateName = new TextBlock()
            {
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Text = getStateButtonText(eState).ToUpper(), FontWeight = FontWeights.Bold, FontSize = 0.2d * height
            };
            tbStateName.SetValue(Grid.RowProperty, 0);
            grd.Children.Add(tbStateName);

            Viewbox vbDescr = new Viewbox()
            {
                Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Width = width, Height = hRow,
                Margin = new Thickness(cornerRadius),
                Child = new TextBlock()
                {
                    TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
                    Text = getStateDescr(eState)
                }
            };
            vbDescr.SetValue(Grid.RowProperty, 2);
            grd.Children.Add(vbDescr);


            retBorder.Child = grd;
            return retBorder;
        }

        private string getStateDescr(OrderStatusEnum eState)
        {
            string retVal = null;
            switch (eState)
            {
                case OrderStatusEnum.None:
                    break;
                case OrderStatusEnum.WaitingCook:
                    break;
                case OrderStatusEnum.Cooking:
                    retVal = "Начать приготовление блюда/заказа";
                    break;
                case OrderStatusEnum.Ready:
                    retVal = "Закончить приготовление блюда/заказа";
                    break;
                case OrderStatusEnum.Took:
                    retVal = "Забрать блюдо/заказ и отнести его Клиенту";
                    break;
                case OrderStatusEnum.Cancelled:
                    retVal = "Отменить приготовление блюда/заказа";
                    break;
                case OrderStatusEnum.Commit:
                    retVal = "Зафиксировать, т.е. запретить изменять статус блюда/заказа";
                    break;
                case OrderStatusEnum.CancelConfirmed:
                    retVal = "Подтвердить отмену блюда/заказа";
                    break;
                default:
                    break;
            }

            return retVal;
        }

        private string getStateButtonText(OrderStatusEnum eState)
        {
            string retVal = null;
            switch (eState)
            {
                case OrderStatusEnum.None:
                    break;
                case OrderStatusEnum.WaitingCook:
                    retVal = "Ожидание";
                    break;
                case OrderStatusEnum.Cooking:
                    retVal = "Готовить";
                    break;
                case OrderStatusEnum.Ready:
                    retVal = "Закончить";
                    break;
                case OrderStatusEnum.Took:
                    retVal = "Забрать";
                    break;
                case OrderStatusEnum.Cancelled:
                    retVal = "Отменить";
                    break;
                case OrderStatusEnum.Commit:
                    retVal = "Зафиксировать";
                    break;
                case OrderStatusEnum.CancelConfirmed:
                    retVal = "Подтвердить отмену";
                    break;
                default:
                    break;
            }
            return retVal;
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
