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

        private bool _isDish, _isIngrIndepend, _isTimerBrushesIndepend;
        private double _fontSize, _padd;

        // поля дат состояний и временных промежутков
        private DateTime _dtCookingStartEstimated;   // ожидаемое начало приготовления
        private TimeSpan _tsCookingEstimated;   // время приготовления
        private string _strCookingEstimated;
        private string _timerString;
        internal string TimerString { get { return _timerString; } }

        private bool _negativeTimer;
        private string _waitCookBrushesName;

        private DishPanel _parentPanel;


        public DishPanel(OrderDishViewModel dishView, DishPanel parentPanel = null)
        {
            InitializeComponent();

            _dishView = dishView;
            _parentPanel = parentPanel;
            grdDishLine.DataContext = _dishView;

            _isDish = _dishView.ParentUID.IsNull();  // признак блюда
            _isIngrIndepend = (bool)AppLib.GetAppGlobalValue("IsIngredientsIndependent", false);
            // признак изменения рамки таймера
            _isTimerBrushesIndepend = (_isDish || (!_isDish && _isIngrIndepend));

            dishView.PropertyChanged += DishView_PropertyChanged;

            //double dishLineMinHeight = (double)AppLib.GetAppGlobalValue("ordPnlDishLineMinHeight");
            //base.MinHeight = dishLineMinHeight;

            double fontScale = AppLib.GetAppSetting("AppFontScale").ToDouble();
            double fontSize = (double)AppLib.GetAppGlobalValue("ordPnlDishLineFontSize"); // 12d
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
            this.tbDishQuantity.FontSize = _fontSize;

            _padd = 0.5 * fontSize;  // от немасштабного фонта
            brdMain.Padding = new Thickness(0, 0.5*_padd, 0, 0.5*_padd);
            brdTimer.Padding = new Thickness(0, _padd, 0, _padd);

            _negativeTimer = isTimerNegative();
            _waitCookBrushesName = getWaitCookBrushesName();
            // ожидаемое время начала приготовления для автоматического перехода в состояние приготовления
            _dtCookingStartEstimated = _dishView.CreateDate.AddSeconds(_dishView.DelayedStartTime);
            // время приготовления
            _tsCookingEstimated = TimeSpan.FromSeconds(_dishView.EstimatedTime);
            _strCookingEstimated = AppLib.GetAppStringTS(_tsCookingEstimated);

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
                BrushesPair brPair = BrushHelper.AppBrushes["ingrLineBase"];
                tbDishName.Background = brPair.Background;
                tbDishName.Foreground = brPair.Foreground;
            }
        }

        private bool isTimerNegative()
        {
            return !_dishView.WaitingTimerString.IsNull() && _dishView.WaitingTimerString.StartsWith("-");
        }

        // кисти для РЕЖИМА ОЖИДАНИЯ, а также значение таймера
        private string getWaitCookBrushesName()
        {
            string retVal = StatusEnum.WaitingCook.ToString();

            // для ингредиент берем значение таймера от родителя
            if (!_isTimerBrushesIndepend && (_parentPanel != null))
            {
                _timerString = _parentPanel.TimerString;
                _dishView.WaitingTimerString = _timerString;
                return retVal;
            }

            // если есть "Готовить через" - отображаем время начала автомат.перехода в сост."В процессе" по убыванию
            if (_dishView.DelayedStartTime != 0)
            {
                TimeSpan ts = _dtCookingStartEstimated - DateTime.Now;
                retVal = "estimateStart";
                _timerString = AppLib.GetAppStringTS(ts);
                if (ts.Ticks < 0)
                {
                    if (_dishView.EstimatedTime > 0)
                    {
                        retVal = "estimateCook"; _timerString = _strCookingEstimated;
                    }
                    else
                    {
                        retVal = StatusEnum.WaitingCook.ToString(); _timerString = "";
                    }
                }
            }
            // если есть время приготовления, то отобразить время приготовления
            else if (_dishView.EstimatedTime != 0)
            {
                retVal = "estimateCook";
                _timerString = _strCookingEstimated;
            }
            else
                _timerString = "";

            _dishView.WaitingTimerString = _timerString;

            return retVal;
        }


        // изменение свойств блюда - обновить кисти
        private void DishView_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == "CreateDate") || (e.PropertyName == "DelayedStartTime"))
                _dtCookingStartEstimated = _dishView.CreateDate.AddSeconds(_dishView.DelayedStartTime);

            // поменялся статус - однозначно меняем кисти
            if (e.PropertyName == "Status")
            {
                setTimerBorder();
            }

            // по значению таймера
            else if (e.PropertyName == "WaitingTimerString")
            {
                bool currentValue = isTimerNegative();
                if (_negativeTimer != currentValue)
                {
                    _negativeTimer = currentValue; setTimerBorder();
                }
                else if (_dishView.Status == StatusEnum.WaitingCook)
                {
                    string currentWaitCookBrushesName = getWaitCookBrushesName();
                    if (_waitCookBrushesName != currentWaitCookBrushesName)
                    {
                        _waitCookBrushesName = currentWaitCookBrushesName;
                        setTimerBorder();
                    }
                }
            }
        }

        // установка рамки вокруг таймера
        private void setTimerBorder()
        {
            // для блюда и независимого ингредиента
            if (_isTimerBrushesIndepend)
            {
                tbDishStatusTS.FontSize = 1.2 * _fontSize;
                tbDishStatusTS.FontWeight = FontWeights.Bold;
                setTimerBrushes();
            }
            // для ЗАВИСИМОГО ИНГРЕДИЕНТА рамка зависит от одинаковости статуса ингредиента и блюда
            else
            {
                tbDishStatusTS.FontSize = _fontSize;
                tbDishStatusTS.FontWeight = FontWeights.Normal;

                OrderDishViewModel parentDish = (OrderDishViewModel)_parentPanel.grdDishLine.DataContext;
                bool isBorder = (_dishView.Status != parentDish.Status);
                if (isBorder)
                {
                    setTimerBrushes();
                }
                // убрать рамку вокруг ЗАВИСИМОГО ингредиента
                else
                {
                    BrushesPair brPair = BrushHelper.AppBrushes["ingrLineBase"];
                    brdTimer.Background = brPair.Background;
                    tbDishStatusTS.Foreground = brPair.Foreground;
                }
            }
        }

        // установка кистей при изменении состоянию блюда
        private void setTimerBrushes()
        {
            Dictionary<string, BrushesPair> appBrushes = BrushHelper.AppBrushes;
            StatusEnum status = _dishView.Status;
            BrushesPair brPair = null;

            if (status == StatusEnum.WaitingCook)
            {
                brPair = appBrushes[_waitCookBrushesName];
            }
            else
            {
                // проверить на наличие кистей для отрицательных значений
                if (_negativeTimer)
                {
                    string keyNegative = status.ToString() + "minus";
                    if (appBrushes.ContainsKey(keyNegative)) brPair = appBrushes[keyNegative];
                }
                if (brPair == null)
                {
                    string key = status.ToString();
                    if (appBrushes.ContainsKey(key)) brPair = appBrushes[key];
                }
            }

            if (brPair != null)
            {
                brdTimer.Background = brPair.Background;
                tbDishStatusTS.Foreground = brPair.Foreground;
            }

        }

        // клик по строке блюда/ингредиента
        private void root_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // КЛИКАБЕЛЬНОСТЬ (условия отсутсвия)
            // 1. не входит в отображаемые отделы
            if (AppLib.IsDepViewOnKDS(_dishView.DepartmentId) == false) return;

            // 2. ЗАВИСИМЫЙ, неотмененный ингредиент, родительское блюдо которого тоже входит в отображаемые отделы,
            //    в этом случае, изменение состояния должно осуществляться блюдом
            //if (!_isDish && !_isIngrIndepend && (_dishView.Quantity > 0)) return;

            OrderViewModel orderView = null;
            FrameworkElement orderPanel = AppLib.FindVisualParent(this, typeof(OrderPanel), null);
            if (orderPanel != null) orderView = (orderPanel as OrderPanel).OrderViewModel;

            StateChange win = new StateChange() { Order = orderView, Dish = _dishView };

            win.ShowDialog();

        //    MessageBox.Show(string.Format("dish id {0} - {1}, state {2}",dishView.Id, dishView.DishName, dishView.Status));
        }

    }  // class
}
