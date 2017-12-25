using IntegraLib;
using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.View;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace KDSWPFClient.ViewModel
{
    public class OrderDishViewModel : INotifyPropertyChanged, IJoinSortedCollection<OrderDishModel>, IContainIDField
    {
        public int Id { get; set; }

        // порядковый номер блюда в списке заказа, начинается с 1
        private int _index;
        public int Index
        {
            get { return _index; }
            set { _index = value; }
        }

        private int _status;
        public int DishStatusId {
            get { return _status; }
            set { _status = value; }
        }
        public OrderStatusEnum Status { get { return (OrderStatusEnum)_status; } }

        public int DepartmentId { get; set; }

        public string UID { get; set; }

        public string ParentUID { get; set; }

        public string DishName { get; set; }

        public int FilingNumber { get; set; }

        public decimal Quantity { get; set; }

        public string Comment { get; set; }

        public System.DateTime CreateDate { get; set; }

        public int DelayedStartTime { get; set; }

        public int EstimatedTime { get; set; }

        public string ServiceErrorMessage { get; set; }

        public string GroupedDishIds { get; set; }


        public string WaitingTimerString { get; set; }

        public string ViewTimerString { get; set; }

        // поля дат состояний и временных промежутков
        // - ожидаемое начало приготовления
        private DateTime _dtCookingStartEstimated;
        // - время приготовления
        private TimeSpan _tsCookingEstimated;
        private string _strCookingEstimated;

        bool _isUseReadyConfirmed;
        bool _enableTimerToAutoReadyConfirm;
        public bool EnableTimerToAutoReadyConfirm { get { return _enableTimerToAutoReadyConfirm; } }

        // период времени (в секундах), в течение которого официант должен забрать блюдо
        TimeSpan _expTakeTS;
        // период времени (в секундах), по истечении которого происходит автоматический переход из состояния Готово в ПодтвГотово
        TimeSpan _autoGotoReadyConfirmTS, _savedReadyTS;



        // CONSTRUCTORS
        public OrderDishViewModel()
        {
            // из глобальных настроек
            _isUseReadyConfirmed = (bool)WpfHelper.GetAppGlobalValue("UseReadyConfirmedState", false);
            _expTakeTS = TimeSpan.FromSeconds(System.Convert.ToInt32(WpfHelper.GetAppGlobalValue("ExpectedTake")));
            _autoGotoReadyConfirmTS = TimeSpan.FromSeconds(System.Convert.ToInt32(WpfHelper.GetAppGlobalValue("AutoGotoReadyConfirmPeriod", 0)));
        }
        public OrderDishViewModel(OrderDishModel svcOrderDish, int index): this()
        {
            FillDataFromServiceObject(svcOrderDish, index);
        }

        public void FillDataFromServiceObject(OrderDishModel svcOrderDish, int index)
        {
            _index = index;
            Id = svcOrderDish.Id;
            DishStatusId = svcOrderDish.DishStatusId;
            DepartmentId = svcOrderDish.DepartmentId;
            UID = svcOrderDish.Uid;
            ParentUID = svcOrderDish.ParentUid;
            DishName = svcOrderDish.Name;
            FilingNumber = svcOrderDish.FilingNumber;
            Quantity = svcOrderDish.Quantity;
            Comment = svcOrderDish.Comment;
            CreateDate = svcOrderDish.CreateDate;
            DelayedStartTime = svcOrderDish.DelayedStartTime;
            EstimatedTime = svcOrderDish.EstimatedTime;
            ServiceErrorMessage = svcOrderDish.ServiceErrorMessage;
            GroupedDishIds = svcOrderDish.GroupedDishIds;

            WaitingTimerString = svcOrderDish.WaitingTimerString;

            setLocalDTFields();
            this.ViewTimerString = getViewTimerString();
        }

        private void setLocalDTFields()
        {
            // ожидаемое время начала приготовления для автоматического перехода в состояние приготовления
            _dtCookingStartEstimated = this.CreateDate.AddSeconds(this.DelayedStartTime);
            // время приготовления
            _tsCookingEstimated = TimeSpan.FromSeconds(this.EstimatedTime);
            _strCookingEstimated = AppLib.GetAppStringTS(_tsCookingEstimated);
        }

        public void UpdateFromSvc(OrderDishModel svcOrderDish)
        {
            if (DishStatusId != svcOrderDish.DishStatusId)
            {
                DishStatusId = svcOrderDish.DishStatusId;
                // заходим в состояние Готово при включенном ПодтвГотово - запоминаем время готовки, чтобы потом вычислять таймер автомат.перехода в ПодтвГотово
                if ((_isUseReadyConfirmed == true) && (svcOrderDish.DishStatusId == 2))
                {
                    _savedReadyTS = AppLib.GetTSFromString(svcOrderDish.WaitingTimerString);
                }
                OnPropertyChanged("Status");
            }

            if (UID != svcOrderDish.Uid) UID = svcOrderDish.Uid;
            if (ParentUID != svcOrderDish.ParentUid) ParentUID = svcOrderDish.ParentUid;
            if (DepartmentId != svcOrderDish.DepartmentId) DepartmentId = svcOrderDish.DepartmentId;

            if (DishName != svcOrderDish.Name)
            {
                DishName = svcOrderDish.Name;
                OnPropertyChanged("DishName");
            }

            if (FilingNumber != svcOrderDish.FilingNumber)
            {
                FilingNumber = svcOrderDish.FilingNumber;
                OnPropertyChanged("FilingNumber");
            }

            if (Quantity != svcOrderDish.Quantity)
            {
                Quantity = svcOrderDish.Quantity;
                OnPropertyChanged("Quantity");
            }

            if (Comment != svcOrderDish.Comment)
            {
                Comment = svcOrderDish.Comment;
                OnPropertyChanged("Comment");
            }

            if (CreateDate != svcOrderDish.CreateDate)
            {
                CreateDate = svcOrderDish.CreateDate;
                OnPropertyChanged("CreateDate");
            }

            if (DelayedStartTime != svcOrderDish.DelayedStartTime)
            {
                DelayedStartTime = svcOrderDish.DelayedStartTime;
                OnPropertyChanged("DelayedStartTime");
            }

            if (EstimatedTime != svcOrderDish.EstimatedTime)
            {
                EstimatedTime = svcOrderDish.EstimatedTime;
                OnPropertyChanged("EstimatedTime");
            }

            if (ServiceErrorMessage != svcOrderDish.ServiceErrorMessage) ServiceErrorMessage = svcOrderDish.ServiceErrorMessage;

            setLocalDTFields();

            if (WaitingTimerString != svcOrderDish.WaitingTimerString)
            {
                WaitingTimerString = svcOrderDish.WaitingTimerString;
                OnPropertyChanged("WaitingTimerString");

                string viewTimer = getViewTimerString();
                if (viewTimer != this.ViewTimerString)
                {
                    this.ViewTimerString = viewTimer;
                    OnPropertyChanged("ViewTimerString");
                }
            }
        }


        // время, отображаемое на панели блюда
        private string getViewTimerString()
        {
            // для ИНГРЕДИЕНТОВ
            if ((this.ParentUID != null) && (this.ParentUID.Length > 0))
            {
                // отключен флажок НЕЗАВИСИМОСТИ (IsIngredientsIndependent == false) и ВЫКЛЮЧЕН флажок показа таймера ShowTimerOnDependIngr
                bool b1 = (bool)WpfHelper.GetAppGlobalValue("IsIngredientsIndependent", false),
                     b2 = (bool)WpfHelper.GetAppGlobalValue("ShowTimerOnDependIngr", false);
                bool isShowTimer = b1 || (!b1 && b2);
                if (isShowTimer == false) { return null; }
            }

            // текущее значение таймера
            string timerString = this.WaitingTimerString;
            // состояние "Ожидание" начала готовки
            if (this.Status == OrderStatusEnum.WaitingCook)
            {
                // если есть "Готовить через" - отображаем время начала автомат.перехода в сост."В процессе" по убыванию
                if (this.DelayedStartTime != 0)
                {
                    TimeSpan ts = _dtCookingStartEstimated - DateTime.Now;
                    timerString = AppLib.GetAppStringTS(ts);
                    if (ts.Ticks < 0)
                    {
                        if (this.EstimatedTime > 0)
                            timerString = _strCookingEstimated;
                        else
                            timerString = "";
                    }
                }
                // если есть время приготовления, то отобразить время приготовления
                else if (this.EstimatedTime != 0)
                {
                    timerString = _strCookingEstimated;
                }
                else
                    // по умолчанию отправить 
                    timerString = "";
            }

            // другие состояния
            else
            {
                TimeSpan tsTimerValue = AppLib.GetTSFromString(this.WaitingTimerString);

                // состояние "В процессе" - отображаем время приготовления по убыванию от планового времени приготовления,
                // если нет планового времени приготовления, то сразу отрицат.значения
                if (this.Status == OrderStatusEnum.Cooking)
                {
                    tsTimerValue = _tsCookingEstimated - (tsTimerValue.Ticks < 0 ? tsTimerValue.Negate() : tsTimerValue);
                }

                else
                {
                    // состояние "Готово/ПодтвГотово" - счетчик по убыванию от ExpectedTake (период, в течение которого официант должен забрать блюдо)
                    if ((!_isUseReadyConfirmed && (this.Status == OrderStatusEnum.Ready))
                        || (_isUseReadyConfirmed && (this.Status == OrderStatusEnum.ReadyConfirmed)))
                    {
                        if (!_expTakeTS.IsZero())
                        {
                            tsTimerValue = _expTakeTS - (tsTimerValue.Ticks < 0 ? tsTimerValue.Negate() : tsTimerValue);
                        }
                    }
                    // есть ПодтвГотово, находимся в состоянии Готово и _autoGotoReadyConfirmPeriod не равен 0 - счетчик по убыванию от _autoGotoReadyConfirmPeriod (период, по истечении которого происходит автоматический переход в состояние ПодтвГотово)
                    else if (_isUseReadyConfirmed 
                        && (!_autoGotoReadyConfirmTS.IsZero()) 
                        && (this.Status == OrderStatusEnum.Ready) && (_savedReadyTS.IsZero() == false))
                    {
                        tsTimerValue = _autoGotoReadyConfirmTS - (tsTimerValue - _savedReadyTS);
                        _enableTimerToAutoReadyConfirm = (tsTimerValue.TotalSeconds > 0);
                    }
                }

                timerString = AppLib.GetAppStringTS(tsTimerValue);
            }

            return timerString;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }


    }  // class
}
