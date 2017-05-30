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
        private int[] _deps;
        private MenuDish[] _menuDishes, _ingredients;

        public MainWindow()
        {
            InitializeComponent();

            _rooms = new string[] {"зал 1","зал 2","мансарда","бильярдная" };
            _tables = new string[] { "стол 1", "стол 2", "стол 3", "стол 4", "стол 5", "стол 6", "стол 7", "стол 8" };
            _waiters = new string[] { "Дийнецька Мар'яна", "Оніщенко Віктор", "Потапенко Роман", "Попович Роман", "Ільясов Іскандер", "Лі Володимир", "Лабай Денис", "Левченко Слава", "Овдієнко Владислав", "Іванова Марта", "Довганич Вероніка", "Цуркан Іван", "Официант СФБ", "Харченко Ольга", "Фастовець Микола" };
            _deps = new int[] { 1, 2, 15,16,17,18,19,20,21,22};
            _menuDishes = createMenuDishes();
            _ingredients = createMenuIngredients();

            rbAutoGen.IsChecked = true;
            _timer = new Timer(1000d);
            _timer.Elapsed += _timer_Elapsed;

            _delegateAutoNewOrder =  new Action(() => createNewOrder(0));
        }


        private void createNewOrder(int newNumber = 0)
        {
            string viewText;
            using (BoardChefTestEntities db = new BoardChefTestEntities())
            {
                Order ord;
                if (newNumber == 0)
                {
                    newNumber = (db.Order.Count() == 0) ? 500 : db.Order.OrderByDescending(o => o.Id).FirstOrDefault().Number + 1;
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
                    viewText = string.Format("{3}заказ {0} создан успешно: Id {1}, блюд {2}", ord.Number, ord.Id, ord.OrderDish.Count, (newNumber==0?"":"ДО"));
                }
                catch (Exception ex)
                {
                    viewText = string.Format("{0}заказ {1} НЕ создан: {2}", (newNumber == 0 ? "" : "ДО"), ord.Number, ex.ToString());
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
                OrderStatusId = 0
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
            int cnt = _rnd.Next(1, 10);
            for (int i = 0; i < cnt; i++)
            {
                OrderDish dish = new OrderDish()
                {
                    OrderId = ord.Id, DishStatusId = 0, DepartmentId = getRndDepartment(),
                    FilingNumber = _rnd.Next(1,3), Quantity = _rnd.Next(1,5),
                    CreateDate = DateTime.Now
                };
                // параметры блюда из меню
                MenuDish menuDish = getRndMenuDish();
                dish.UID = menuDish.UID;
                dish.DishName = menuDish.Name;
                dish.Comment = menuDish.Comment;

                ord.OrderDish.Add(dish);

                // случайные ингредиенты
                if (_rnd.NextDouble() > 0.5d)
                {
                    int cntIngr = _rnd.Next(1, 3);
                    for (int j = 0; j < cntIngr; j++)
                    {
                        OrderDish ingr = new OrderDish()
                        {
                            OrderId = ord.Id, DishStatusId = 0, DepartmentId = dish.DepartmentId,
                            FilingNumber = dish.FilingNumber, Quantity = dish.Quantity,
                            CreateDate = dish.CreateDate
                        };
                        MenuDish menuIngr = getRndMenuIngr();
                        ingr.UID = menuDish.UID;
                        ingr.DishName = menuDish.Name;
                        ingr.Comment = menuDish.Comment;

                        ord.OrderDish.Add(dish);
                    }
                }  // ингредиенты
            }  // цикл по блюдам
        }  // method


        private MenuDish[] createMenuDishes()
        {
            return new MenuDish[] 
            {
                new MenuDish() { UID = "48CFD0E9-13D4-4096-B4F4-2D370B511361", Name="Блюдо 1", Comment=null},
                new MenuDish() { UID = "0C1ECCBA-A905-4988-878E-6508CC54A422", Name="Блюдо 2", Comment=null},
                new MenuDish() { UID = "5752315D-84B5-4AB9-9709-B2173F9B5809", Name="Блюдо 3", Comment="123214цу"},
                new MenuDish() { UID = "0A9D482F-61FC-4F86-937A-BF067FD3B899", Name="Блюдо 4", Comment="ывпвып вапвыапуцкнкеор вапывап вапывап"},
                new MenuDish() { UID = "6B051506-8E49-44A8-840E-B3595110841F", Name="Блюдо 5", Comment=""},
                new MenuDish() { UID = "41071919-4227-424A-9DDF-614A927903DF", Name="Блюдо 6", Comment="dsgerghteh dfbsdb dtghrt dsgfbds sdthrth"},
                new MenuDish() { UID = "71EDE593-1CD1-4B59-BCD3-784A74EDA939", Name="Блюдо 7", Comment=""},
                new MenuDish() { UID = "925EB588-03FF-4111-A7AB-BE29BA6D812A", Name="Блюдо 8", Comment="rtyt"},
                new MenuDish() { UID = "767A2395-8091-492B-A616-013AF39BA4EE", Name="Блюдо 9", Comment="cfggf dfghfghgfh"},
                new MenuDish() { UID = "BE0EA216-91BB-4D14-815A-50E030E2DE50", Name="Блюдо 10", Comment=""},
                new MenuDish() { UID = "B7674E1C-A2CE-4265-8C7C-C279CACC49D7", Name="Блюдо 11", Comment=null},
                new MenuDish() { UID = "E1530FF5-4609-4A63-ABDE-9ED46082DC76", Name="Блюдо 12", Comment="fghtyhdsrt sdfg5ryher"},
                new MenuDish() { UID = "90E48905-A9F1-4642-8CDD-1097495AA59B", Name="Блюдо 13", Comment="dfgdgdsfg ryutyu567 mjrtyuj"},
                new MenuDish() { UID = "642E3196-4EC7-43CD-A516-4FDD10D80DC0", Name="Блюдо 14", Comment=""},
                new MenuDish() { UID = "D18F937E-4CB6-4734-8D71-5D9788DE670B", Name="Блюдо 15", Comment=""},
            };
        }
        private MenuDish getRndMenuDish()
        {
            return _menuDishes[_rnd.Next(1, _menuDishes.Count()) - 1];
        }

        private MenuDish[] createMenuIngredients()
        {
            return new MenuDish[]
            {
                new MenuDish() { UID = "48CFD0E9-13D4-4096-B4F4-2D370B511361", Name="Ингредиент 1", Comment="sdgfaregtehb dgfhj7"},
                new MenuDish() { UID = "0C1ECCBA-A905-4988-878E-6508CC54A422", Name="Ингредиент 2", Comment=""},
                new MenuDish() { UID = "5752315D-84B5-4AB9-9709-B2173F9B5809", Name="Ингредиент 3", Comment="fghtrh54h rfthy"},
                new MenuDish() { UID = "0A9D482F-61FC-4F86-937A-BF067FD3B899", Name="Ингредиент 4", Comment=null},
                new MenuDish() { UID = "6B051506-8E49-44A8-840E-B3595110841F", Name="Ингредиент 5", Comment=""},
                new MenuDish() { UID = "41071919-4227-424A-9DDF-614A927903DF", Name="Ингредиент 6", Comment="dfgh rthert6y45yh fghj"},
                new MenuDish() { UID = "71EDE593-1CD1-4B59-BCD3-784A74EDA939", Name="Ингредиент 7", Comment=null},
                new MenuDish() { UID = "925EB588-03FF-4111-A7AB-BE29BA6D812A", Name="Ингредиент 8", Comment="fdghe"},
                new MenuDish() { UID = "767A2395-8091-492B-A616-013AF39BA4EE", Name="Ингредиент 9", Comment=""},
                new MenuDish() { UID = "BE0EA216-91BB-4D14-815A-50E030E2DE50", Name="Ингредиент 10", Comment="ttthdrth"},
                new MenuDish() { UID = "B7674E1C-A2CE-4265-8C7C-C279CACC49D7", Name="Ингредиент 11", Comment=""},
                new MenuDish() { UID = "E1530FF5-4609-4A63-ABDE-9ED46082DC76", Name="Ингредиент 12", Comment="ryhr dfsghrt drther6 sretwe rd"},
                new MenuDish() { UID = "90E48905-A9F1-4642-8CDD-1097495AA59B", Name="Ингредиент 13", Comment=""},
                new MenuDish() { UID = "642E3196-4EC7-43CD-A516-4FDD10D80DC0", Name="Ингредиент 14", Comment="fghf rthert34e rtyyrj"},
                new MenuDish() { UID = "D18F937E-4CB6-4734-8D71-5D9788DE670B", Name="Ингредиент 15", Comment=""},
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
    }  // class

}
