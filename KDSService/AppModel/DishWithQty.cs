using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    // класс, хранящий OrderDishModel и суммарное количество порций при группировке блюд
    public class DishWithQty
    {
        public OrderDishModel DishModel { get; set; }

        public decimal Quantity { get; set; }

        public DishWithQty()
        {
        }

        public DishWithQty(OrderDishModel dishModel)
        {
            this.DishModel = dishModel;
            this.Quantity = dishModel.Quantity;
        }

    }  // class
}
