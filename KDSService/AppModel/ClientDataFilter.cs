using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    public class ClientDataFilter
    {
        // фильтр по всем заказам
        public List<int> StatusesList { get; set; }
        public List<int> DepIDsList { get; set; }
        public OrderGroupEnum GroupBy { get; set; }

        // ограничение количества элементов
        public int EndpointOrderID { get; set; }
        public int EndpointOrderItemID { get; set; }
        public LeafDirectionEnum LeafDirection { get; set; }
        public int ApproxMaxDishesCountOnPage { get; set; }
    }
}
