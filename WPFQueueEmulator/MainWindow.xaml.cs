using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WPFEmulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string[] _langNames = { "укр", "рус", "анг" };

        private ObservableCollection<genOrder> _orders = new ObservableCollection<genOrder>();
        private ObservableCollection<genOrderStatus> _ordersStatus = new ObservableCollection<genOrderStatus>();
        private Timer _orderTimer = new Timer();
        private Random rnd = new Random();

        private int _currNumber = 10123;
        private object _threadLockObj;
        private KDSContext _db;
        private bool _enableEvents;

        private string[] _statusNames = { "Готовится", "ГОТОВ", "ВЫДАН" };

        public MainWindow()
        {
            InitializeComponent();

            _threadLockObj = new object();
            _db = new KDSContext();
            _orderTimer.AutoReset = false;
            _orderTimer.Elapsed += _orderTimer_Elapsed;

            lbOrders.ItemsSource = _ordersStatus;
            //rbAuto.IsChecked = true;
            rbManual.IsChecked = true;
            rbRu.IsChecked = true;
        }

        private void rbAuto_Checked(object sender, RoutedEventArgs e)
        {
            createAutoOrder();
            btnPause.Visibility = Visibility.Visible;
            btnPauseOnlyOrders.Visibility = Visibility.Visible;
        }

        #region auto create order
        private void createAutoOrder()
        {
            _orderTimer.Interval = 1000; // rnd.Next(2, 5) * 1000d;
            _orderTimer.Start();
        }

        private void _orderTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke((Action)delegate
            {
                genOrder order = new genOrder(rbAuto.IsChecked ?? false) {
                    Number = _currNumber++,
                    LanguageTypeId = rnd.Next(1, 4)
                };
                order.OrderStatusChanged += NewOrder_StatusEventHandler;

                lock (_threadLockObj)
                {
                    _db.Order.Add(new Order()
                    {
                        CreateDate = order.Date,
                        LanguageTypeId = order.LanguageTypeId,
                        Number = order.Number,
                        QueueStatusId = order.StatusId,
                        OrderStatusId =0, DepartmentId=0,
                        UID = Guid.NewGuid().ToString(),
                        TableNumber = "tableName",
                        RoomNumber ="roomName",
                        StartDate =order.Date,
                        SpentTime =0, Waiter = "waiterName"
                    });
                    _db.SaveChanges();

                    _orders.Add(order);
                    addListStatusItem(order);
                }

                createAutoOrder();
            });
        }

        private void NewOrder_StatusEventHandler(object sender, genOrderStatusChangedArgs e)
        {
            this.Dispatcher.Invoke(() => 
            {
                genOrder order = (genOrder)sender;

                if (e.StatusId == 1)
                {
                    lock (_threadLockObj)
                    {
                        setDBOrderStatus(order.Number, order.StatusId);
                        addListStatusItem(order);
                    }
                }
                else if (e.StatusId == 2)
                {
                    lock (_threadLockObj)
                    {
                        setDBOrderStatus(order.Number, order.StatusId);
                        addListStatusItem(order);

                        order.OrderStatusChanged -= NewOrder_StatusEventHandler;
                        _orders.Remove(order);
                        order = null;
                    }
                }
            });
        }

        private void addListStatusItem(genOrder order)
        {
            genOrderStatus os = new genOrderStatus()
            {
                Number = order.Number,
                StatusId = order.StatusId,
                StatusName = order.StatusName,
                Date = order.Date,
                LanguageId = order.LanguageTypeId,
                LanguageName = _langNames[order.LanguageTypeId-1]
            };
            if (os.StatusId == 1)
            {
                os.Date = order.Date1;
                os.SpanString = order.Date1.Subtract(order.Date).Seconds.ToString();
            }
            else if (os.StatusId == 2)
            {
                os.Date = order.Date2;
                os.SpanString = order.Date2.Subtract(order.Date1).Seconds.ToString();
            }

            _ordersStatus.Add(os);
            lbOrders.ScrollIntoView(os);
        }

        private void setDBOrderStatus(int number, int statusId)
        {
            Order dbOrder = _db.Order.FirstOrDefault(o => o.Number == number);
            if (dbOrder != null)
            {
                dbOrder.QueueStatusId = statusId;
                _db.SaveChanges();
            }
        }
        #endregion

        private void rbManual_Checked(object sender, RoutedEventArgs e)
        {
            setTimers(false,false);
            btnPause.Visibility = Visibility.Hidden;
            btnPauseOnlyOrders.Visibility = Visibility.Hidden;
        }

        private FrameworkElement FindVisualChildrenByName(DependencyObject objectFrom, string childName)
        {
            if (objectFrom == null) return null;

            FrameworkElement retVal = null;
            int iCount = VisualTreeHelper.GetChildrenCount(objectFrom);
            for(int i = 0; i < iCount; i++)
            {
                DependencyObject rawChild = VisualTreeHelper.GetChild(objectFrom, i);
                if (rawChild is FrameworkElement)
                {
                    FrameworkElement curObj = (rawChild as FrameworkElement);
                    if (curObj.Name == childName) retVal = curObj;
                    else retVal = FindVisualChildrenByName(curObj, childName);
                }
                if (retVal != null) return retVal;
            }

            return null;
        }

        private void createOrder_Click(object sender, RoutedEventArgs e)
        {
            genOrder order = new genOrder(false) {
                Number = _currNumber++, LanguageTypeId = getSelectLang()
            };

            lock (_threadLockObj)
            {
                _db.Order.Add(new Order()
                {
                    CreateDate = order.Date,
                    LanguageTypeId = order.LanguageTypeId,
                    Number = order.Number,
                    QueueStatusId = order.StatusId,
                    OrderStatusId = 0,
                    DepartmentId = 0,
                    UID = Guid.NewGuid().ToString(),
                    TableNumber = "tableName",
                    RoomNumber = "roomName",
                    StartDate = order.Date,
                    SpentTime = 0,
                    Waiter = "waiterName"
                });
                _db.SaveChanges();

                _orders.Add(order);
                addListStatusItem(order);
            }
        }

        private void lbOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (rbManual.IsChecked == false) return;

            genOrderStatus os = (genOrderStatus)(sender as ListBox).SelectedItem;
            tbOrderNumber.Text = os.Number.ToString();

            setCheckBoxesValue(os);
        }

        private void cbReady_Checked(object sender, RoutedEventArgs e)
        {
            if (_enableEvents == false) return;

            genOrderStatus os = (genOrderStatus)lbOrders.SelectedItem;
            if (os.StatusName == _statusNames[2]) return;

            os.StatusId = 1;
            os.StatusName = _statusNames[os.StatusId];
            updateBindStatus(os);
        }

        private void cbReady_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_enableEvents == false) return;

            genOrderStatus os = (genOrderStatus)lbOrders.SelectedItem;
            if (os.StatusName == _statusNames[2]) return;

            os.StatusId = 0;
            os.StatusName = _statusNames[os.StatusId];
            updateBindStatus(os);
        }

        private void cbOut_Checked(object sender, RoutedEventArgs e)
        {
            if (_enableEvents == false) return;

            genOrderStatus os = (genOrderStatus)lbOrders.SelectedItem;

            os.StatusId = 2;
            os.StatusName = _statusNames[os.StatusId];
            updateBindStatus(os);
        }

        private void cbOut_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_enableEvents == false) return;

            genOrderStatus os = (genOrderStatus)lbOrders.SelectedItem;

            os.StatusId = 1;
            os.StatusName = _statusNames[os.StatusId];
            updateBindStatus(os);
        }

        private void updateBindStatus(genOrderStatus os)
        {
            setDBOrderStatus(os.Number, os.StatusId);

            DependencyObject obj = lbOrders.ItemContainerGenerator.ContainerFromItem(os);
            FrameworkElement tbStatus = FindVisualChildrenByName((FrameworkElement)obj, "tbStatus");
            if (tbStatus != null)
            {
                BindingExpression be = (tbStatus as TextBlock).GetBindingExpression(TextBlock.TextProperty);
                if (be != null) be.UpdateTarget();
            }

            setCheckBoxesValue(os);
        }
        private void setCheckBoxesValue(genOrderStatus os)
        {
            bool bVal;

            if (os.StatusName == _statusNames[0])
            {
                _enableEvents = false;
                cbReady.IsChecked = false;
                cbOut.IsChecked = false;
                _enableEvents = true;
                return;
            }

            _enableEvents = false;

            bVal = (os.StatusName == _statusNames[1]);
            if ((cbReady.IsChecked ?? false) != bVal) cbReady.IsChecked = bVal;

            bVal = (os.StatusName == _statusNames[2]);
            if ((cbOut.IsChecked ?? false) != bVal) cbOut.IsChecked = bVal;

            _enableEvents = true;
        }

        private void btnPause_Checked(object sender, RoutedEventArgs e)
        {
            btnPause.Content = "Продолжить";
            btnPause.Foreground = Brushes.Red;
            btnPause.FontWeight = FontWeights.Bold;

            System.Windows.Media.Effects.DropShadowEffect effect = (System.Windows.Media.Effects.DropShadowEffect)btnPause.Effect;
            effect.BlurRadius = 0; effect.ShadowDepth = 0;

            setTimers(false, false);
        }

        private void btnPause_Unchecked(object sender, RoutedEventArgs e)
        {
            btnPause.Content = "Пауза всех таймеров";
            btnPause.Foreground = Brushes.Black;
            btnPause.FontWeight = FontWeights.Normal;

            System.Windows.Media.Effects.DropShadowEffect effect = (System.Windows.Media.Effects.DropShadowEffect)btnPause.Effect;
            effect.BlurRadius = 10; effect.ShadowDepth = 3;

            setTimers(true, false);
        }

        private void btnPauseOnlyOrders_Checked(object sender, RoutedEventArgs e)
        {
            btnPauseOnlyOrders.Content = "Продолжить (только заказы)";
            btnPauseOnlyOrders.Foreground = Brushes.Red;
            btnPauseOnlyOrders.FontWeight = FontWeights.Bold;

            System.Windows.Media.Effects.DropShadowEffect effect = (System.Windows.Media.Effects.DropShadowEffect)btnPauseOnlyOrders.Effect;
            effect.BlurRadius = 0; effect.ShadowDepth = 0;

            setTimers(false, true);
        }

        private void btnPauseOnlyOrders_Unchecked(object sender, RoutedEventArgs e)
        {
            btnPauseOnlyOrders.Content = "Пауза (только заказы)";
            btnPauseOnlyOrders.Foreground = Brushes.Black;
            btnPauseOnlyOrders.FontWeight = FontWeights.Normal;

            System.Windows.Media.Effects.DropShadowEffect effect = (System.Windows.Media.Effects.DropShadowEffect)btnPauseOnlyOrders.Effect;
            effect.BlurRadius = 10; effect.ShadowDepth = 3;

            setTimers(true, true);
        }

        private void setTimers(bool isRun, bool onlyOrders)
        {
            if (isRun) {
                _orderTimer.Start();
                if (onlyOrders == false) foreach (genOrder gOrd in _orders) gOrd.StartTimer();
            }
            else {
                _orderTimer.Stop();
                if (onlyOrders == false) foreach (genOrder gOrd in _orders) gOrd.StopTimer();
            }

        }

        private void rbLang_Click(object sender, RoutedEventArgs e)
        {
            int selLang = getSelectLang();
//            MessageBox.Show("select lang - " + selLang.ToString());
        }

        private int getSelectLang()
        {
            return (rbUa.IsChecked ?? false) ? 1 : (rbRu.IsChecked ?? false) ? 2 : 3;
        }

    }  // class
}
