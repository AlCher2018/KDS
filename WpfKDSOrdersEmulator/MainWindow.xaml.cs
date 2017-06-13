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

            _rooms = new string[] { "зал 1", "зал 2", "мансарда", "бильярдная" };
            _tables = new string[] { "стол 1", "стол 2", "стол 3", "стол 4", "стол 5", "стол 6", "стол 7", "стол 8" };
            _waiters = new string[] { "Дийнецька Мар'яна", "Оніщенко Віктор", "Потапенко Роман", "Попович Роман", "Ільясов Іскандер", "Лі Володимир", "Лабай Денис", "Левченко Слава", "Овдієнко Владислав", "Іванова Марта", "Довганич Вероніка", "Цуркан Іван", "Официант СФБ", "Харченко Ольга", "Фастовець Микола" };
            _deps = new int[] { 1, 2, 15, 16, 17, 18, 19, 20, 21, 22 };
            _delayStartTime = new int[] { 10, 15, 20, 25, 30 };
            _menuDishes = createMenuDishes();
            _ingredients = createMenuIngredients();

            rbAutoGen.IsChecked = true;
            _timer = new Timer(1000d);
            _timer.Elapsed += _timer_Elapsed;

            _delegateAutoNewOrder = new Action(() => createNewOrder(0));

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
            bool isAddDishes = (newNumber != 0);
            string viewText;
            using (BoardChefTestEntities db = new BoardChefTestEntities())
            {
                Order ord;
                // создать новый заказ
                if (newNumber == 0)
                {
                    newNumber = (db.Order.Count() == 0) ? 500 : db.Order.OrderByDescending(o => o.Number).FirstOrDefault().Number + 1;

                    ord = getNewOrder(newNumber, db);
                    if (ord != null)
                    {
                        // добавить в БД блюда и ингредиенты, если есть
                        foreach (OrderDish dish in ord.OrderDish) db.OrderDish.Add(dish);
                        try
                        {
                            db.SaveChanges();
                            viewText = string.Format("Заказ {0} создан успешно: Id {1}, блюд {2}", ord.Number, ord.Id, ord.OrderDish.Count);
                        }
                        catch (Exception ex)
                        {
                            viewText = string.Format("Заказ {0} НЕ создан: {1}", ord.Number, ex.ToString());
                        }
                        putMessageToConsole(viewText);
                    }
                }

                // сделать ДОЗАКАЗ
                else
                {
                    OrderDish curDish;
                    ord = db.Order.FirstOrDefault(o => o.Number == newNumber);
                    if (ord != null)
                    {
                        int cnt = ord.OrderDish.Count;
                        createRndDishes(ord);
                        for (int i = cnt; i < ord.OrderDish.Count; i++)
                        {
                            curDish = ord.OrderDish.ElementAt(i);
                            db.OrderDish.Add(curDish);
                        }
                        try
                        {
                            db.SaveChanges();
                            viewText = string.Format("ДОЗаказ {0} создан успешно: Id {1}, блюд {2}", ord.Number, ord.Id, ord.OrderDish.Count);
                        }
                        catch (Exception ex)
                        {
                            viewText = string.Format("ДОЗаказ {0} НЕ создан: {1}", ord.Number, ex.ToString());
                        }
                        putMessageToConsole(viewText);

                    }
                }  // else

            }  // using
        }  // method

        private void putMessageToConsole(string viewText)
        {
            if (tbOrders.Text.IsNull())
                tbOrders.Text = viewText;
            else
                tbOrders.Text += Environment.NewLine + viewText;
        }

        private Order getNewOrder(int newNumber, BoardChefTestEntities db)
        {
            Order retVal = new Order()
            {
                Number = newNumber,
                UID = Guid.NewGuid().ToString(),
                RoomNumber = getRndRoom(),
                TableNumber = getRndTable(),
                Waiter = getRndWaiter(),
                CreateDate = DateTime.Now,
                OrderStatusId = 1,
                LanguageTypeId = 1, QueueStatusId = 0,
                DivisionColorRGB = getRndDivisionColor()
            };

            // сразу сохранить в БД, чтобы получить Id заказа
            try
            {
                db.Order.Add(retVal);
                db.SaveChanges();
                createRndDishes(retVal);
            }
            catch (Exception ex)
            {
                putMessageToConsole(string.Format("Заказ {0} НЕ создан: {1}", retVal.Number, ex.Message));
                retVal = null;
            }

            return retVal;
        }

        #region случайные значения
        private string getRndRoom()
        {
            return _rooms[_rnd.Next(1, _rooms.Count()) - 1];
        }
        private string getRndTable()
        {
            return _tables[_rnd.Next(1, _tables.Count()) - 1];
        }
        private string getRndWaiter()
        {
            return _waiters[_rnd.Next(1, _waiters.Count()) - 1];
        }
        private int getRndDepartment()
        {
            return _deps[_rnd.Next(1, _deps.Count()) - 1];
        }
        private string getRndDivisionColor()
        {
            if (_rnd.NextDouble() < 0.3d)
            {
                byte[] aColors = new byte[3];
                _rnd.NextBytes(aColors);

                return aColors[0].ToString() + "," + aColors[1].ToString() + "," + aColors[2].ToString();
            }
            else return null;
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
                    OrderId = ord.Id,
                    DishStatusId = 0,
                    FilingNumber = _rnd.Next(1, 3),
                    Quantity = _rnd.Next(1, 5),
                    CreateDate = DateTime.Now,
                    UID = Guid.NewGuid().ToString().Substring(0, 15),
                    DishName = menuDish.Name + ", " + menuDish.DepartmentId.ToString(),
                    Comment = menuDish.Comment,
                    DepartmentId = menuDish.DepartmentId,
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
                            OrderId = ord.Id,
                            DishStatusId = 0,
                            DepartmentId = menuIngr.DepartmentId,
                            DishName = menuIngr.Name + ", " + menuIngr.DepartmentId.ToString(),
                            FilingNumber = dish.FilingNumber,
                            Quantity = dish.Quantity,
                            CreateDate = dish.CreateDate,
                            UID = dish.UID,
                            ParentUid = dish.UID,
                            EstimatedTime = menuIngr.EstimatedTime,
                            Comment = menuIngr.Comment
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
                new MenuDish() { Name="Блюдо 1", DepartmentId = 1, EstimatedTime = 60, Comment=null},
                new MenuDish() { Name="Блюдо 2", DepartmentId = 2, EstimatedTime = 0, Comment=null},
                new MenuDish() { Name="Блюдо 3", DepartmentId = 19, EstimatedTime = 20, Comment="123214цу"},
                new MenuDish() { Name="Блюдо 4", DepartmentId = 20, EstimatedTime = 30, Comment="ывпвып вапвыапуцкнкеор вапывап вапывап"},
                new MenuDish() { Name="Блюдо 5", DepartmentId = 1, EstimatedTime = 0, Comment=""},
                new MenuDish() { Name="Блюдо 6", DepartmentId = 19, EstimatedTime = 300, Comment="dsgerghteh dfbsdb dtghrt dsgfbds sdthrth"},
                new MenuDish() { Name="Блюдо 7", DepartmentId = 1, EstimatedTime = 0, Comment=""},
                new MenuDish() { Name="Блюдо 8", DepartmentId = 19, EstimatedTime = 300, Comment="rtyt"},
                new MenuDish() { Name="Блюдо 9", DepartmentId = 1, EstimatedTime = 600, Comment="cfggf dfghfghgfh"},
                new MenuDish() { Name="Блюдо 10", DepartmentId = 15, EstimatedTime = 600, Comment=""},
                new MenuDish() { Name="Блюдо 11", DepartmentId = 20, EstimatedTime = 0, Comment=null},
                new MenuDish() { Name="Блюдо 12", DepartmentId = 15, EstimatedTime = 100, Comment="fghtyhdsrt sdfg5ryher"},
                new MenuDish() { Name="Блюдо 13", DepartmentId = 19, EstimatedTime = 0, Comment="dfgdgdsfg ryutyu567 mjrtyuj"},
                new MenuDish() { Name="Блюдо 14", DepartmentId = 15, EstimatedTime = 180, Comment=""},
                new MenuDish() { Name="Блюдо 15", DepartmentId = 20, EstimatedTime = 180, Comment=""},
            };
        }
        private MenuDish getRndMenuDish()
        {
            return _menuDishes[_rnd.Next(1, _menuDishes.Count() + 1) - 1];
        }

        private MenuDish[] createMenuIngredients()
        {
            return new MenuDish[]
            {
                new MenuDish() { Name="Ингредиент 1", DepartmentId = 1, EstimatedTime=0, Comment="sdgfaregtehb dgfhj7"},
                new MenuDish() { Name="Ингредиент 2", DepartmentId = 1, EstimatedTime=120, Comment=""},
                new MenuDish() { Name="Ингредиент 3", DepartmentId = 2, EstimatedTime=300, Comment="fghtrh54h rfthy"},
                new MenuDish() { Name="Ингредиент 4", DepartmentId = 2, EstimatedTime=0, Comment=null},
                new MenuDish() { Name="Ингредиент 5", DepartmentId = 2, EstimatedTime=0, Comment=""},
                new MenuDish() { Name="Ингредиент 6", DepartmentId = 15, EstimatedTime=600, Comment="dfgh rthert6y45yh fghj"},
                new MenuDish() { Name="Ингредиент 7", DepartmentId = 15, EstimatedTime=180, Comment=null},
                new MenuDish() { Name="Ингредиент 8", DepartmentId = 15, EstimatedTime=120, Comment="fdghe"},
                new MenuDish() { Name="Ингредиент 9", DepartmentId = 19, EstimatedTime=0, Comment=""},
                new MenuDish() { Name="Ингредиент 10", DepartmentId = 19, EstimatedTime=300, Comment="ttthdrth"},
                new MenuDish() { Name="Ингредиент 11", DepartmentId = 19, EstimatedTime=300, Comment=""},
                new MenuDish() { Name="Ингредиент 12", DepartmentId = 20, EstimatedTime=0, Comment="ryhr dfsghrt drther6 sretwe rd"},
                new MenuDish() { Name="Ингредиент 13", DepartmentId = 20, EstimatedTime=0, Comment=""},
                new MenuDish() { Name="Ингредиент 14", DepartmentId = 20, EstimatedTime=180, Comment="fghf rthert34e rtyyrj"},
                new MenuDish() { Name="Ингредиент 15", DepartmentId = 20, EstimatedTime=0, Comment=""},
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

        private void btnTest1_Click(object sender, RoutedEventArgs e)
        {
            DateTime dt = DateTime.Now;
            Order ord = new Order()
            {
                Number = 500,
                UID = Guid.NewGuid().ToString(),
                RoomNumber = getRndRoom(),
                TableNumber = getRndTable(),
                Waiter = getRndWaiter(),
                CreateDate = dt, StartDate = dt,
                OrderStatusId = 0,
                LanguageTypeId = 1,
                QueueStatusId = 0
            };
            using (BoardChefTestEntities db = new BoardChefTestEntities())
            {
                db.Database.ExecuteSqlCommand("delete from [OrderDish]; delete from [Order]");

                db.Order.Add(ord);
                db.SaveChanges();

                db.OrderDish.Add(new OrderDish()
                {
                    OrderId = ord.Id,
                    DishStatusId = 0,
                    Quantity = 5,
                    FilingNumber = 1,
                    CreateDate = ord.CreateDate, StartDate = ord.CreateDate,
                    UID = Guid.NewGuid().ToString().Substring(0, 15),
                    DishName = "Пицца 1",
                    DepartmentId = 17,
                    DelayedStartTime = 0,
                    EstimatedTime = 480
                });
                db.OrderDish.Add( new OrderDish()
                {
                    OrderId = ord.Id,
                    DishStatusId = 0,
                    Quantity = 1,
                    FilingNumber = 1,
                    CreateDate = ord.CreateDate, StartDate = ord.CreateDate,
                    UID = Guid.NewGuid().ToString().Substring(0, 15),
                    DishName = "Пицца 2",
                    DepartmentId = 17,
                    DelayedStartTime = 0,
                    EstimatedTime = 480
                });
                db.OrderDish.Add(new OrderDish()
                {
                    OrderId = ord.Id,
                    DishStatusId = 0,
                    Quantity = 1,
                    FilingNumber = 1,
                    CreateDate = ord.CreateDate,
                    StartDate = ord.CreateDate,
                    UID = Guid.NewGuid().ToString().Substring(0, 15),
                    DishName = "Пицца 3",
                    DepartmentId = 17,
                    DelayedStartTime = 0,
                    EstimatedTime = 480
                });
                db.OrderDish.Add(new OrderDish()
                {
                    OrderId = ord.Id,
                    DishStatusId = 0,
                    Quantity = 1,
                    FilingNumber = 1,
                    CreateDate = ord.CreateDate,
                    StartDate = ord.CreateDate,
                    UID = Guid.NewGuid().ToString().Substring(0, 15),
                    DishName = "Пицца 4",
                    DepartmentId = 17,
                    DelayedStartTime = 0,
                    EstimatedTime = 480
                });

                db.SaveChanges();
            }
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
        public string Name { get; set; }
        public string Comment { get; set; }
        public int DepartmentId { get; set; }

        // плановое время приготовления
        public int EstimatedTime { get; set; }

    }  // class

}
