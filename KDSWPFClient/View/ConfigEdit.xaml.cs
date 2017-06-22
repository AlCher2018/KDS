using CAVControls;
using KDSWPFClient.Lib;
using KDSWPFClient.Model;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.ViewModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace KDSWPFClient.View
{
    /// <summary>
    /// Interaction logic for ConfigEdit.xaml
    /// </summary>
    public partial class ConfigEdit : Window
    {
        Dictionary<int, DepartmentViewModel> _deps;
        public Dictionary<int, DepartmentViewModel> DepartmentsDict { get { return _deps; } set { _deps = value; } }

        // для определения измененных значений
        private CfgValueKeeper _cfgValKeeper;
        private Dictionary<string, string> _appNewSettings;
        public Dictionary<string, string> AppNewSettings { get { return _appNewSettings; } }

        private bool _useReadyConfirmedState;

        // звуки
        string _audioPath;
        System.Media.SoundPlayer _wavPlayer;


        public ConfigEdit()
        {
            InitializeComponent();
            
            this.Loaded += ConfigEdit_Loaded;

            _cfgValKeeper = new CfgValueKeeper();
            _appNewSettings = new Dictionary<string, string>();

            // размеры окна
            double screenWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
            double screenHeight = (double)AppLib.GetAppGlobalValue("screenHeight");
            this.Width = 0.67d * screenWidth;
            this.Height = 0.75d * screenHeight;


            // дополнтельные действия в зависимости от подтверджения готовности
            _useReadyConfirmedState =  (bool)AppLib.GetAppGlobalValue("UseReadyConfirmedState", false);
            if (_useReadyConfirmedState)
            {
                cbxState7.Visibility = Visibility.Visible;

                cbx17.Visibility = Visibility.Visible;
                cbx27.Visibility = Visibility.Visible;
                cbx73.Visibility = Visibility.Visible;
            }
            else
            {
                cbxState7.Visibility = Visibility.Collapsed;

                cbx17.Visibility = Visibility.Collapsed;
                cbx27.Visibility = Visibility.Collapsed;
                cbx73.Visibility = Visibility.Collapsed;
            }

            // заполнить комбобокс звуковых файлов
            _audioPath = AppLib.GetAppDirectory("Audio");
            _wavPlayer = new System.Media.SoundPlayer();
            if (Directory.Exists(_audioPath))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(_audioPath);
                List<string> files = new List<string>();

                foreach (FileInfo fileInfo in dirInfo.GetFiles("*.wav", SearchOption.TopDirectoryOnly)) files.Add(fileInfo.Name);
                cbxSelectAudio.ItemsSource = files;

                var defFile = AppLib.GetAppGlobalValue("NewOrderAudioAttention");
                if ((defFile != null) && (files.Contains(defFile)))
                {
                    cbxSelectAudio.SelectedValue = defFile;
                    _wavPlayer.SoundLocation = _audioPath + defFile;
                    _wavPlayer.LoadAsync();
                }
            }
            cbxSelectAudio.SelectionChanged += CbxSelectAudio_SelectionChanged;

        }

        private void ConfigEdit_Loaded(object sender, RoutedEventArgs e)
        {
            if (_deps != null)
            {
                // контрол меняет значение поля IsViewOnKDS в _deps  !!!
                listBoxDepartments.ItemsSource = _deps.Values;
            }

            _cfgValKeeper.AddPreValue("depUIDs", false, null);
            _cfgValKeeper.AddPreValue("IsWriteTraceMessages", true, chkIsWriteTraceMessages);
            _cfgValKeeper.AddPreValue("IsLogUserAction", true, chkIsLogUserAction);
            _cfgValKeeper.AddPreValue("AppFontScale", false, tbFontSizeScale);
            _cfgValKeeper.AddPreValue("OrdersColumnsCount", false, tbxOrdersColumnsCount);
            _cfgValKeeper.AddPreValue("AutoReturnOrdersGroupByTime", false, tbTimerIntervalToOrderGroupByTime);
            _cfgValKeeper.AddPreValueDirectly("NewOrderAudioAttention", (string)cbxSelectAudio.SelectedValue);
            _cfgValKeeper.AddPreValue("OrderHeaderClickable", true, cbxOrderHeaderClickable);
            _cfgValKeeper.AddPreValue("IngrClickable", true, cbxIngrClickable);
            
            // получить от службы
            //AppDataProvider dataProvider = (AppDataProvider)AppLib.GetAppGlobalValue("AppDataProvider");
            //int expTake = dataProvider.GetExpectedTakeValue();
            //_cfgValKeeper.AddPreValueDirectly("ExpectedTake", expTake.ToString(), tbTimerExpectedTake);

            bool isDefault = true;
            if (AppLib.GetAppGlobalValue("KDSMode") != null)
            {
                KDSModeEnum eMode = (KDSModeEnum)AppLib.GetAppGlobalValue("KDSMode");
                _cfgValKeeper.AddPreValueDirectly("KDSMode", eMode.ToString());  // прямое сохранение только в символьном виде

                if (KDSModeHelper.DefinedKDSModes.ContainsKey(eMode))
                {
                    string sStates, sActions;
                    if (eMode == KDSModeEnum.Special)
                    {
                        KDSModeStates modeStates = KDSModeHelper.DefinedKDSModes[KDSModeEnum.Special];
                        sStates = modeStates.AllowedStatesToString();
                        sActions = modeStates.AllowedActionsToString();
                    }
                    else
                    {
                        sStates = ""; sActions = "";
                    }
                    _cfgValKeeper.AddPreValueDirectly("KDSModeSpecialStates", sStates);
                    _cfgValKeeper.AddPreValueDirectly("KDSModeSpecialActions", sActions);

                    isDefault = false;

                    // чекнуть нужную кнопку
                    foreach (RadioButton item in lbxKDSMode.Children.OfType<RadioButton>())
                    {
                        if ((item.Tag != null) && (Convert.ToInt32(item.Tag) == (int)eMode))
                            item.IsChecked = true;
                        else
                            item.IsChecked = false;
                    }
                }
                else
                {
                    _cfgValKeeper.AddPreValueDirectly("KDSModeSpecialStates", "");
                    _cfgValKeeper.AddPreValueDirectly("KDSModeSpecialActions", "");
                }
            }

            // роль КДС по умолчанию
            if (isDefault)
            {
                _cfgValKeeper.AddPreValueDirectly("KDSMode", "");
                _cfgValKeeper.AddPreValueDirectly("KDSModeSpecialStates", "");
                _cfgValKeeper.AddPreValueDirectly("KDSModeSpecialActions", "");

                rbSpecial.IsChecked = true;
            }
        }

        // выбор роли КДСа
        // для предопредл.роли отобразить флажки и дизаблить
        private void rbKDSMode_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rbChecked = (RadioButton)sender;
            KDSModeEnum kdsMode;
            if (Enum.TryParse<KDSModeEnum>(rbChecked.Tag.ToString(), out kdsMode))
            {
                KDSModeStates kdsStates = KDSModeHelper.DefinedKDSModes[kdsMode];
                // установить флажки состояний
                foreach (CheckBox item in pnlStates.Children.OfType<CheckBox>())
                {
                    OrderStatusEnum eStatus = (OrderStatusEnum) Convert.ToInt32(item.Tag);
                    item.IsChecked = (kdsStates.AllowedStates.Contains(eStatus));
                }

                // установить флажки действий
                KeyValuePair<OrderStatusEnum, OrderStatusEnum> kvp;
                foreach (CheckBox item in pnlActions.Children.OfType<CheckBox>())
                {
                    if (item.Tag != null)
                    {
                        kvp = KDSModeHelper.GetStatusPairFromIntPair(item.Tag.ToString());
                        item.IsChecked = ((kvp.Key != OrderStatusEnum.None) && (kdsStates.AllowedActions.Contains(kvp)));
                    }
                }

                if (kdsMode == KDSModeEnum.Special)
                {
                    pnlStates.IsEnabled = true; pnlActions.IsEnabled = true;
                }
                else
                {
                    pnlStates.IsEnabled = false; pnlActions.IsEnabled = false;
                }
            }
        }  // method


        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            //  *** получить напрямую новые значения  ***
            // новые значения из списков
            _cfgValKeeper.PutNewValueDirectly("depUIDs", getIsViewDepIds());

            // роль КДС из контролов
            KDSModeEnum eMode = getKDSModeFromRadioButtons();
            if (eMode != KDSModeEnum.None)
            {
                _cfgValKeeper.PutNewValueDirectly("KDSMode", eMode.ToString());
                string sStates, sActions;
                if (eMode == KDSModeEnum.Special)
                {
                    // получить новые объектные значения разрешений
                    List<OrderStatusEnum> newAllowedStates = getStatesFromCheckBoxes(pnlStates);
                    List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> newAllowedActions = getActionsFromCheckBoxes(pnlActions);
                    sStates = KDSModeHelper.StatesListToString(newAllowedStates); if (sStates == null) sStates = "";
                    sActions = KDSModeHelper.ActionsListToString(newAllowedActions); if (sActions == null) sActions = "";
                }
                else
                {
                    sStates = ""; sActions = "";
                }
                // записать их новые значения в кипер
                _cfgValKeeper.PutNewValueDirectly("KDSModeSpecialStates", sStates);
                _cfgValKeeper.PutNewValueDirectly("KDSModeSpecialActions", sActions);
            }

            _cfgValKeeper.PutNewValueDirectly("NewOrderAudioAttention", (string)cbxSelectAudio.SelectedValue);

            // *** получить новые значения из контролов  ***
            _cfgValKeeper.PutNewValueFromControls();

            // собрать словарь новых значений
            _appNewSettings = _cfgValKeeper.GetNewValuesDict();
            // сделаны ли какие-нибудь изменения?
            if (_appNewSettings.Count > 0)
            {
                string errMsg = null;
                string sBuf;
                if (AppLib.SaveAppSettings(_appNewSettings, out errMsg))  // сохранить в config-файле (все в символьном виде)
                {
                    // для объектов, взятых из AppProps, сохранить туда
                    _cfgValKeeper.SaveToAppProps();  
                    
                    // для некоторых значений может понадобиться преобразование типов для сохранения в App.Properties
                    if (_appNewSettings.ContainsKey("KDSMode") && (_appNewSettings["KDSMode"].IsNull() == false))
                    {
                        if (Enum.TryParse<KDSModeEnum>(_appNewSettings["KDSMode"], out eMode)) AppLib.SetAppGlobalValue("KDSMode", eMode); 
                    }
                    // особые состояния и действия хранятся не в App.Properties, а в четвертом элементе KDSModeStates и в config-e !!
                    if (_appNewSettings.ContainsKey("KDSModeSpecialStates"))
                    {
                        sBuf = _appNewSettings["KDSModeSpecialStates"];
                        AppLib.SetAppGlobalValue("KDSModeSpecialStates", sBuf);

                        KDSModeStates modeStates = KDSModeHelper.DefinedKDSModes[KDSModeEnum.Special];
                        modeStates.StringToAllowedStates(sBuf);
                        modeStates.CreateUserStateSets();
                    }
                    if (_appNewSettings.ContainsKey("KDSModeSpecialActions"))
                    {
                        sBuf = _appNewSettings["KDSModeSpecialActions"];
                        AppLib.SetAppGlobalValue("KDSModeSpecialActions", sBuf);
                        KDSModeHelper.DefinedKDSModes[KDSModeEnum.Special].StringToAllowedActions(sBuf);
                    }

                    MessageBox.Show("Настройки успешно сохранены в config-файле!", "Сохранение настроек в config-файле", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Ошибка сохранения настроек приложения:" + Environment.NewLine + "\t" + errMsg, "Сохранение настроек в config-файле", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            this.DialogResult = true;
        }

        // список выбранных отделов в строку
        private string getIsViewDepIds()
        {
            StringBuilder sb = new StringBuilder();
            foreach (DepartmentViewModel item in _deps.Values)
            {
                if (item.IsViewOnKDS == true)
                {
                    if (sb.Length > 0) sb.Append(",");
                    sb.Append(item.Id.ToString());
                }
            }
            return sb.ToString();
        }

        #region информация о роли из контролов
        private KDSModeEnum getKDSModeFromRadioButtons()
        {
            List<RadioButton> rbList = lbxKDSMode.Children.OfType<RadioButton>().ToList();
            foreach (RadioButton item in rbList)
            {
                if (item.IsChecked ?? false)
                {
                    KDSModeEnum mode;
                    if (Enum.TryParse<KDSModeEnum>(item.Tag.ToString(), out mode)) return mode;
                }
            }
            return KDSModeEnum.None;
        }
        private List<OrderStatusEnum> getStatesFromCheckBoxes(Panel panel)
        {
            List<OrderStatusEnum> retVal = new List<OrderStatusEnum>();

            List<CheckBox> cbList = panel.Children.OfType<CheckBox>().ToList();
            foreach (CheckBox item in cbList)
            {
                if (item.IsChecked ?? false)
                {
                    OrderStatusEnum eState = (OrderStatusEnum)Convert.ToInt32(item.Tag);
                    retVal.Add(eState);
                }
            }
            return (retVal.Count == 0) ? null: retVal;
        }
        private List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> getActionsFromCheckBoxes(Panel panel)
        {
            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> retVal = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>();

            List<CheckBox> cbList = panel.Children.OfType<CheckBox>().ToList();
            foreach (CheckBox item in cbList)
            {
                if ((item.IsChecked ?? false) && (item.Tag != null))
                {
                    KeyValuePair<OrderStatusEnum, OrderStatusEnum> actionPair = KDSModeHelper.GetStatusPairFromIntPair(item.Tag.ToString());
                    if (actionPair.Key != OrderStatusEnum.None) retVal.Add(actionPair);
                }
            }

            return (retVal.Count == 0) ? null : retVal;
        }
        #endregion

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            //this.Close();
            DialogResult = false;
        }


        #region Department list behaviour
        //private void lstDishes_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        //{
        //    e.Handled = true;
        //}

        //private void scrollDishes_PreviewTouchDown(object sender, TouchEventArgs e)
        //{
        //    initDrag(e.GetTouchPoint(scrollDishes).Position);
        //}
        //private void scrollDishes_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        //{
        //    //            if (e.StylusDevice != null) return;

        //    initDrag(e.GetPosition(scrollDishes));
        //}

        //private void scrollDishes_PreviewTouchUp(object sender, TouchEventArgs e)
        //{
        //    endDrag();
        //}
        //private void scrollDishes_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        //{
        //    //            if (e.StylusDevice != null) return;

        //    endDrag();
        //}

        //private void scrollDishes_PreviewTouchMove(object sender, TouchEventArgs e)
        //{
        //    if (lastDragPoint.HasValue)
        //    {
        //        doMove(e.GetTouchPoint((sender as IInputElement)).Position);
        //    }
        //}
        //private void scrollDishes_PreviewMouseMove(object sender, MouseEventArgs e)
        //{
        //    //            if (e.StylusDevice != null) return;

        //    if (lastDragPoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
        //    {
        //        doMove(e.GetPosition(sender as IInputElement));
        //    }
        //}


        //private void initDrag(Point mousePos)
        //{
        //    if (AppLib.IsEventsEnable == false) { AppLib.IsEventsEnable = true; }

        //    //_dateTime = DateTime.Now;
        //    //make sure we still can use the scrollbars
        //    if (mousePos.X <= scrollDishes.ViewportWidth && mousePos.Y < scrollDishes.ViewportHeight)
        //    {
        //        //scrollDishes.Cursor = Cursors.SizeAll;
        //        initDragPoint = mousePos;
        //        lastDragPoint = initDragPoint;
        //        //Mouse.Capture(scrollViewer);
        //    }
        //}

        //private void scrollDishes_ScrollChanged(object sender, ScrollChangedEventArgs e)
        //{
        //    // debug
        //    //return;

        //    Visibility visButtonTop, visButtonBottom;

        //    if (e.VerticalOffset == 0)
        //    {
        //        visButtonTop = Visibility.Hidden;
        //        visButtonBottom = (pnlDishes.ActualHeight == scrollDishes.ActualHeight) ? Visibility.Hidden : Visibility.Visible;
        //    }
        //    else if (e.VerticalOffset == (pnlDishes.ActualHeight - scrollDishes.ActualHeight))
        //    {
        //        visButtonTop = Visibility.Visible;
        //        visButtonBottom = Visibility.Hidden;
        //    }
        //    else
        //    {
        //        visButtonTop = Visibility.Visible;
        //        visButtonBottom = Visibility.Visible;
        //    }

        //    if (btnScrollDown.Visibility != visButtonBottom) btnScrollDown.Visibility = visButtonBottom;
        //    if (btnScrollUp.Visibility != visButtonTop) btnScrollUp.Visibility = visButtonTop;
        //}

        //private void endDrag()
        //{
        //    if ((lastDragPoint == null) || (initDragPoint == null))
        //    {
        //        _isDrag = false;
        //    }
        //    else
        //    {
        //        _isDrag = (Math.Abs(lastDragPoint.Value.X - initDragPoint.Value.X) > 10) || (Math.Abs(lastDragPoint.Value.Y - initDragPoint.Value.Y) > 15);
        //    }
        //}
        //private void doMove(Point posNow)
        //{
        //    if (AppLib.IsEventsEnable == false) { return; }

        //    double dX = posNow.X - lastDragPoint.Value.X;
        //    double dY = posNow.Y - lastDragPoint.Value.Y;

        //    lastDragPoint = posNow;
        //    //Debug.Print(posNow.ToString());
        //    scrollDishes.ScrollToHorizontalOffset(scrollDishes.HorizontalOffset - dX);
        //    scrollDishes.ScrollToVerticalOffset(scrollDishes.VerticalOffset - dY);
        //}
        #endregion

        #region audio subsystem
        private void CbxSelectAudio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string fullFileName = _audioPath + cbxSelectAudio.SelectedItem;
            _wavPlayer.SoundLocation = fullFileName;
            _wavPlayer.Load();
            _wavPlayer.Play();
        }

        // MouseDown event handler
        private void btnBrowseAudioFile_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
        }

        // Click event handler
        private void btnBrowseAudioFile_Click(object sender, RoutedEventArgs e)
        {
            browseAudioFile();
        }

        private string browseAudioFile()
        {
            string retVal="";

            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.InitialDirectory = Environment.CurrentDirectory;
            openFileDialog.Filter = "wav files (*.wav)|*.wav";
            openFileDialog.CheckFileExists = true;

            if (openFileDialog.ShowDialog() ?? false)
            {
                try
                {
                    string file = openFileDialog.FileName;
                    FileInfo fileInfo = new FileInfo(file);
                    string destFile = _audioPath + fileInfo.Name;
                    FileInfo destFileInfo = (File.Exists(destFile)) ? new FileInfo(destFile) : fileInfo.CopyTo(destFile);

                    List<string> filesList = (List<string>)cbxSelectAudio.ItemsSource;
                    if (!filesList.Contains(fileInfo.Name)) filesList.Add(fileInfo.Name);
                    // отобразить в списке и проиграть
                    cbxSelectAudio.SelectedValue = fileInfo.Name;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }
            }

            return retVal;
        }
        #endregion

        // ************** INNER CLASSES  ********************

        #region inner classes
        // внутренний класс для сохранения первоначальных значений настроек
        private class CfgValueKeeper
        {
            private List<CfgValue> _values;

            public CfgValueKeeper()
            {
                _values = new List<CfgValue>();
            }

            public void AddPreValue(string key, bool fromAppProps, Control control)
            {
                _values.Add(new CfgValue(key, fromAppProps, control));
            }

            public void AddPreValueDirectly(string key, string value, Control control = null)
            {
                _values.Add(new CfgValue(key, value, control));
            }

            public void PutNewValueDirectly(string key, string value)
            {
                CfgValue cfgValue = _values.FirstOrDefault(v => v.Key == key);
                if (cfgValue != null) cfgValue.PutNewStringValue(value);
            }

            internal void PutNewValueFromControls()
            {
                _values.ForEach(v => v.PutNewValueFromControl());
            }

            internal void SaveToAppProps()
            {
                _values.ForEach(v => v.SaveToAppProps());
            }

            internal bool IsChanged()
            {
                bool retVal = false;
                foreach (CfgValue item in _values)
                {
                    if (item.IsChanged()) { retVal = true; break; }
                }
                return retVal;
            }

            internal Dictionary<string, string> GetNewValuesDict()
            {
                Dictionary<string, string> retVal = new Dictionary<string, string>();
                foreach (CfgValue item in _values)
                {
                    if (item.IsChanged())
                    {
                        retVal.Add(item.Key, item.NewValueAsString);
                    }
                }

                return retVal;
            }
        }

        private class CfgValue
        {
            private string _key;
            public string Key { get { return _key; } }
            private Control _control;
            private string _typeName;
            private bool _fromAppProps;

            private object _preValue;
            private object _newValue;
            public string NewValueAsString { get { return _newValue.ToString(); } }

            public CfgValue(string key, bool fromAppProps, Control control)
            {
                _key = key; _control = control; _fromAppProps = fromAppProps;

                // из AppLib.GetAppSetting(_key) возвращается СТРОКА 
                _preValue = (fromAppProps) ? AppLib.GetAppGlobalValue(_key) : AppLib.GetAppSetting(_key);
                if (_preValue == null) return;

                _typeName = _preValue.GetType().Name;

                if (control != null) putValueToControl();
            }

            public CfgValue(string key, string value, Control control = null)
            {
                _key = key; _control = control; _fromAppProps = false;
                _preValue = value;

                _typeName = _preValue.GetType().Name;

                if (control != null) putValueToControl();
            }

            public void PutNewStringValue(string value)
            {
                _newValue = value;
            }


            private void putValueToControl()
            {
                _typeName = _preValue.GetType().Name;

                switch (_typeName)
                {
                    case "Int32":
                    case "Int16":
                    case "Int64":
                    case "Decimal":
                        if (_control != null)
                        {
                            if (_control is TextBox) (_control as TextBox).Text = _preValue.ToString();
                            else if (_control is NumericUpDown) (_control as NumericUpDown).Value = Convert.ToDecimal(_preValue);
                        }
                        break;
                    case "Boolean":
                        if ((_control != null) && (_control is CheckBox))
                            (_control as CheckBox).IsChecked = (bool)_preValue;
                        break;
                    case "String":
                        if (_control != null)
                        {
                            if (_control is TextBox) (_control as TextBox).Text = _preValue.ToString();
                            else if (_control is NumericUpDown) (_control as NumericUpDown).Value = (decimal)_preValue.ToString().ToDouble();
                            else if (_control is ComboBox) (_control as ComboBox).SelectedValue = (string)_preValue;
                        }
                        break;
                    case "DateTime":
                        if ((_control != null) && (_control is TextBox))
                            (_control as TextBox).Text = Convert.ToDateTime(_preValue).ToString(CultureInfo.InvariantCulture);
                        break;
                    default:
                        break;
                }
            }


            public void PutNewValueFromControl()
            {
                if (_control != null)
                {
                    switch (_typeName)
                    {
                        case "Int32":
                        case "Int16":
                        case "Int64":
                        case "Decimal":
                            if (_control is TextBox) _newValue = (_control as TextBox).Text.ToInt();
                            else if (_control is NumericUpDown)
                            {
                                decimal decVal = (_control as NumericUpDown).Value;
                                TypeCode tCode;
                                if (Enum.TryParse(_typeName, out tCode)) _newValue = Convert.ChangeType(decVal, tCode);
                                else _newValue = decVal;
                            }
                            break;

                        case "Boolean":
                            if (_control is CheckBox) _newValue = (_control as CheckBox).IsChecked ?? false;
                            break;

                        case "String":
                            if (_control is TextBox) _newValue = (_control as TextBox).Text;
                            else if (_control is NumericUpDown)
                            {
                                NumericUpDown nud = (_control as NumericUpDown);
                                if (nud.ContentStringFormat != null)
                                    _newValue = nud.Value.ToString(nud.ContentStringFormat, CultureInfo.InvariantCulture);
                                else
                                    _newValue = nud.Value.ToString(CultureInfo.InvariantCulture);
                            }
                            else if (_control is ComboBox) _newValue = (string)((_control as ComboBox).SelectedValue);
                            break;

                        case "DateTime":
                            if (_control is TextBox)
                            {
                                _newValue = DateTime.Parse((_control as TextBox).Text, CultureInfo.InvariantCulture);
                            }
                            break;

                        default:
                            break;
                    }
                }
            }


            internal bool IsChanged()
            {
                if (_newValue == null)
                    return false;
                else if (_preValue == null)
                    return true;
                else
                    return !_newValue.Equals(_preValue);
            }

            internal void SaveToAppProps()
            {
                if ((_fromAppProps) && IsChanged()) AppLib.SetAppGlobalValue(_key, _newValue);
            }

        } // class CfgValue
        #endregion

    }  // class ConfigEdit
}
