using KDSWPFClient;
using KDSWPFClient.ServiceReference1;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace KDSWPFClient.ViewModel
{
    public class OrderViewModel
    {
        public int Id { get; set; }

        public int OrderStatusId { get; set; }

        public int DepartmentId { get; set; }

        public string UID { get; set; }
        public int Number { get; set; }

        public DateTime CreateDate { get; set; }
        public DateTime StartDate { get; set; }
        public int SpentTime { get; set; }

        public string HallName { get; set; }
        public string TableName { get; set; }

        public string Waiter { get; set; }

        public virtual List<OrderDishViewModel> OrderDish { get; set; }


        public OrderViewModel()
        {
        }

        public OrderViewModel(OrderModel svcOrder)
        {
            Id = svcOrder.Id;
        }

    }  // class 
}
