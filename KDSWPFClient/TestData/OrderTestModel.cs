using IntegraLib;
using System;
using System.Collections.Generic;


namespace TestData
{
    public class OrderTestModel
    {
        public int Id { get; set; }

        public int Number { get; set; }

        public string Uid { get; set; }

        public DateTime CreateDate { get; set; }

        public string HallName { get; set; }

        public string TableName { get; set; }

        public string Waiter { get; set; }

        public OrderStatusEnum Status { get; set; }

        private List<OrderDishTestModel> _dishesDict;

        public List<OrderDishTestModel> Dishes
        {
            get { return _dishesDict; }
        }

        public OrderTestModel()
        {
            _dishesDict = new List<OrderDishTestModel>();
        }

        public OrderTestModel(OrderTestModel baseOrder): this()
        {
            this.Id = baseOrder.Id;
            this.Number = baseOrder.Number;
            this.Uid = baseOrder.Uid;
            this.CreateDate = baseOrder.CreateDate;
            this.HallName = baseOrder.HallName;
            this.TableName = baseOrder.TableName;
            this.Waiter = baseOrder.Waiter;
            this.Status = baseOrder.Status;
        }


    }  // class
}
