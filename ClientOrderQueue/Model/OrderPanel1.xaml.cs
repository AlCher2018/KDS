using ClientOrderQueue.Lib;
using System;
using System.Collections.Generic;
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


namespace ClientOrderQueue.Model
{
    /// <summary>
    /// Interaction logic for OrderPanel.xaml
    /// </summary>
    public partial class OrderPanel1 : UserControl
    {

        #region dependency properties and change handlers
        private Brush BackColor
        {
            get { return (Brush)GetValue(BackColorProperty); }
            set { SetValue(BackColorProperty, value); }
        }
        // Using a DependencyProperty as the backing store for BackColor.  This enables animation, styling, binding, etc...
        private static readonly DependencyProperty BackColorProperty =
            DependencyProperty.Register("BackColor", typeof(Brush), typeof(OrderPanel1), new PropertyMetadata(Brushes.Gold));

        public Brush ForeColor
        {
            get { return (Brush)GetValue(ForeColorProperty); }
            set { SetValue(ForeColorProperty, value); }
        }
        // Using a DependencyProperty as the backing store for ForeColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ForeColorProperty =
            DependencyProperty.Register("ForeColor", typeof(Brush), typeof(OrderPanel1), new PropertyMetadata(Brushes.Black));



        public string OrderNumber
        {
            get { return (string)GetValue(OrderNumberProperty); }
            set { SetValue(OrderNumberProperty, value); }
        }

        // Using a DependencyProperty as the backing store for OrderNumber.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OrderNumberProperty =
            DependencyProperty.Register("OrderNumber", typeof(string), typeof(OrderPanel1), new PropertyMetadata(null, new PropertyChangedCallback(OnOrderNumberChanged)));

        private static void OnOrderNumberChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            OrderPanel1 op = (OrderPanel1)sender;
            string num = op.OrderNumber;
            if (num.Length < 5) op.OrderNumberTail = new string('4', 5 - num.Length);
            else op.OrderNumberTail = "";
        }

        private string OrderNumberTail
        {
            get { return (string)GetValue(OrderNumberTailProperty); }
            set { SetValue(OrderNumberTailProperty, value); }
        }
        // Using a DependencyProperty as the backing store for OrderNumberTail.  This enables animation, styling, binding, etc...
        private static readonly DependencyProperty OrderNumberTailProperty =
            DependencyProperty.Register("OrderNumberTail", typeof(string), typeof(OrderPanel1), new PropertyMetadata(null));


