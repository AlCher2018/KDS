using KDSWPFClient.ServiceReference1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestData
{
    public class OrderDishTestModel
    {
        public int Id { get; set; }

        public string Uid { get; set; }

        public DateTime CreateDate { get; set; }

        public string Name { get; set; }

        // номер подачи
        public int FilingNumber { get; set; }

        // количество блюда/порции, может быть дробным
        public decimal Quantity { get; set; }

        // если есть, то это ингредиент к блюду ParentUid
        public string ParentUid { get; set; }

        // здесь описание модификаторов
        public string Comment { get; set; }

        public OrderStatusEnum Status { get; set; }


    }  // class
}
