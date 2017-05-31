using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public int DishStatusId { get; set; }
        private StatusEnum _status;

        public StatusEnum Status { get { return _status; } }

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
            _status = (StatusEnum)DishStatusId;
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
        }

        public void UpdateFromSvc(OrderDishModel svcOrderDish)
        {
            if (DishStatusId != svcOrderDish.DishStatusId)
            {
                DishStatusId = svcOrderDish.DishStatusId;
                _status = (StatusEnum)DishStatusId;
                OnPropertyChanged("Status");
            }

            if (UID != svcOrderDish.Uid) UID = svcOrderDish.Uid;

            if (ParentUID != svcOrderDish.ParentUid) ParentUID = svcOrderDish.ParentUid;

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

            if (WaitingTimerString != svcOrderDish.WaitingTimerString)
            {
                WaitingTimerString = svcOrderDish.WaitingTimerString;
                OnPropertyChanged("WaitingTimerString");
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }


    }  // class
}
