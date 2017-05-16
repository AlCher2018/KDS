using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace TestData
{
    public static class TestDataHelper
    {
        private static Random rnd = new Random();

        public static List<OrderTestModel> GetTestOrders(int rangeFrom, int rangeTo)
        {
            List<OrderTestModel> retVal = new List<OrderTestModel>();
            Dictionary<int, OrderTestModel> dbOrders = GetOrdersDict();

            OrderTestModel ord, oDict;
            int rndCount = rnd.Next(rangeFrom, rangeTo);
            for (int i = 0; i < rndCount; i++)
            {
                oDict = dbOrders.Values.ElementAt(rnd.Next(1, dbOrders.Count));
                ord = new OrderTestModel(oDict);
                SetRandomDishes(ord.Dishes, 1, 21);

                retVal.Add(ord);
            }
            return retVal;
        }  // method


        public static void SetRandomDishes(List<OrderDishTestModel> dishes, int rangeFrom, int rangeTo)
        {
//            List<OrderDishModel> retVal = new List<OrderDishModel>();
            Dictionary<int, OrderDishTestModel> dbDishes = GetDishesDict();

            OrderDishTestModel oDish;
            int rndCount = rnd.Next(rangeFrom, rangeTo);
            for (int i = 0; i < rndCount; i++)
            {
                int iDish = rnd.Next(0, dbDishes.Count);
                oDish = dbDishes.Values.ElementAt(iDish);

                dishes.Add(oDish);
            }
        }


        // словарь заказов
        public static Dictionary<int, OrderTestModel> GetOrdersDict()
        {
            return new Dictionary<int, OrderTestModel>()
            {
                {1, new OrderTestModel() { Id = 1, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 1",
                    TableName = "Стол 1", Waiter = "Коцяруба Елизавета"} },
                {2, new OrderTestModel() { Id = 2, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 1",
                    TableName = "Стол 2", Waiter = "Товстокор Артем"} },
                {3, new OrderTestModel() { Id = 3, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 1",
                    TableName = "Стол 3", Waiter = "Визняк Анна"} },
                {4, new OrderTestModel() { Id = 4, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 1",
                    TableName = "Стол 4", Waiter = "Кебас Христина"} },
                {5, new OrderTestModel() { Id = 5, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 1",
                    TableName = "Стол 5", Waiter = "Кузнецова Катерина"} },
                {6, new OrderTestModel() { Id = 6, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 2",
                    TableName = "Стол 1", Waiter = "Чередніченко Віталій"} },
                {7, new OrderTestModel() { Id = 7, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 2",
                    TableName = "Стол 2", Waiter = "Лещенко Ирина"} },
                {8, new OrderTestModel() { Id = 8, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 2",
                    TableName = "Стол 3", Waiter = "Тригуб Людмила"} },
                {9, new OrderTestModel() { Id = 9, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 2",
                    TableName = "Стол 4", Waiter = "Плотникова Елена"} },
                {10, new OrderTestModel() { Id = 10, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 2",
                    TableName = "Стол 5", Waiter = "Гаркуша Олексій"} },
                {11, new OrderTestModel() { Id = 11, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 2",
                    TableName = "Стол 6", Waiter = "Салаков Андрей"} },
                {12, new OrderTestModel() { Id = 12, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 2",
                    TableName = "Стол 7", Waiter = "Оніщенко Віктор"} },
                {13, new OrderTestModel() { Id = 13, Number = 1, CreateDate = DateTime.Now, HallName = "Зал 2",
                    TableName = "Стол 8", Waiter = "Довганич Вероніка"} },
            };
        }

        // словарь блюд
        public static Dictionary<int, OrderDishTestModel> GetDishesDict()
        {
            return new Dictionary<int, OrderDishTestModel>()
            {
                {1, new OrderDishTestModel() {
                    Id = 1, Name ="Блюдо 1", Quantity = 1, CreateDate = DateTime.Now, FilingNumber = 1, Comment = ""
                } },
                {2, new OrderDishTestModel() {
                    Id = 2, Name ="Блюдо 2", Quantity = 1, CreateDate = DateTime.Now, FilingNumber = 1, Comment = "двойной чили"
                } },
                {3, new OrderDishTestModel() {
                    Id = 3, Name ="Блюдо 3", Quantity = 2, CreateDate = DateTime.Now, FilingNumber = 2, Comment = "не острое"
                } },
                {4, new OrderDishTestModel() {
                    Id = 4, Name ="Блюдо 4", Quantity = 1.5m, CreateDate = DateTime.Now, FilingNumber = 3, Comment = ""
                } },
                {5, new OrderDishTestModel() {
                    Id = 5, Name ="Блюдо 5", Quantity = 4, CreateDate = DateTime.Now, FilingNumber = 1, Comment = ""
                } },
                {6, new OrderDishTestModel() {
                    Id = 6, Name ="Блюдо 6", Quantity = 1, CreateDate = DateTime.Now, FilingNumber = 2, Comment = ""
                } },
                {7, new OrderDishTestModel() {
                    Id = 7, Name ="Блюдо 7", Quantity = 1, CreateDate = DateTime.Now, FilingNumber = 3, Comment = ""
                } },
                {8, new OrderDishTestModel() {
                    Id = 8, Name ="Блюдо 8", Quantity = 0.5m, CreateDate = DateTime.Now, FilingNumber = 1, Comment = ""
                } },
                {9, new OrderDishTestModel() {
                    Id = 9, Name ="Блюдо 9", Quantity = 1, CreateDate = DateTime.Now, FilingNumber = 1, Comment = ""
                } },
                {10, new OrderDishTestModel() {
                    Id = 10, Name ="Блюдо 10", Quantity = 0.75m, CreateDate = DateTime.Now, FilingNumber = 2, Comment = ""
                } },
                {11, new OrderDishTestModel() {
                    Id = 11, Name ="Блюдо 11", Quantity = 1, CreateDate = DateTime.Now, FilingNumber = 1, Comment = ""
                } },
                {12, new OrderDishTestModel() {
                    Id = 12, Name ="Блюдо 12", Quantity = 2.5m, CreateDate = DateTime.Now, FilingNumber = 1, Comment = ""
                } },
                {13, new OrderDishTestModel() {
                    Id = 13, Name ="Блюдо 13", Quantity = 1, CreateDate = DateTime.Now, FilingNumber = 3, Comment = ""
                } },
            };
        }

    }  // class
}
