using IntegraLib;
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

namespace KDSWPFClient.View
{
#if notUserControl
    public class DishPanel: Border
    {
#region fields & properties
        private OrderDishViewModel _dishView;
        internal OrderDishViewModel DishView { get { return _dishView; } }

        private bool _isDish, _isIngrIndepend;
        private double _padd;
        private string _currentBrushKey;
        //private DishPanel _parentPanel;
        private Border brdTimer;
        private TextBlock tbDishStatusTS;
#endregion


        // CTOR
        public DishPanel(OrderDishViewModel dishView, double panelWidth)
        {
            this.Width = panelWidth;
            this.SnapsToDevicePixels = true;
            this.BorderBrush = Brushes.DarkBlue;
            this.BorderThickness = new System.Windows.Thickness(1,0,1,1);
            this.MouseUp += root_MouseUp;

            double fontScale = (double)WpfHelper.GetAppGlobalValue("AppFontScale", 1.0d);
            double fontSizeDishName = (double)WpfHelper.GetAppGlobalValue("ordPnlDishNameFontSize");

            _padd = 0.5 * fontSizeDishName;  // от размера фонта наименования блюда
            this.Padding = new Thickness(0, 0.5 * _padd, 0, 0.5 * _padd);

            _dishView = dishView;
            _isDish = _dishView.ParentUID.IsNull();  // признак блюда
            _isIngrIndepend = (bool)WpfHelper.GetAppGlobalValue("IsIngredientsIndependent", false);
            _dishView.PropertyChanged += DishView_PropertyChanged;

            Grid grdDishLine = new Grid();
            this.Child = grdDishLine;
            // содержание грида - 4 строки
            // 0. № п/п
            grdDishLine.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(8d, GridUnitType.Star) });
            // 1. наименование блюда
            grdDishLine.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(43d, GridUnitType.Star) });
            // 2. количество
            grdDishLine.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(15d, GridUnitType.Star) });
            // 3. таймер состояния
            grdDishLine.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(34d, GridUnitType.Star) });

            // индекс блюда
            TextBlock tbDishIndex = new TextBlock() { TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            tbDishIndex.SetValue(Grid.ColumnProperty, 0);
            Binding bind = new Binding("Index") { Source = _dishView, Converter = new IsZeroConverter() };
            tbDishIndex.SetBinding(TextBlock.TextProperty, bind);
            tbDishIndex.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlDishIndexFontSize");
            grdDishLine.Children.Add(tbDishIndex);

            // имя блюда: текст и комментарий
            TextBlock tbDish = new TextBlock() { TextAlignment = TextAlignment.Left, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            tbDish.SetValue(Grid.ColumnProperty, 1);
            Run tbDishName = new Run() { FontWeight = FontWeights.Bold };
            //   блюдо
            BrushesPair brPair;
            if (_isDish)
            {
                tbDishName.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlDishNameFontSize");
                brPair = BrushHelper.AppBrushes["dishLineBase"];
            }
            //    ингредиент
            else
            {
                tbDishName.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlIngrNameFontSize");
                brPair = BrushHelper.AppBrushes["ingrLineBase"];
            }
            if (brPair != null)
            {
                this.Background = brPair.Background;
                tbDishName.Foreground = brPair.Foreground;
            }
            bind = new Binding("DishName") { Source = _dishView };
            tbDishName.SetBinding(Run.TextProperty, bind);
            tbDish.Inlines.Add(tbDishName);
            Run tbComment = new Run() { FontWeight = FontWeights.Normal, FontStyle = FontStyles.Italic };
            bind = new Binding("Comment") { Source = _dishView };
            tbComment.SetBinding(Run.TextProperty, bind);
            tbDish.Inlines.Add(tbComment);
            // модификаторы
            if (dishView.Comment.IsNull() == false)
            {
                tbComment.Text = string.Format("\n({0})", dishView.Comment);
                tbComment.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlDishCommentFontSize");
            }
            grdDishLine.Children.Add(tbDish);

            // количество
            TextBlock tbDishQuantity = new TextBlock() { TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
            tbDishQuantity.SetValue(Grid.ColumnProperty, 2);
            bind = new Binding("Quantity") { Source=_dishView, Converter = new DishQuantityToStringConverter()};
            tbDishQuantity.SetBinding(TextBlock.TextProperty, bind);
            tbDishQuantity.Margin = new Thickness(0, 0, 3, 0);
            tbDishQuantity.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlDishQuantityFontSize");
            grdDishLine.Children.Add(tbDishQuantity);

            // рамка таймера
            brdTimer = new Border();
            brdTimer.SetValue(Grid.ColumnProperty, 3);
            brdTimer.Padding = new Thickness(0, _padd, 0, _padd);
            brdTimer.Margin = new Thickness(0, 0, 3, 0);
            double timerCornerRadius = 0.015 * this.Width;
            brdTimer.CornerRadius = new CornerRadius(timerCornerRadius, timerCornerRadius, timerCornerRadius, timerCornerRadius);
            // текстовый блок таймера
            tbDishStatusTS = new TextBlock() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap };
            bind = new Binding("ViewTimerString") {Source = _dishView };
            tbDishStatusTS.SetBinding(TextBlock.TextProperty, bind);
            tbDishStatusTS.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlDishTimerFontSize");
            brdTimer.Child = tbDishStatusTS;

            grdDishLine.Children.Add(brdTimer);

            // рамка вокруг таймера
            setTimerBorder();
        }


        // изменение свойств блюда - обновить кисти
        private void DishView_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // поменялся статус - однозначно меняем кисти
            if ((e.PropertyName == "Status") || (e.PropertyName == "ViewTimerString"))
            {
                setTimerBorder();
            }
        }

        // установка рамки вокруг таймера
        private void setTimerBorder()
        {
            string brushKey = _dishView.Status.ToString();
            // состояние "Ожидание" начала готовки
            if (_dishView.Status == OrderStatusEnum.WaitingCook)
            {
                // если есть "Готовить через" - отображаем время начала автомат.перехода в сост."В процессе" по убыванию
                if (_dishView.DelayedStartTime != 0)
                {
                    brushKey = "estimateStart";
                }
                // если есть время приготовления, то отобразить время приготовления
                else if (_dishView.EstimatedTime != 0)
                {
                    brushKey = "estimateCook";
                }
            }
            else if ((_dishView.Status == OrderStatusEnum.Ready) && _dishView.EnableTimerToAutoReadyConfirm)
            {
                brushKey = OrderStatusEnum.ReadyConfirmed.ToString() + OrderStatusEnum.Ready.ToString();
            }
            else
            {
                bool negativeTimer = (!_dishView.ViewTimerString.IsNull() && _dishView.ViewTimerString.StartsWith("-"));
                // проверить на наличие кистей для отрицательных значений
                if (negativeTimer) brushKey += "minus";
            }

            if (brushKey != _currentBrushKey)
            {
                _currentBrushKey = brushKey;

                Dictionary<string, BrushesPair> appBrushes = BrushHelper.AppBrushes;
                BrushesPair brPair = null;
                if (appBrushes.ContainsKey(_currentBrushKey)) brPair = appBrushes[_currentBrushKey];

                if (brPair != null)
                {
                    brdTimer.Background = brPair.Background;
                    tbDishStatusTS.Foreground = brPair.Foreground;
                }
            }
        }


        // клик по строке блюда/ингредиента
        private void root_MouseUp(object sender, MouseButtonEventArgs e)
        {
            string sLogMsg = "click on order ITEM";

            // КЛИКАБЕЛЬНОСТЬ (условия отсутствия)
            // 1. не входит в отображаемые отделы
            if (AppLib.IsDepViewOnKDS(_dishView.DepartmentId) == false)
            {
                AppLib.WriteLogClientAction(sLogMsg + " - NO action (dep not view)");
                return;
            }

            // 2. условие кликабельности ингредиента (независимо от блюда) или блюда на допнаправлении
            else if (!_isDish)
            {
                // IsIngredientsIndependent может меняться динамически, поэтому проверяем каждый раз
                bool b1 = (bool)WpfHelper.GetAppGlobalValue("IsIngredientsIndependent", false);
                if (!b1)
                {
                    AppLib.WriteLogClientAction(sLogMsg + " - NO action (клик по ингр/допНП не разрешен в IsIngredientsIndependent)");
                    return;
                }
            }

            OrderViewModel orderView = null;
            FrameworkElement orderPanel = WpfHelper.FindVisualParent(this, typeof(OrderPanel), null);
            if (orderPanel != null) orderView = (orderPanel as OrderPanel).OrderViewModel;

            AppLib.WriteLogClientAction("{0} - open StateChange window for dishId {1} ({2})", sLogMsg, _dishView.Id, _dishView.DishName);

            App.OpenStateChangeWindow(orderView, _dishView);

            e.Handled = true;
        }


    }  // class DishPAnelNew
#endif
}
