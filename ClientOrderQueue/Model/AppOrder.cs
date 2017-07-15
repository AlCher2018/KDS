using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientOrderQueue.Model
{
    public class AppOrder
    {
        public int Id { get; set; }

        public Order Order { get; set; }

        // время, от которого считается таймер приготовления блюда
        // если нет ожидаемого времени приготовления, то это - CreateDate
        // иначе - это CreateDate + EstimateTS
        public DateTime OrderCookingBaseDT { get; set; }
        // признак наличия ожидаемого времени приготовления заказа
        public bool IsExistOrderEstimateDT { get; set; }
    }
}
