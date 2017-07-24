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
        public void SetStatus(StatusEnum value)
        {
            _status = value;
        }

        public string UID { get; set; }
        public int Number { get; set; }

        public DateTime CreateDate { get; set; }

        public string WaitingTimerString { get; set; }

        public string HallName { get; set; }
        public string TableName { get; set; }

        public string Waiter { get; set; }

        public string DivisionColorRGB { get; set; }

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
            CreateDate = svcOrder.CreateDate;
            Waiter = svcOrder.Waiter;
            HallName = svcOrder.HallName;
            TableName = svcOrder.TableName;
            WaitingTimerString = svcOrder.WaitingTimerString;
            DivisionColorRGB = svcOrder.DivisionColorRGB;

            // создание коллекции блюд
            this.Dishes = new List<OrderDishViewModel>();
            int dishIndex = 0, curIndex = 0;
            foreach (OrderDishModel item in svcOrder.Dishes.Values)
            {
                // нумеруем только блюда
                if (item.ParentUid.IsNull()) { dishIndex++; curIndex = dishIndex; }
                else curIndex = 0;

                this.Dishes.Add(new OrderDishViewModel(item, curIndex));
            }
            _isDishesListUpdated = true;
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

            if (CreateDate != svcOrder.CreateDate)
            {
                CreateDate = svcOrder.CreateDate;
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
            
            if (DivisionColorRGB != svcOrder.DivisionColorRGB)
            {
                DivisionColorRGB = svcOrder.DivisionColorRGB;
                OnPropertyChanged("DivisionColorRGB");
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
