using KDSWPFClient;
using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;



namespace KDSWPFClient.ViewModel
{
    public class OrderViewModel : INotifyPropertyChanged, IJoinSortedCollection<OrderModel>, IContainIDField, IContainInnerCollection
    {
        public int Id { get; set; }

        // порядковый номер заказа, начинается с 1
        // нужен для обобщенного стат.метода соединения двух списков (IJoinSortedCollection)
        private int _index;
        public int Index
        {
            get { return _index; }
            set { _index = value; }
        }

        public int OrderStatusId { get; set; }

        private StatusEnum _status;
        public StatusEnum Status { get { return _status; } }

        public string UID { get; set; }
        public int Number { get; set; }

        private DateTime _createDate;
        public string CreateDate { get; set; }

        public string WaitingTimerString { get; set; }

        public string HallName { get; set; }
        public string TableName { get; set; }

        public string Waiter { get; set; }

        public virtual List<OrderDishViewModel> Dishes { get; set; }

        public OrderPanel ViewPanel { get; set; }

        private bool _isDishesListUpdated;
        public bool IsInnerListUpdated { get { return _isDishesListUpdated; } }


        // КОНСТРУКТОРЫ
        public OrderViewModel()
        {
        }

        public OrderViewModel(OrderModel svcOrder, int index = 1) : this()
        {
            FillDataFromServiceObject(svcOrder, index);
        }


        public void FillDataFromServiceObject(OrderModel svcOrder, int index = 1)
        {
            Id = svcOrder.Id;
            _index = index;
            OrderStatusId = svcOrder.OrderStatusId;
            _status = (StatusEnum)OrderStatusId;
            UID = svcOrder.Uid;
            Number = svcOrder.Number;

            _createDate = svcOrder.CreateDate;
            setViewCreateDate();

            WaitingTimerString = svcOrder.WaitingTimerString;
            Waiter = svcOrder.Waiter;
            HallName = svcOrder.HallName;
            TableName = svcOrder.TableName;

            this.Dishes = new List<OrderDishViewModel>();
            int dishIndex = 0;
            foreach (OrderDishModel item in svcOrder.Dishes.Values)
            {
                dishIndex++;
                this.Dishes.Add(new OrderDishViewModel(item, dishIndex));
            }
            _isDishesListUpdated = true;
        }


        private void setViewCreateDate()
        {
            if (_createDate.Equals(DateTime.MinValue))
                CreateDate = "no data";
            else if (DateTime.Now.Day !=_createDate.Day)  // показать и дату создания заказа
            {
                CreateDate = _createDate.ToString("dd.MM.yyyy HH:mm:ss");
            }
            else  // показать только время создания заказа
            {
                CreateDate = _createDate.ToString("HH:mm:ss");
            }
        }

        public void UpdateFromSvc(OrderModel svcOrder)
        {
            if (OrderStatusId != svcOrder.OrderStatusId)
            {
                OrderStatusId = svcOrder.OrderStatusId;
                _status = (StatusEnum)OrderStatusId;
                OnPropertyChanged("Status");
            }
            if (UID != svcOrder.Uid) UID = svcOrder.Uid;

            if (Number != svcOrder.Number)
            {
                Number = svcOrder.Number;
                OnPropertyChanged("Number");
            }

            if (_createDate != svcOrder.CreateDate)
            {
                _createDate = svcOrder.CreateDate;
                setViewCreateDate();
                OnPropertyChanged("CreateDate");
            }

            if (WaitingTimerString != svcOrder.WaitingTimerString)
            {
                WaitingTimerString = svcOrder.WaitingTimerString;
                OnPropertyChanged("WaitingTimerString");
            }

            if (Waiter != svcOrder.Waiter)
            {
                Waiter = svcOrder.Waiter;
                OnPropertyChanged("Waiter");
            }

            if (HallName != svcOrder.HallName)
            {
                HallName = svcOrder.HallName;
                OnPropertyChanged("HallName");
            }

            if (TableName != svcOrder.TableName)
            {
                TableName = svcOrder.TableName;
                OnPropertyChanged("TableName");
            }

            // ОБНОВИТЬ БЛЮДА В ЗАКАЗЕ
            // выставить флаг _isDishesListUpdated в true, если была изменена коллекция блюд или изменен порядок блюд
            // и необходимо перерисовать все панели
            _isDishesListUpdated = AppLib.JoinSortedLists<OrderDishViewModel, OrderDishModel>(Dishes, svcOrder.Dishes.Values.ToList());

        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }

    }  // class 
}