        public string MarginKoefStr
        {
            get { return (string)GetValue(MarginKoefStrProperty); }
            set { SetValue(MarginKoefStrProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MarginKoefStr.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MarginKoefStrProperty =
            DependencyProperty.Register("MarginKoefStr", typeof(string), typeof(OrderPanel1), new PropertyMetadata("0"));

        #endregion

        #region кисти и текстовые строки для различных состояний и языков
        private string[] _titleLangs;
        public string TitleLangs
        {
            set { parseLangStrToArray(value, ref _titleLangs); }
        }

        // статус заказа: 1-готовится, 2-приготовлен, 3-забран
        // на 3-х языках: 1-русский, 2-украинский, 3-английский
        private string[][] _statusLangs;
        public string Status1Langs
        {
            get { return null; }
            set { parseLangStrToArray(value, ref _statusLangs[0]); }
        }
        public string Status2Langs
        {
            get { return null; }
            set { parseLangStrToArray(value, ref _statusLangs[1]); }
        }
        public string Status3Langs
        {
            get { return null; }
            set { parseLangStrToArray(value, ref _statusLangs[2]); }
        }

        private int _orderStatus, _orderLang = 1;
        public int OrderStatus
        {
            get { return _orderStatus; }
            set
            {
                _orderStatus = value;
                setStatus();
            }
        }

        public int OrderLang
        {
            get { return _orderLang; }
            set
            {
                if ((value < 1) || (value > 3)) return;
                if (_orderLang != value)
                {
                    _orderLang = value;
                    setLang();
                }
            }
        }

        // фоновые кисти состояний
        private Brush[] _backBrushes;
        public Brush[] BackBrushes
        {
            set
            {
                _backBrushes = value;
                int minLen = Math.Min(_backBrushes.Length, value.Length);
                for (int i = 0; i < minLen; i++) _backBrushes[i] = value[i];
                setStatus();
            }
        }

        #endregion

        #region client name props
        private bool _isShowClientName;
        public bool IsShowClientName
        {
            get { return _isShowClientName; }
            set
            {
                _isShowClientName = value;
                setLang();
            }
        }

        private string _clientName;
        public string ClientName
        {
            get { return _clientName; }
            set
            {
                _clientName = value;
                setLang();
            }
        }
        #endregion

        #region cooking time
        private bool _isShowCookingTime;
        public bool IsShowCookingTime
        {
            get { return _isShowCookingTime; }
            set
            {
                _isShowCookingTime = value;
                setCookingTimeControls();
                cookingTimerStart();
            }
        }

        // заголовок таймера приготовления
        private string[] _cookingTimeTitleLangs;
        public string CookingTimeTitleLangs
        {
            set { parseLangStrToArray(value, ref _cookingTimeTitleLangs); }
        }

        private void setCookingTimeControls()
        {
            if (_isShowCookingTime)
            {
                Grid.SetRowSpan(this.cntOrderNumber, 1);
                this.grdMain.RowDefinitions[0].Height = new GridLength(1.2d, GridUnitType.Star);
                this.cntCookingTimer.Visibility = Visibility.Visible;
//                this.tbCookingTimer.Text = "00:00:00";
                setLang();
            }
            else
            {
                Grid.SetRowSpan(this.cntOrderNumber, 3);
                this.grdMain.RowDefinitions[0].Height = new GridLength(1d, GridUnitType.Star);
                this.cntCookingTimer.Visibility = Visibility.Collapsed;
            }
        }

        // ожидаемая дата/время, когда блюдо приготовится
        private DateTime _cookingEstDate = DateTime.MinValue;
        // дата создания заказа
        private DateTime _orderCreateDate;
        // ожидаемое время приготовления, в минутах
        private double _cookingEstMinutes = 0d;
        public DateTime OrderCreateDate
        {
            get { return _orderCreateDate; }
            set
            {
                _orderCreateDate = value;
                _cookingEstDate = _orderCreateDate.AddMinutes(_cookingEstMinutes);
            }
        }
        public double CookingEstMinutes
        {
            get { return _cookingEstMinutes; }
            set
            {
                _cookingEstMinutes = value;
                _cookingEstDate = _orderCreateDate.AddMinutes(_cookingEstMinutes);
            }
        }

        // время приготовления заказа
        private Timer _cookingTimer;

        private void cookingTimerStart()
        {
            if (_cookingTimer == null)
            {
                _cookingTimer = new Timer(200d);
                _cookingTimer.AutoReset = true;
                _cookingTimer.Elapsed += _cookingTimer_Elapsed;
            }
            _cookingTimer.Start();
        }

        private void _cookingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _cookingTimer.Enabled = false;

            this.Dispatcher.Invoke(updateCookingTimer);

            _cookingTimer.Enabled = true;
        }

        private void cookingTimerStop()
        {
            if (_cookingTimer != null) _cookingTimer.Stop();
        }

        private void updateCookingTimer()
        {
            if (_cookingEstDate == DateTime.MinValue) return;

            TimeSpan ts = (_cookingEstMinutes == 0 ? DateTime.Now - _cookingEstDate : _cookingEstDate - DateTime.Now);

            ts = getRoundedTimeSpan(ts, 1d);
            string tss = ts.ToStringExt();

            if (this.tbCookingTimer.Text != tss) this.tbCookingTimer.Text = tss;
        }

        private TimeSpan getRoundedTimeSpan(TimeSpan ts, double divider)
        {
            //переводим такты в секунды
            double sec = (double)ts.Ticks / 10000000d;

            //округляем секунды
            var newSec = Math.Round(sec / divider) * divider;

            TimeSpan retVal = TimeSpan.FromSeconds(newSec);
            return retVal;
        }

        #endregion

        #region ready state image
        private string _stateReadyImagePath;
        public string StateReadyImagePath
        {
            get { return _stateReadyImagePath; }
            set
            {
                _stateReadyImagePath = value;

                if (System.IO.File.Exists(_stateReadyImagePath))
                {
                    try
                    {
                        this.imgStat2.Source = new BitmapImage(new Uri(_stateReadyImagePath, UriKind.RelativeOrAbsolute));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
        }
        public BitmapImage StateReadyImage { set { this.imgStat2.Source = value; } }
        #endregion


        public OrderPanel1()
        {
            InitializeComponent();

            // кисти по умолчанию для 3-х состояний: готовится, готово и выдано
            _backBrushes = new Brush[] { Brushes.Gold, Brushes.LimeGreen, Brushes.Orange };

            parseLangStrToArray("Заказ|Замовлення|Order", ref _titleLangs);

            _statusLangs = new string[3][];
            parseLangStrToArray("Готовится|Готується|In process", ref _statusLangs[0]);
            parseLangStrToArray("Готов|Готово|Done", ref _statusLangs[1]);
            parseLangStrToArray("Забрали|Забрали|Taken", ref _statusLangs[2]);

            parseLangStrToArray("Ожидать|Чекати|Wait", ref _cookingTimeTitleLangs);
        } // ctor

        private void parseLangStrToArray(string strLangs,ref string[] arrLangs)
        {
            if ((strLangs != null) && strLangs.Contains('|'))
            {
                string[] buf = strLangs.Split('|');
                if (buf.Length == 3) arrLangs = buf;
            }
        }

        // статус заказа: 1-готовится, 2-приготовлен, 3-забран
        private void setStatus()
        {
            switch (_orderStatus)
            {
                case 1:
                    this.BackColor = _backBrushes[0];
                    Grid.SetColumnSpan(this.cntStatText, 2);
                    this.imgStat2.Visibility = Visibility.Hidden;
                    break;
                case 2:
                    this.BackColor = _backBrushes[1];
                    Grid.SetColumnSpan(this.cntStatText, 1);
                    this.imgStat2.Visibility = Visibility.Visible;
                    break;

                case 3:
                    this.BackColor = _backBrushes[2];
                    Grid.SetColumnSpan(this.cntStatText, 2);
                    this.imgStat2.Visibility = Visibility.Hidden;
                    break;

                default:
                    this.BackColor = Brushes.LightPink;
                    this.imgStat2.Visibility = Visibility.Hidden;
                    break;
            }
            setLang();
        }

        // язык: 0-русский, 1-украинский, 2-английский
        private void setLang()
        {
            if ((_orderLang < 1) || (_orderLang > 3)) _orderLang = 1;

            if ((_orderStatus >= 1) && (_orderStatus <= 3))
                this.tbStatText.Text = _statusLangs[_orderStatus-1][_orderLang-1];
            else
                this.tbStatText.Text = "Unknown status";

            if (this.IsShowClientName)
                this.tbStatTitle.Text = _clientName;
            else
                this.tbStatTitle.Text = _titleLangs[_orderLang-1];

            if (this.IsShowCookingTime)
                this.tbCookingTimerTitle.Text = _cookingTimeTitleLangs[_orderLang-1];
        }

    } // class
}
