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
    public class OrderViewModel : INotifyPropertyChanged
    {
        public int Id { get; set; }

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
        public bool IsDishesListUpdated { get { return _isDishesListUpdated; } }

        public OrderViewModel(OrderModel svcOrder)
        {
            Id = svcOrder.Id;
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
            _isDishesListUpdated = false;

            int dishIndex = 0;
            OrderDishViewModel dishView;
            foreach (OrderDishModel dishModel in svcOrder.Dishes.Values)
            {
                if (dishIndex == Dishes.Count)
                {
                    dishView = new OrderDishViewModel(dishModel, dishIndex);
                    Dishes.Add(dishView);
                    _isDishesListUpdated = true;
                }
                else if (Dishes[dishIndex].Id != dishModel.Id)
                {
                    // попытаться найти блюдо с таким Ид и переставить его в нужную позицию
                    dishView = this.Dishes.FirstOrDefault(d => d.Id == dishModel.Id);
                    if (dishView == null)  // не найдено - ВСТАВЛЯЕМ в нужную позицию
                    {
                        dishView = new OrderDishViewModel(dishModel, dishIndex+1);
                        Dishes.Insert(dishIndex, dishView);
                        _isDishesListUpdated = true;
                    }
                    else  // переставляем
                    {
                        Dishes.Remove(dishView);
                        Dishes.Insert(dishIndex, dishView);
                        dishView.Index = dishIndex + 1;
                        dishView.UpdateFromSvc(dishModel);
                    }
                }
                else
                {
                    dishView = Dishes[dishIndex];
                    dishView.Index = dishIndex + 1;
                    dishView.UpdateFromSvc(dishModel);
                }
                dishIndex++;
            }

            // удалить блюда, которые не пришли от службы
            while (Dishes.Count > (dishIndex+1)) { Dishes.RemoveAt(Dishes.Count-1); _isDishesListUpdated = true; }

        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }


    }  // class 
}
