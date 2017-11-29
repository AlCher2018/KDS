﻿using IntegraLib;
using KDSWPFClient.Lib;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KDSWPFClient.View
{
    /// <summary>
    /// Interaction logic for DishPanel.xaml
    /// </summary>
    public partial class DishPanel : UserControl
    {
        private OrderDishViewModel _dishView;
        internal OrderDishViewModel DishView { get { return _dishView; } }

        private bool _isDish, _isIngrIndepend;
        private double _fontSize, _padd;
        private string _currentBrushKey;
        //private DishPanel _parentPanel;


        public DishPanel(OrderDishViewModel dishView)  // DishPanel parentPanel = null
        {
            InitializeComponent();

            _dishView = dishView;
            //_parentPanel = parentPanel;
            grdDishLine.DataContext = _dishView;

            _isDish = _dishView.ParentUID.IsNull();  // признак блюда
            _isIngrIndepend = (bool)WpfHelper.GetAppGlobalValue("IsIngredientsIndependent", false);

            dishView.PropertyChanged += DishView_PropertyChanged;

            //double dishLineMinHeight = (double)AppLib.GetAppGlobalValue("ordPnlDishLineMinHeight");
            //base.MinHeight = dishLineMinHeight;

            double fontScale = (double)WpfHelper.GetAppGlobalValue("AppFontScale",1.0d);
            double fontSize = (double)WpfHelper.GetAppGlobalValue("ordPnlDishLineFontSize"); // 12d
            _fontSize = fontSize * fontScale;

            // на уровне всего элемента для всех TextBlock-ов  - НЕЛЬЗЯ!!! т.к. Measure() неправильно считает размер!
            // this.SetValue(TextBlock.FontSizeProperty, _fontSize);   
            this.tbDishIndex.FontSize = 0.8 * _fontSize;
            //this.tbDishFilingNumber.FontSize = _fontSize;
            this.tbDishName.FontSize = _fontSize;
            // модификаторы
            if (dishView.Comment.IsNull() == false)
            {
                this.tbComment.Text = string.Format("\n({0})", dishView.Comment);
                this.tbComment.FontSize = 0.9 * _fontSize;
            }
            this.tbDishQuantity.FontSize = 1.1 * _fontSize;

            tbDishStatusTS.FontSize = _fontSize;
            tbDishStatusTS.FontWeight = FontWeights.Bold;

            _padd = 0.5 * fontSize;  // от немасштабного фонта
            brdMain.Padding = new Thickness(0, 0.5*_padd, 0, 0.5*_padd);
            brdTimer.Padding = new Thickness(0, _padd, 0, _padd);

            // рамка вокруг таймера
            setTimerBorder();
            
            // кисти и прочее
            //    блюдо
            if (_isDish)
            {
                tbDishName.FontWeight = FontWeights.Bold;
            }
            //    ингредиент
            else
            {
                tbDishName.FontWeight = FontWeights.Bold;
                BrushesPair brPair = BrushHelper.AppBrushes["ingrLineBase"];
                tbDishName.Background = brPair.Background;
                tbDishName.Foreground = brPair.Foreground;
            }
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

    }  // class
}
