using KDSClient.ServiceReference1;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSClient.ViewModel
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public OrderStatusEnum OrderStatus { get; set; }
        public int DepartmentId { get; set; }

        public string UID { get; set; }
        public int Number { get; set; }

        public DateTime DateCreate { get; set; }
        public DateTime DateStart { get; set; }
        public int SpentTime { get; set; }

        public string HallName { get; set; }
        public string TableName { get; set; }

        public string Garson { get; set; }

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
