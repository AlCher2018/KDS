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

        public string WaitingTimerString { get; set; }

        public string ViewTimerString { get; set; }

        // поля дат состояний и временных промежутков
        // - ожидаемое начало приготовления
        private DateTime _dtCookingStartEstimated;
        // - время приготовления
        private TimeSpan _tsCookingEstimated;
        private string _strCookingEstimated;


        // CONSTRUCTORS
        public OrderDishViewModel()
        {

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
            WaitingTimerString = svcOrderDish.WaitingTimerString;

            setLocalDTFields();
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

        private string getViewTimerString()
        {
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
                    timerString = "";
            }

            // другие состояния
            else
            {
                if (this.Id == 482) Debug.Print("tmr, from svc: " + this.WaitingTimerString);
                TimeSpan tsTimerValue = AppLib.GetTSFromString(this.WaitingTimerString);

                // состояние "В процессе" - отображаем время приготовления по убыванию от планого времени приготовления,
                // если нет планового времени приготовления, то сразу отрицат.значения
                if (this.Status == OrderStatusEnum.Cooking)
                {
                    tsTimerValue = _tsCookingEstimated - (tsTimerValue.Ticks < 0 ? tsTimerValue.Negate() : tsTimerValue);
                }

                // состояние "ГОТОВО": проверить период ExpectedTake, в течение которого официант должен забрать блюдо
                else
                {
                    // из глобальных настроек
                    bool isUseReadyConfirmed = (bool)AppLib.GetAppGlobalValue("UseReadyConfirmedState", false);
                    if ((!isUseReadyConfirmed && (this.Status == OrderStatusEnum.Ready))
                        || (isUseReadyConfirmed && (this.Status == OrderStatusEnum.ReadyConfirmed)))
                    {
                        int expTake = (int)AppLib.GetAppGlobalValue("ExpectedTake");
                        if (expTake > 0)
                        {
                            tsTimerValue = TimeSpan.FromSeconds(expTake) - (tsTimerValue.Ticks < 0 ? tsTimerValue.Negate() : tsTimerValue);
                            if (this.Id == 482) Debug.Print("tmr, expected: " + tsTimerValue.ToString());
                        }
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
