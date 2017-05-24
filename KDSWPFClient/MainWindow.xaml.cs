﻿using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.View;
using KDSWPFClient.ViewModel;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using KDSWPFClient.Model;

namespace KDSWPFClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static Timer _timer;   // статический, чтобы не было повторяющихся чисел

        private AppDataProvider _dataProvider;

        // страницы заказов
        private OrdersPages _pages;
        // фильтр заказов на данном КДС. Может быть статическим (отделы из config-файла) или динамическим (статус заказов) 
        private ValueChecker<OrderDishModel> _valueDishChecker;
        private List<OrderViewModel> _viewOrders;
        //private List<TestData.OrderTestModel> _viewOrders;

        // временные списки для удаления неразрешенных блюд/заказов, т.к. от службы получаем ВСЕ блюда и ВСЕ заказы в нетерминальных состояниях
        private List<OrderModel> _delOrderIds;
        private List<int> _delDishIds;  
        private bool _isUpdateLayout = false;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            _dataProvider = (AppDataProvider)AppLib.GetAppGlobalValue("AppDataProvider");

            double topBotMargValue = (double)AppLib.GetAppGlobalValue("dishesPanelTopBotMargin");
            this.vbxOrders.Margin = new Thickness(0, topBotMargValue, 0, topBotMargValue);

            _pages = new OrdersPages();
            _viewOrders = new List<OrderViewModel>();
            // debug test data
            //Button_Click(null,null);
            // условия отбора блюд
            _valueDishChecker = new ValueChecker<OrderDishModel>();
            setDishCheckerForDepIds();

            _timer = new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();

            // кнопки переключения страниц
            btnSetPagePrevious.Width = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPagePrevious.Height = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPageNext.Width = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPageNext.Height = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");

            _delOrderIds = new List<OrderModel>(); _delDishIds = new List<int>();
        }


        private void setDishCheckerForDepIds()
        {
            // - отделы, разрешенные на данном КДС (из config-файла)
            string sAllowDeps = (string)AppLib.GetAppGlobalValue("depUIDs");
            if (sAllowDeps.IsNull() == false)
            {
                _valueDishChecker.Update("depId", (OrderDishModel dish) => sAllowDeps.Contains(dish.Department.UID));
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if ((_timer != null) && _timer.Enabled)
            {
                _timer.Stop(); _timer.Dispose();
            }
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Stop();

            this.Dispatcher.Invoke(new Action(updateOrders));

            _timer.Start();
        }  // method


        // обновить внутреннюю коллекцию заказов данными, полученными от сервиса
        // с учетом фильтрации блюд (состояние и отдел)
        private void updateOrders()
        {
            //OrderModel om = orders[0];
            //string s = string.Format("id: {0}; Number {1}; hallName {2}; dishes count: {3}", om.Id, om.Number, om.HallName, om.Dishes.Count);
            //Debug.Print(s);

            // получить заказы от сервиса
            List<OrderModel> svcOrders = _dataProvider.GetOrders();
            if (svcOrders == null) return;

            // удалить из svcOrders блюда, не входящие в условия фильтрации
            // TODO добавить в чекер разрешенные состояния из текущей роли
            // не здесь!!! где хранить чекер для внешних изменений
//            List<OrderStatusEnum> allowedStates = KDSModeHelper.GetKDSModeAllowedStates((KDSModeEnum)AppLib.GetAppGlobalValue("KDSMode"));

            _delOrderIds.Clear(); _delDishIds.Clear();
            foreach (OrderModel orderMode in svcOrders)
            {
                // собрать Id блюд для удаления
                foreach (KeyValuePair<int, OrderDishModel> item in orderMode.Dishes)
                    if (_valueDishChecker.Checked(item.Value) == false) _delDishIds.Add(item.Key);

                _delDishIds.ForEach(key => orderMode.Dishes.Remove(key)); // удалить неразрешенные блюда

                if (orderMode.Dishes.Count == 0) _delOrderIds.Add(orderMode);
            }
            //   и заказы, у которых нет разрешенных блюд
            _delOrderIds.ForEach(o => svcOrders.Remove(o));

            Debug.Print("orders {0}", svcOrders.Count);
            svcOrders.ForEach(o => Debug.Print("   order id {0}, dishes {1}", o.Id, o.Dishes.Count));

            // *****
            //  В svcOrder<orderModel> НАХОДИТСЯ СПИСОК, КОТОРЫЙ НЕОБХОДИМО ОТОБРАЗИТЬ НА ЭКРАНЕ
            // *****
            // *** ОБНОВИТЬ _viewOrdes ДАННЫМИ ИЗ svcOrders
            bool isViewRepaint = false;   // признак необходимости перерисовать панели заказов на экране
            // удаление заказов
            List<int> delIds = _viewOrders.Select(vo => vo.Id).Except(svcOrders.Select(o => o.Id)).ToList();
            OrderViewModel orderView;
            foreach (int delId in delIds)
            {
                orderView = _viewOrders.Find(vo => vo.Id == delId);
                if (orderView != null)
                {
                    //_pages.RemoveOrderPanel(orderView);  // удалить панель заказа со страницы
                    _viewOrders.Remove(orderView);  // удалить заказ из внутр.коллекции
                }
                if (isViewRepaint == false) isViewRepaint = true;   // перерисовать
            }
            // обновление списка блюд в заказах
            foreach (OrderModel svcOrder in svcOrders)
            {
                orderView = _viewOrders.FirstOrDefault(o => o.Id == svcOrder.Id);
                if (orderView == null)   // из БД (от службы) пришел новый заказ! - добавить заказ в _viewOrders
                {
                    _viewOrders.Add(new OrderViewModel(svcOrder));
                    if (isViewRepaint == false) isViewRepaint = true;   // перерисовать
                }
                else
                {
                    orderView.UpdateFromSvc(svcOrder);
                    if (orderView.IsDishesListUpdated && !isViewRepaint) isViewRepaint = true;
                }
            }

            // перерисовать полностью
            if (isViewRepaint == true)
            {
                DateTime dt = DateTime.Now;
                _pages.ClearPages(); // очистить панели заказов
                Debug.Print("CLEAR orders - {0}", DateTime.Now - dt);

                // добавить заказы
                dt = DateTime.Now;
                _pages.AddOrdersPanels(_viewOrders);
                Debug.Print("CREATE orders - {0}", DateTime.Now - dt);

                setChangePageButtonsState();

                this.vbxOrders.Child = _pages.CurrentPage;
            }

        }  // method


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            StateChange chWin = new StateChange();
            //chWin.Order = _viewOrders[0];
            

            chWin.ShowDialog();

            //_viewOrders = TestData.TestDataHelper.GetTestOrders(5, 30);

            //updateViewOrders();
        }



        private void setChangePageButtonsState()
        {
            btnSetPagePrevious.Visibility = Visibility.Hidden;
            btnSetPageNext.Visibility = Visibility.Hidden;
            if (_pages.Count == 0) return;

            // состояние кнопки перехода на предыдущюю страницу
            if ((_pages.CurrentPageIndex - 1) > 0)
            {
                tbPagePreviousNum.Text = "Стр. " + (_pages.CurrentPageIndex - 1).ToString();
                btnSetPagePrevious.Visibility = Visibility.Visible;
            }
            // и на следующую страницу
            else if (_pages.CurrentPageIndex < _pages.Count)
            {
                tbPageNextNum.Text = "Стр. " + (_pages.CurrentPageIndex + 1).ToString();
                btnSetPageNext.Visibility = Visibility.Visible;
            }
        }

        private void btnSetPageNext_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_pages.SetNextPage())
            {
                this.vbxOrders.Child = _pages.CurrentPage;
                setChangePageButtonsState();
            }
        }

        private void btnSetPagePrevious_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_pages.SetPreviousPage())
            {
                this.vbxOrders.Child = _pages.CurrentPage;
                setChangePageButtonsState();
            }
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            string sAllowDepsPre = (string)AppLib.GetAppGlobalValue("depUIDs");

            ConfigEdit cfgEdit = new ConfigEdit() { DepartmentsDict = _dataProvider.Departments };
            cfgEdit.ShowDialog();

            // не поменялись ли разрешенные отделы?
            string sAllowDepsNew = (string)AppLib.GetAppGlobalValue("depUIDs");
            if (sAllowDepsNew.Equals(sAllowDepsPre) == false)
            {
                // обновить фильтр блюд
                setDishCheckerForDepIds();  
            }


            _timer.Start();
        }


        #region inner classes
        // тип TObj - тип объекта, для которого будет проверяться предикат
        private class ValueChecker<T>
        {
            private List<KeyValuePair<string, Predicate<T>>> _predicatesList;

            public ValueChecker()
            {
                _predicatesList = new List<KeyValuePair<string, Predicate<T>>>();
            }

            // все условия должны быть true, т.е. соединяем по AND
            public bool Checked(T checkObject)
            {
                foreach (KeyValuePair<string, Predicate<T>> item in _predicatesList)
                {
                    if (item.Value(checkObject) == false) return false;
                }
                return true;
            }

            public void Add(string key, Predicate<T> predicate)
            {
                _predicatesList.Add(new KeyValuePair<string, Predicate<T>>(key, predicate));
            }

            public void Update(string key, Predicate<T> newPredicate)
            {
                KeyValuePair<string, Predicate<T>> pred = _predicatesList.FirstOrDefault(p => p.Key == key);

                if (pred.Key != null) _predicatesList.Remove(pred);

                _predicatesList.Add(new KeyValuePair<string, Predicate<T>>(key, newPredicate));
            }

            // удалить ВСЕ записи с данным ключем
            public void Remove(string key)
            {
                KeyValuePair<string, Predicate<T>> pred = _predicatesList.FirstOrDefault(p => p.Key == key);

                if (pred.Key != null)
                {
                    _predicatesList.Remove(pred);
                }
            }

        }
        #endregion

    }  // class MainWindow
}
