using KDSWPFClient.Lib;
using KDSWPFClient.Model;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private AppDataProvider _dataProvider;
        private AppViewModelEnum _modelType;
        private OrderStatusEnum _currentState;

        // разрешенные переходы
        private List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> _allowedStates;


        public StateChange()
        {
            InitializeComponent();

            this.Loaded += StateChange_Loaded;

            _dataProvider = (AppDataProvider)AppLib.GetAppGlobalValue("AppDataProvider");
        }

        private void StateChange_Loaded(object sender, RoutedEventArgs e)
        {
            IsShowTitle = true;
            setModelType();

            _currentState = (OrderStatusEnum)((_modelType == AppViewModelEnum.Order) ? Order.OrderStatusId : Dish.DishStatusId);
            _allowedStates = (List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>)AppLib.GetAppGlobalValue("StatesAllowedForMove");

            setWinLayout();
        }


        // размер шрифта зависит от высоты строки грида!!
        private void setWinLayout()
        {
            // размеры 
            //this.Width = (double)AppLib.GetAppGlobalValue("screenWidth");
            //this.Height = (double)AppLib.GetAppGlobalValue("screenHeight");
            KDSWPFClient.MainWindow mWin = (KDSWPFClient.MainWindow)App.Current.MainWindow;
            this.Width = mWin.ActualWidth; this.Height = mWin.ActualHeight;
            Point topLeftPoint = AppLib.GetWindowTopLeftPoint(mWin);
            this.Top = topLeftPoint.Y; this.Left = topLeftPoint.X;

            mainGrid.Width = Width / 2d;
            mainGrid.Height = Height / 2d;

            // title
            #region title
            switch (_modelType)
            {
                case AppViewModelEnum.None:
                    this.Title = "Изменение состояния ----";
                    break;
                case AppViewModelEnum.Order:
                    this.Title = "Изменение состояния ЗАКАЗА";
                    break;
                case AppViewModelEnum.Dish:
                    this.Title = "Изменение состояния БЛЮДА";
                    break;
                default:
                    break;
            }

            if (this.IsShowTitle)
            {
                double titleFontSize = 0.5d * AppLib.GetRowHeightAbsValue(mainGrid, 0);
                textTitle.Text = base.Title;
                textTitle.FontSize = titleFontSize;
                textTitle.FontStyle = FontStyles.Italic;
                textTitle.Margin = new Thickness(titleFontSize, 0d, 0d, 0d);
            }
            else
            {
                borderTitle.Visibility = Visibility.Collapsed;
            }
            #endregion

            double rowHeight;
            // message
            rowHeight = AppLib.GetRowHeightAbsValue(mainGrid, 1);
            double messageFontSize = 0.3d * rowHeight;
            tbMessage.FontSize = messageFontSize;
            tbMessage.Margin = new Thickness(messageFontSize, 0d, messageFontSize, 0d);
            runOrderNumber.Text = (Order == null) ? "---" : Order.Number.ToString();
            runDishText.Text = (Dish == null) ? "" : ", блюдо \"" + Dish.DishName + "\"";
            runState.Text = StateGraphHelper.GetStateDescription(_currentState, (Order != null));
            AppLib.AssignFontSizeByMeasureHeight(tbMessage, new Size(mainGrid.Width, mainGrid.Height), rowHeight, true);

            // кнопки переходов
            if ((_modelType == AppViewModelEnum.Order) || (_modelType == AppViewModelEnum.Dish)) createChangeStateButtons();

            // кнопка Закрыть
            double btnCloseFontSize = 0.3 * AppLib.GetRowHeightAbsValue(mainGrid, 3);
            btnClose.FontSize = btnCloseFontSize;
            btnClose.Margin = new Thickness(btnCloseFontSize, 0, btnCloseFontSize, btnCloseFontSize);
            btnClose.Padding = new Thickness(2d*btnCloseFontSize, 0d, 2d * btnCloseFontSize, 0);
        }

        private void createChangeStateButtons()
        {
            double rowHeight = AppLib.GetRowHeightAbsValue(mainGrid, 2);
            // сообщение об отсутствии переходов
            tbNoAllowedStates.FontSize = Math.Floor(0.15d * rowHeight);

            // из РАЗРЕШЕННЫХ переходов выбрать переходы, ДОСТУПНЫЕ для текущего состояния
            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> allowedStatesForCurrentState = null;
            if (_allowedStates != null) allowedStatesForCurrentState = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>(_allowedStates.Where(states => states.Key == _currentState));

            if ((allowedStatesForCurrentState == null) || (allowedStatesForCurrentState.Count == 0))
            {
                // нет доступных переходов
                tbNoAllowedStates.Visibility = Visibility.Visible;
                return;
            }
            else
            {
                pnlStateButtons.Visibility = Visibility.Visible;
            }

            // есть переходы для текущего состояния - настроить размеры кнопок переходов
            // количество кнопок смены состояния
            int statesCount = allowedStatesForCurrentState.Count;
            double pnlWidth = mainGrid.Width;
            double pnlHeight = AppLib.GetRowHeightAbsValue(mainGrid, 2);
            // высота кнопок всегда одинаковая, а ширина и расстояние по горизонтали зависит от их кол-ва
            // контейнер для кнопок уже центрирован по верт.и гориз. в своем контейнере, поэтому верт.поля не нужны
            double height = Math.Floor(0.75 * pnlHeight), width, horMargin;
            // расчет ширины кнопки
            double k = (statesCount == 1) ? 0.5 : 0.2;  // доля гор.поля между кнопками от ширины кнопки
            // для случая [поле][кнопка][поле][кнопка]...[поле]     x = y / ((1 + k) * c + k), 
            // для случая [кнопка][поле][кнопка][поле]...[кнопка]   x = y / (c + kc - k) = y / (c + k(c - 1))
            // где x - ширина кнопки, y - ширина контейнера для кнопки, k - доля гор.поля между кнопками от ширины кнопки, c - количество кнопок
            width = pnlWidth / ((1d + k) * statesCount + k);
            horMargin = k * width;
            Border btnState; int iBtn = 0;
            foreach (KeyValuePair<OrderStatusEnum, OrderStatusEnum> item in allowedStatesForCurrentState)
            {
                // возврат из Готов в Приготовление
                bool isReturnCooking = ((item.Key == OrderStatusEnum.Ready) && (item.Value == OrderStatusEnum.Cooking));

                iBtn++;  // номер кнопки
                btnState = getStateButton(width, height, ((iBtn==1)?0:horMargin), item.Value, isReturnCooking);
                pnlStateButtons.Children.Add(btnState);
            }
        }

        // buttonsCount - колво кнопок для настройки их ширины
        private Border getStateButton(double width, double height, double horMargin, OrderStatusEnum eState, bool isReturnCooking)
        {
            // получить фон и цвет шрифта
            SolidColorBrush backgroundBrush = null, foregroundBrush = null;
            StateGraphHelper.SetStateButtonBrushes(eState, out backgroundBrush, out foregroundBrush);
            // и надписи на кнопке
            string btnText1, btnText2;
            StateGraphHelper.SetStateButtonTexts(eState, out btnText1, out btnText2, (_modelType== AppViewModelEnum.Order), isReturnCooking);

            Border retBorder = new Border()
            {
                Width = width, Height = height, BorderThickness = new Thickness(2d), BorderBrush = Brushes.DarkGray,
                CornerRadius = new CornerRadius(0.1 * height),
                Margin = new Thickness(horMargin, 0d, 0d, 0d),
                Background = backgroundBrush
            };
            retBorder.SetValue(TextBlock.ForegroundProperty, foregroundBrush);
            retBorder.Tag = eState;
            retBorder.MouseUp += buttonState_MouseUp;

            Grid grd = new Grid();
            grd.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1d, GridUnitType.Star) });
            grd.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1d, GridUnitType.Star) });

            TextBlock tbStateName = new TextBlock()
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = btnText1,
                FontWeight = FontWeights.Bold,
                FontSize = Math.Floor(0.2d * height)
            };
            tbStateName.SetValue(Grid.RowProperty, 0);
            grd.Children.Add(tbStateName);

            double fontSize = Math.Floor(0.15d * height);
            TextBlock tbStateDescr = new TextBlock()
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = btnText2,
                FontSize = fontSize,
                Margin = new Thickness(FontSize, 0, FontSize, 0),
                TextWrapping = TextWrapping.Wrap
            };
            AppLib.AssignFontSizeByMeasureHeight(tbStateDescr, new Size(width, height), height / 2d, true);

            tbStateDescr.SetValue(Grid.RowProperty, 1);
            grd.Children.Add(tbStateDescr);

            retBorder.Child = grd;
            return retBorder;
        }

        private void buttonState_MouseUp(object sender, MouseButtonEventArgs e)
        {
            OrderStatusEnum requiredState = (OrderStatusEnum)(sender as Border).Tag;
            //            MessageBox.Show(requiredState.ToString());

            try
            {
                if (_modelType == AppViewModelEnum.Order)
                {
                    _dataProvider.SetNewOrderStatus(Order.Id, requiredState);
                }
                else if (_modelType == AppViewModelEnum.Dish)
                {
                    _dataProvider.SetNewDishStatus(Order.Id, Dish.Id, requiredState);
                }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                MessageBox.Show(ex.Message);
            }
            Close();
        }

        private void setModelType()
        {
            if ((Order == null) && (Dish == null))
            {
                _modelType = AppViewModelEnum.None;
            }
            else if (Dish != null)
            {
                _modelType = AppViewModelEnum.Dish;
            }
            else 
            {
                _modelType = AppViewModelEnum.Order;
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


    // добавить к кнопке DependencyProperty для стилевых триггеров, устанавливающих фон и цвет текста
    public class StateButton : Border
    {
        public OrderStatusEnum Status
        {
            get { return (OrderStatusEnum)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Status.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(OrderStatusEnum), typeof(StateButton), new PropertyMetadata(OrderStatusEnum.None));
    }

}
