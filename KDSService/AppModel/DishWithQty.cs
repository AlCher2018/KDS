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

        private List<int> _groupedIds;


        public decimal Quantity { get; set; }

        public DishWithQty()
        {
        }

        public DishWithQty(OrderDishModel dishModel)
        {
            this.DishModel = dishModel;
            this.Quantity = dishModel.Quantity;
        }

        public void AddGroupedId(int id)
        {
            if (_groupedIds == null) _groupedIds = new List<int>();
            _groupedIds.Add(id);
        }

        public string GetGroupedIds()
        {
            if (_groupedIds == null)
                return null;
            else
                return string.Join(";", _groupedIds);
        }

    }  // class
}
