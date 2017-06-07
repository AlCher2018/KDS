using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;


namespace WpfKDSOrdersEmulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static Random _rnd = new Random();

        private Timer _timer;
        private Action _delegateAutoNewOrder;

        private string[] _rooms, _tables, _waiters;
        private int[] _deps, _delayStartTime;
        private MenuDish[] _menuDishes, _ingredients;

        public MainWindow()
        {
            InitializeComponent();

            _rooms = new string[] {"зал 1","зал 2","мансарда","бильярдная" };
            _tables = new string[] { "стол 1", "стол 2", "стол 3", "стол 4", "стол 5", "стол 6", "стол 7", "стол 8" };
            _waiters = new string[] { "Дийнецька Мар'яна", "Оніщенко Віктор", "Потапенко Роман", "Попович Роман", "Ільясов Іскандер", "Лі Володимир", "Лабай Денис", "Левченко Слава", "Овдієнко Владислав", "Іванова Марта", "Довганич Вероніка", "Цуркан Іван", "Официант СФБ", "Харченко Ольга", "Фастовець Микола" };
            _deps = new int[] { 1, 2, 15,16,17,18,19,20,21,22};
            _delayStartTime = new int[] { 10, 15, 20, 25, 30};
            _menuDishes = createMenuDishes();
            _ingredients = createMenuIngredients();

            rbAutoGen.IsChecked = true;
            _timer = new Timer(1000d);
            _timer.Elapsed += _timer_Elapsed;

            _delegateAutoNewOrder =  new Action(() => createNewOrder(0));

            //clearDB();
        }

        private void clearDB()
        {
            using (BoardChefTestEntities db = new BoardChefTestEntities())
            {
                db.Database.ExecuteSqlCommand("delete from [OrderDish]; delete from [Order]");
            }
        }

        private void createNewOrder(int newNumber = 0)
        {
            string viewText;
            bool isSubOrder = (newNumber != 0);
            using (BoardChefTestEntities db = new BoardChefTestEntities())
            {
                Order ord;
                if (newNumber == 0)
                {
                    newNumber = (db.Order.Count() == 0) ? 500 : db.Order.OrderByDescending(o => o.Number).FirstOrDefault().Number + 1;
                    ord = getNewOrder(newNumber);
                }
                // дозаказ
                else
                {
                    Order ordMain = db.Order.FirstOrDefault(o => o.Number == newNumber);
                    ord = getNewOrder(newNumber, ordMain);
                }
                db.Order.Add(ord);

                // добавить в БД блюда и ингредиенты, если есть
                foreach (OrderDish dish in ord.OrderDish) db.OrderDish.Add(dish);

                try
                {
                    db.SaveChanges();
                    viewText = string.Format("{3}заказ {0} создан успешно: Id {1}, блюд {2}", ord.Number, ord.Id, ord.OrderDish.Count, (isSubOrder ? "ДО" : ""));
                }
                catch (Exception ex)
                {
                    viewText = string.Format("{0}заказ {1} НЕ создан: {2}", (isSubOrder ? "ДО" : ""), ord.Number, ex.ToString());
                }
            }

            if (tbOrders.Text.IsNull()) tbOrders.Text = viewText;
            else tbOrders.Text += Environment.NewLine + viewText;
        }

        private Order getNewOrder(int newNumber, Order baseOrder = null)
        {
            Order retVal = new Order()
            {
                Number = newNumber,
                UID = Guid.NewGuid().ToString(),
                RoomNumber = (baseOrder == null) ? getRndRoom() : baseOrder.RoomNumber,
                TableNumber = (baseOrder == null) ? getRndTable() : baseOrder.TableNumber,
                Waiter = (baseOrder == null) ? getRndWaiter() : baseOrder.Waiter,
                CreateDate = DateTime.Now,
                OrderStatusId = 1
            };
            createRndDishes(retVal);

            return retVal;
        }

        #region случайные значения
        private string getRndRoom()
        {
            return _rooms[_rnd.Next(1,_rooms.Count())-1];
        }
        private string getRndTable()
        {
            return _tables[_rnd.Next(1,_tables.Count())-1];
        }
        private string getRndWaiter()
        {
            return _waiters[_rnd.Next(1, _waiters.Count()) - 1];
        }
        private int getRndDepartment()
        {
            return _deps[_rnd.Next(1, _deps.Count()) - 1];
        }

        // случайные блюда
        private void createRndDishes(Order ord)
        {
            int cnt = _rnd.Next(1, 7);
            for (int i = 0; i < cnt; i++)
            {
                // параметры блюда из меню
                MenuDish menuDish = getRndMenuDish();
                OrderDish dish = new OrderDish()
                {
                    OrderId = ord.Id, DishStatusId = 0, FilingNumber = _rnd.Next(1,3), Quantity = _rnd.Next(1,5), CreateDate = DateTime.Now,
                    UID = menuDish.UID, DishName = menuDish.Name, Comment = menuDish.Comment, DepartmentId = menuDish.DepartmentId,
                    DelayedStartTime = (_rnd.NextDouble() > 0.5d) ? _delayStartTime[_rnd.Next(1, _delayStartTime.Count()) - 1] : 0,
                    EstimatedTime = menuDish.EstimatedTime
                };

                ord.OrderDish.Add(dish);

                // случайные ингредиенты
                if (_rnd.NextDouble() > 0.5d)
                {
                    int cntIngr = _rnd.Next(1, 3);
                    for (int j = 0; j < cntIngr; j++)
                    {
                        MenuDish menuIngr = getRndMenuIngr();
                        OrderDish ingr = new OrderDish()
                        {
                            OrderId = ord.Id, DishStatusId = 0, DepartmentId = getRndDepartment(),
                            FilingNumber = dish.FilingNumber, Quantity = dish.Quantity,
                            CreateDate = dish.CreateDate,
                            UID = dish.UID, ParentUid = dish.UID,
                            DishName = menuIngr.Name, Comment = menuIngr.Comment,
                            EstimatedTime = menuIngr.EstimatedTime
                        };

                        ord.OrderDish.Add(ingr);
                    }
                }  // ингредиенты
            }  // цикл по блюдам
        }  // method


        private MenuDish[] createMenuDishes()
        {
            return new MenuDish[] 
            {
                new MenuDish() { UID = "48CFD0E91", Name="Блюдо 1", Comment=null, DepartmentId = 1, EstimatedTime = 60},
                new MenuDish() { UID = "6508CC54A422", Name="Блюдо 2", Comment=null, DepartmentId = 2, EstimatedTime = 0},
                new MenuDish() { UID = "B2173F9B5809", Name="Блюдо 3", Comment="123214цу", DepartmentId = 1, EstimatedTime = 20},
                new MenuDish() { UID = "0A9D482F", Name="Блюдо 4", Comment="ывпвып вапвыапуцкнкеор вапывап вапывап", DepartmentId = 1, EstimatedTime = 30}
                //new MenuDish() { UID = "6B0515068E49", Name="Блюдо 5", Comment="", DepartmentId = 1, EstimatedTime = 0},
                //new MenuDish() { UID = "410719194227", Name="Блюдо 6", Comment="dsgerghteh dfbsdb dtghrt dsgfbds sdthrth", DepartmentId = getRndDepartment(), EstimatedTime = 300},
                //new MenuDish() { UID = "784A74EDA939", Name="Блюдо 7", Comment="", DepartmentId = getRndDepartment(), EstimatedTime = 0},
                //new MenuDish() { UID = "BE29BA6D812A", Name="Блюдо 8", Comment="rtyt", DepartmentId = getRndDepartment(), EstimatedTime = 300},
                //new MenuDish() { UID = "8091", Name="Блюдо 9", Comment="cfggf dfghfghgfh", DepartmentId = getRndDepartment(), EstimatedTime = 600},
                //new MenuDish() { UID = "4D14815A", Name="Блюдо 10", Comment="", DepartmentId = getRndDepartment(), EstimatedTime = 600},
                //new MenuDish() { UID = "A2CE", Name="Блюдо 11", Comment=null, DepartmentId = getRndDepartment(), EstimatedTime = 0},
                //new MenuDish() { UID = "9ED46082DC76", Name="Блюдо 12", Comment="fghtyhdsrt sdfg5ryher", DepartmentId = getRndDepartment(), EstimatedTime = 100},
                //new MenuDish() { UID = "1097495AA59B", Name="Блюдо 13", Comment="dfgdgdsfg ryutyu567 mjrtyuj", DepartmentId = getRndDepartment(), EstimatedTime = 0},
                //new MenuDish() { UID = "A516D80DC0", Name="Блюдо 14", Comment="", DepartmentId = getRndDepartment(), EstimatedTime = 180},
                //new MenuDish() { UID = "788DE670B", Name="Блюдо 15", Comment="", DepartmentId = getRndDepartment(), EstimatedTime = 180},
            };
        }
        private MenuDish getRndMenuDish()
        {
            return _menuDishes[_rnd.Next(1, _menuDishes.Count()+1) - 1];
        }

        private MenuDish[] createMenuIngredients()
        {
            return new MenuDish[]
            {
                new MenuDish() { Name="Ингредиент 1", Comment="sdgfaregtehb dgfhj7", EstimatedTime=0},
                new MenuDish() { Name="Ингредиент 2", Comment="", EstimatedTime=120},
                new MenuDish() { Name="Ингредиент 3", Comment="fghtrh54h rfthy", EstimatedTime=300},
                new MenuDish() { Name="Ингредиент 4", Comment=null, EstimatedTime=0},
                new MenuDish() { Name="Ингредиент 5", Comment="", EstimatedTime=0},
                new MenuDish() { Name="Ингредиент 6", Comment="dfgh rthert6y45yh fghj", EstimatedTime=600},
                new MenuDish() { Name="Ингредиент 7", Comment=null, EstimatedTime=180},
                new MenuDish() { Name="Ингредиент 8", Comment="fdghe", EstimatedTime=120},
                new MenuDish() { Name="Ингредиент 9", Comment="", EstimatedTime=0},
                new MenuDish() { Name="Ингредиент 10", Comment="ttthdrth", EstimatedTime=300},
                new MenuDish() { Name="Ингредиент 11", Comment="", EstimatedTime=300},
                new MenuDish() { Name="Ингредиент 12", Comment="ryhr dfsghrt drther6 sretwe rd", EstimatedTime=0},
                new MenuDish() { Name="Ингредиент 13", Comment="", EstimatedTime=0},
                new MenuDish() { Name="Ингредиент 14", Comment="fghf rthert34e rtyyrj", EstimatedTime=180},
                new MenuDish() { Name="Ингредиент 15", Comment="", EstimatedTime=0},
            };
        }
        private MenuDish getRndMenuIngr()
        {
            return _ingredients[_rnd.Next(1, _ingredients.Count()) - 1];
        }

        #endregion

        #region различные обработчики
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(_delegateAutoNewOrder);
        }

        private void rbAutoGen_Checked(object sender, RoutedEventArgs e)
        {
            enableAutoGen();
        }

        private void rbManualGen_Checked(object sender, RoutedEventArgs e)
        {
            enableManualGen();
        }

        private void btnAutoGenStart_Click(object sender, RoutedEventArgs e)
        {
            switchTimer();
        }
        private void btnGenerateNewOrder_Click(object sender, RoutedEventArgs e)
        {
            createNewOrder();
        }
        //  создать ДОзаказ
        private void btnGenerateSubOrder_Click(object sender, RoutedEventArgs e)
        {
//            string temp = Microsoft.VisualBasic.Interaction.InputBox("prompt", "title", "0");

            // CoolButton Clicked! Let's show our InputBox.
            InputBox.Visibility = System.Windows.Visibility.Visible;
            InputTextBox.Focus();
        }
        #endregion

        #region простое диалоговое окно
        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            // YesButton Clicked! Let's hide our InputBox and handle the input text.
            InputBox.Visibility = System.Windows.Visibility.Collapsed;

            // Do something with the Input
            string input = InputTextBox.Text;
            if (input.IsNull() == false) createNewOrder(input.ToInt());

            // Clear InputBox.
            InputTextBox.Text = String.Empty;
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            // NoButton Clicked! Let's hide our InputBox.
            InputBox.Visibility = System.Windows.Visibility.Collapsed;

            // Clear InputBox.
            InputTextBox.Text = String.Empty;
        }
        #endregion

        #region таймер автоматической генерации заказов
        private void enableAutoGen()
        {
            btnAutoGenStart.IsEnabled = true;
            btnAutoGenStart.Tag = "0";

            btnGenerateNewOrder.IsEnabled = false;
        }

        private void enableManualGen()
        {
            btnAutoGenStart.IsEnabled = false;
            btnGenerateNewOrder.IsEnabled = true;

            btnAutoGenStart.Tag = "0";
            if (_timer.Enabled) _timer.Stop();
        }

        private void switchTimer()
        {
            string switcher = btnAutoGenStart.Tag.ToString();

            if (switcher == "0")
            {
                _timer.Start();
            }
            else { _timer.Stop(); }


            btnAutoGenStart.Tag = (switcher == "0") ? "1" : "0";
        }

        #endregion

    }  // class

    internal class MenuDish
    {
        public string UID { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public int DepartmentId { get; set; }

        // плановое время приготовления
        public int EstimatedTime { get; set; }

    }  // class

}
