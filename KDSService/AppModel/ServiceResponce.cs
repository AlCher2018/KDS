using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    public class ServiceResponce
    {
        public List<OrderModel> OrdersList { get; set; }

        // флаги наличия "хвостов" в базе для возврата из клиенту для отображения кнопок листания
        public bool isExistsPrevOrders { get; set; }
        public bool isExistsNextOrders { get; set; }

        public ServiceResponce()
        {
            this.OrdersList = new List<OrderModel>();
            this.isExistsPrevOrders = false;
            this.isExistsNextOrders = false;
        }

    }
}
