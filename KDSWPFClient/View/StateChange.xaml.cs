using IntegraWPFLib;
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

        private AppViewModelEnum _modelType;
        private List<OrderStatusEnum> _allowedStates;  // разрешенные переходы
        public List<OrderStatusEnum> AllowedStates { set { _allowedStates = value; } }

        private OrderStatusEnum _currentState;
        public OrderStatusEnum CurrentState
        {
            get { return _currentState; }
            set { _currentState = value; }
        }


        public StateChange()
        {
            InitializeComponent();
        }

        protected override void OnActivated(EventArgs e)
        {
            IsShowTitle = true;
            if ((Order == null) && (Dish == null)) _modelType = AppViewModelEnum.None;
            else if (Dish != null) _modelType = AppViewModelEnum.Dish;
            else _modelType = AppViewModelEnum.Order;

            setWinLayout();

            base.OnActivated(e);
            pnlStateButtons.Visibility = Visibility.Visible;
        }

        // размер шрифта зависит от высоты строки грида!!
        private void setWinLayout()
        {
            if (mainGrid.Width != 0.5d * Width) mainGrid.Width = 0.5d * Width;
            if (mainGrid.Height != 0.5d * Height) mainGrid.Height = 0.5d * Height;

            // title
            setTitle();
            // main message
            setMainMessage();
            // кнопки переходов
            setChangeStateButtons();

            // кнопка Закрыть
            double btnCloseFontSize = 0.3 * WpfHelper.GetRowHeightAbsValue(mainGrid, 3);
            if (btnClose.FontSize != btnCloseFontSize)
            {
                btnClose.FontSize = btnCloseFontSize;
                btnClose.Margin = new Thickness(btnCloseFontSize, 0, btnCloseFontSize, btnCloseFontSize);
                btnClose.Padding = new Thickness(2d * btnCloseFontSize, 0d, 2d * btnCloseFontSize, 0);
            }
        }

        private void setTitle()
        {
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
                double titleFontSize = 0.5d * WpfHelper.GetRowHeightAbsValue(mainGrid, 0);
                if (textTitle.Text != base.Title) textTitle.Text = base.Title;
                if (textTitle.FontSize != titleFontSize)
                {
                    textTitle.FontSize = titleFontSize;
                    textTitle.Margin = new Thickness(titleFontSize, 0d, 0d, 0d);
                }
            }
            else
            {
                borderTitle.Visibility = Visibility.Collapsed;
            }
        }

        private void setMainMessage()
        {
            double rowHeight;
            rowHeight = WpfHelper.GetRowHeightAbsValue(mainGrid, 1);
            // размер шрифта
            double messageFontSize = (Dish == null) ? 0.3d * rowHeight : 0.2d * rowHeight;
            if (tbMessage.FontSize != messageFontSize)
            {
                tbMessage.FontSize = messageFontSize;
                tbMessage.Margin = new Thickness(messageFontSize, 0d, messageFontSize, 0d);
            }

            // текст
            string preText = tbMessage.Text;
            string sMsg = (Order == null) ? "---" : Order.Number.ToString();
            if (runOrderNumber.Text != sMsg) runOrderNumber.Text = sMsg;
            FontWeight fw = (Dish == null) ? FontWeights.Bold : FontWeights.Normal;
            if (runOrderNumber.FontWeight != fw) runOrderNumber.FontWeight = fw;

            sMsg = (Dish == null) ? "" : ", блюдо \"" + Dish.DishName + "\"";
            if (runDishText.Text != sMsg) runDishText.Text = sMsg;
            fw = (Dish == null) ? FontWeights.Normal : FontWeights.Bold;
            if (runDishText.FontWeight != fw) runDishText.FontWeight = fw;

            sMsg = StateGraphHelper.GetStateDescription(_currentState, (Order != null));
            if (runState.Text != sMsg) runState.Text = sMsg;

            if (preText != tbMessage.Text) WpfHelper.AssignFontSizeByMeasureHeight(tbMessage, new Size(mainGrid.Width, mainGrid.Height), rowHeight, true);
        }

        private void setChangeStateButtons()
        {
            double rowHeight = WpfHelper.GetRowHeightAbsValue(mainGrid, 2);

            // количество кнопок смены состояния
            int buttonsCount = _allowedStates.Count;
            double height=0d, width=0d, horMargin=0d;

            // кол-во кнопок изменилось - заново пересчитать их размер и поля
            if (buttonsCount != pnlStateButtons.Children.Count)
            {
                pnlStateButtons.Children.Clear();
                double pnlWidth = mainGrid.Width;
                double pnlHeight = WpfHelper.GetRowHeightAbsValue(mainGrid, 2);
                // высота кнопок всегда одинаковая, а ширина и расстояние по горизонтали зависит от их кол-ва
                // контейнер для кнопок уже центрирован по верт.и гориз. в своем контейнере, поэтому верт.поля не нужны
                height = Math.Floor(0.75 * pnlHeight);
                // расчет ширины кнопки
                // доля гор.поля между кнопками от ширины кнопки
                // для случая [поле][кнопка][поле][кнопка]...[поле]     x = y / ((1 + k) * c + k), 
                // для случая [кнопка][поле][кнопка][поле]...[кнопка]   x = y / (c + kc - k) = y / (c + k(c - 1))
                // где x - ширина кнопки, y - ширина контейнера для кнопки, k - доля гор.поля между кнопками от ширины кнопки, c - количество кнопок
                double k = (buttonsCount == 1) ? 0.5 : 0.2;
                width = Math.Floor(pnlWidth / ((1d + k) * buttonsCount + k));
                horMargin = Math.Floor(k * width);  // расстояние между кнопками
            }

            Border btnState; int iBtn = 0;
            foreach (OrderStatusEnum item in _allowedStates)
            {
                iBtn++;  // номер кнопки
                if (iBtn > pnlStateButtons.Children.Count)  // добавить кнопку
                {
                    btnState = createStateButton(width, height, ((iBtn == 1) ? 0 : horMargin), item);
                    pnlStateButtons.Children.Add(btnState);
                }

                changeStateButton(iBtn-1, item);

            }  // states loop
        }  // method

        // просто изменить текст и кисти кнопки, если надо
        private void changeStateButton(int buttonIndex, OrderStatusEnum eState)
        {
            Border border = (Border)pnlStateButtons.Children[buttonIndex];
            OrderStatusEnum tagStatus = OrderStatusEnum.None;
            // проверить статус в теге кнопки
            if ((border.Tag != null) && (border.Tag is OrderStatusEnum))
            {
                tagStatus = (OrderStatusEnum)border.Tag;
                //if (tagStatus == eState) return;  // выйти, если кнопка с таким же статусом
            }

            // если статус изменился, то поменять кисти
            if (tagStatus != eState)
            {
                border.Tag = eState;
                // получить фон и цвет шрифта
                Brush backgroundBrush = null, foregroundBrush = null;
                StateGraphHelper.SetStateButtonBrushes(eState, out backgroundBrush, out foregroundBrush);
                border.Background = backgroundBrush;
                border.SetValue(TextBlock.ForegroundProperty, foregroundBrush);
            }

            // и надписи на кнопке
            string btnText1, btnText2;
            // возврат из Готов в Приготовление
            bool isReturnCooking = (((_currentState == OrderStatusEnum.Ready) && (eState == OrderStatusEnum.Cooking))
                || ((_currentState == OrderStatusEnum.ReadyConfirmed) && (eState == OrderStatusEnum.Cooking)));
            StateGraphHelper.SetStateButtonTexts(eState, out btnText1, out btnText2, (_modelType == AppViewModelEnum.Order), isReturnCooking);
            Grid grd = (Grid)border.Child;
            TextBlock tbStateName = (TextBlock)grd.Children[0], tbStateDescr = (TextBlock)grd.Children[1];
            Size tbSize = new Size(border.Width, border.Height);
            double reqHeight = border.Height / 2d;

            if (tbStateName.Text != btnText1)
            {
                tbStateName.Text = btnText1;
                WpfHelper.AssignFontSizeByMeasureHeight(tbStateName, tbSize, reqHeight, true);
            }
            if (tbStateDescr.Text != btnText2)
            {
                tbStateDescr.Text = btnText2;
                WpfHelper.AssignFontSizeByMeasureHeight(tbStateDescr, tbSize, reqHeight, true);
            }
        }

        // создать новую кнопку статуса
        private Border createStateButton(double width, double height, double horMargin, OrderStatusEnum eState)
        {
            Border retBorder = new Border()
            {
                Width = width, Height = height, BorderThickness = new Thickness(2d), BorderBrush = Brushes.DarkGray,
                CornerRadius = new CornerRadius(0.1 * height),
                Margin = new Thickness(horMargin, 0d, 0d, 0d)
            };

            retBorder.MouseUp += buttonState_MouseUp;

            Grid grd = new Grid();
            grd.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1d, GridUnitType.Star) });
            grd.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1d, GridUnitType.Star) });

            double fontSize = Math.Floor(0.2d * Math.Min(height, width));
            TextBlock tbStateName = new TextBlock()
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = fontSize,
                TextWrapping = TextWrapping.Wrap
            };
            tbStateName.SetValue(Grid.RowProperty, 0);
            grd.Children.Add(tbStateName);

            TextBlock tbStateDescr = new TextBlock()
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 0.8d * fontSize,
                Margin = new Thickness(FontSize, 0, FontSize, 0),
                TextWrapping = TextWrapping.Wrap
            };
            tbStateDescr.SetValue(Grid.RowProperty, 1);
            grd.Children.Add(tbStateDescr);

            retBorder.Child = grd;

            return retBorder;
        }

        private void buttonState_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // новый статус
            _currentState = (OrderStatusEnum)(sender as Border).Tag;
            closeWin();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            _currentState = OrderStatusEnum.None;
            closeWin();
        }

        private void closeWin()
        {
            this.Order = null;
            this.Dish = null;
            pnlStateButtons.Visibility = Visibility.Hidden;

            this.Hide();
        }

        private enum AppViewModelEnum
        {
            None, Order, Dish
        }

    }  // class

}
