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

        private bool _isDish, _isIngrIndepend, _isTimerBrushesIndepend;
        private double _fontSize, _padd;

        // поля дат состояний и временных промежутков
        private DateTime _dtCookingStartEstimated;   // ожидаемое начало приготовления
        private TimeSpan _tsCookingEstimated;   // время приготовления
        private string _strCookingEstimated;
        private string _timerString;

        internal string TimerString { get { return _timerString; } }

        private bool _negativeTimer;
        private string _cookBrushesName;

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

            _cookBrushesName = getCookBrushesName();
            _negativeTimer = isTimerNegative();
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
                tbDishName.FontWeight = FontWeights.Bold;
                BrushesPair brPair = BrushHelper.AppBrushes["ingrLineBase"];
                tbDishName.Background = brPair.Background;
                tbDishName.Foreground = brPair.Foreground;
            }
        }

        private bool isTimerNegative()
        {
            string s = _timerString; // _dishView.WaitingTimerString;

            return !s.IsNull() && s.StartsWith("-");
        }

        // возвращает ключ из словаря для кистей состояния, а также устанавливает значение таймера ()
        private string getCookBrushesName()
        {
            string retVal = _dishView.Status.ToString();

            // для ингредиента берем значение таймера от родителя, если ингредиент принадлежить этому же КДСу
            // и у ингредиента и родителя одинаковый статус
            //if (!_isTimerBrushesIndepend && (_parentPanel != null) 
            //    && (_dishView.Status == _parentPanel.DishView.Status) 
            //    && (AppLib.IsDepViewOnKDS(_dishView.DepartmentId)))
            //{
            //    _timerString = _parentPanel.TimerString;
            //    _dishView.WaitingTimerString = _timerString;
            //    return retVal;
            //}

            // текущее значение таймера
            _timerString = _dishView.WaitingTimerString;

            // состояние "Ожидание" начала готовки
            if (_dishView.Status == OrderStatusEnum.WaitingCook)
            {
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
                            retVal = "estimateCook";
                            _timerString = _strCookingEstimated;
                        }
                        else
                        {
                            retVal = StatusEnum.WaitingCook.ToString();
                            _timerString = "";
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

                // отобразить таймер для состояния ожидания
                _dishView.WaitingTimerString = _timerString;
            }

            // другие состояния
            else
            {
                TimeSpan tsTimerValue = AppLib.GetTSFromString(_dishView.WaitingTimerString);

                // состояние "В процессе" - отображаем время приготовления по убыванию от планого времени приготовления,
                // если нет планового времени приготовления, то сразу отрицат.значения
                if (_dishView.Status == OrderStatusEnum.Cooking)
                {
                    tsTimerValue = _tsCookingEstimated - (tsTimerValue.Ticks < 0 ? tsTimerValue.Negate() : tsTimerValue);
                }

                // состояние "ГОТОВО": проверить период ExpectedTake, в течение которого официант должен забрать блюдо
                else
                {
                    // из глобальных настроек
                    bool isUseReadyConfirmed = (bool)AppLib.GetAppGlobalValue("UseReadyConfirmedState", false);
                    if ((!isUseReadyConfirmed && (_dishView.Status == OrderStatusEnum.Ready))
                        || (isUseReadyConfirmed && (_dishView.Status == OrderStatusEnum.ReadyConfirmed)))
                    {
                        int expTake = (int)AppLib.GetAppGlobalValue("ExpectedTake");
                        if (expTake > 0)
                        {
                            tsTimerValue = TimeSpan.FromSeconds(expTake) - (tsTimerValue.Ticks < 0 ? tsTimerValue.Negate() : tsTimerValue);
                        }
                    }
                }

                _timerString = AppLib.GetAppStringTS(tsTimerValue);
            }

            if (_dishView.WaitingTimerString != _timerString ) _dishView.WaitingTimerString = _timerString;

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
                string currentCookBrushesName = getCookBrushesName();
                bool currentValue = isTimerNegative();

                //if (_dishView.Id == 8) Debug.Print("_timerString={0}, _negativeTimer = {1}, currentValue={2}", _timerString, _negativeTimer, currentValue);

                if (_negativeTimer != currentValue)
                {
                    _negativeTimer = currentValue;

                    //if (_dishView.Id == 8) Debug.Print(" --- setTimerBorder();  _isTimerBrushesIndepend={0}", _isTimerBrushesIndepend);

                    setTimerBorder();
                }
                else if (_cookBrushesName != currentCookBrushesName)
                {
                    _cookBrushesName = currentCookBrushesName;
                    setTimerBorder();
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
//                setTimerBrushes();
            }
            // для ЗАВИСИМОГО ИНГРЕДИЕНТА рамка зависит от одинаковости статуса ингредиента и блюда
            else
            {
                tbDishStatusTS.FontSize = _fontSize;
                tbDishStatusTS.FontWeight = FontWeights.Normal;

                //OrderDishViewModel parentDish = (OrderDishViewModel)_parentPanel.grdDishLine.DataContext;
                //bool isBorder = (_dishView.Status != parentDish.Status);
                //if (isBorder)
                //{
  //                  setTimerBrushes();
                //}
                //// убрать рамку вокруг ЗАВИСИМОГО ингредиента
                //else
                //{
                //    BrushesPair brPair = BrushHelper.AppBrushes["ingrLineBase"];
                //    brdTimer.Background = brPair.Background;
                //    tbDishStatusTS.Foreground = brPair.Foreground;
                //}
            }

            //if (_dishView.Id == 8) Debug.Print(" --- setTimerBrushes();  _negativeTimer={0}", _negativeTimer);

            setTimerBrushes();
        }

        // установка кистей при изменении состоянию блюда
        private void setTimerBrushes()
        {
            Dictionary<string, BrushesPair> appBrushes = BrushHelper.AppBrushes;
            OrderStatusEnum status = _dishView.Status;
            BrushesPair brPair = null;

            if (status == OrderStatusEnum.WaitingCook)
            {
                brPair = appBrushes[_cookBrushesName];
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

            //if (_dishView.Id == 8) Debug.Print(" --- (brPair == null) = {0}", (brPair == null));

            if (brPair != null)
            {
                brdTimer.Background = brPair.Background;
                tbDishStatusTS.Foreground = brPair.Foreground;
            }

        }

        // клик по строке блюда/ингредиента
        private void root_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // КЛИКАБЕЛЬНОСТЬ (условия отсутствия)
            // 1. не входит в отображаемые отделы
            if (AppLib.IsDepViewOnKDS(_dishView.DepartmentId) == false)
                return;
            // 2. условие кликабельности ингредиента (независимо от блюда) или блюда на допнаправлении
            else
            {
                bool b1 = _isTimerBrushesIndepend;
                bool b2 = (bool)AppLib.GetAppGlobalValue("IngrClickable", false);
                if ((b1 == false) && (b2 == false)) return;
            }

            OrderViewModel orderView = null;
            FrameworkElement orderPanel = AppLib.FindVisualParent(this, typeof(OrderPanel), null);
            if (orderPanel != null) orderView = (orderPanel as OrderPanel).OrderViewModel;

            App.OpenStateChangeWindow(orderView, _dishView);

            e.Handled = true;

        //    MessageBox.Show(string.Format("dish id {0} - {1}, state {2}",dishView.Id, dishView.DishName, dishView.Status));
        }

    }  // class
}
