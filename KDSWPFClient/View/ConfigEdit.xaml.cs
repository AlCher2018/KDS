using KDSWPFClient.Lib;
using KDSWPFClient.Model;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

        private CfgValueKeeper _cfgValKeeper;

        public ConfigEdit()
        {
            InitializeComponent();

            this.Loaded += ConfigEdit_Loaded;

            _cfgValKeeper = new CfgValueKeeper();
        }

        private void ConfigEdit_Loaded(object sender, RoutedEventArgs e)
        {
            if (_deps != null)
            {
                // контрол меняет значение поля IsViewOnKDS в _deps  !!!
                listBoxDepartments.ItemsSource = _deps.Values;
            }

            _cfgValKeeper.AddPreValue("depUIDs", true, null);
            _cfgValKeeper.AddPreValue("IsWriteTraceMessages", true, chkIsWriteTraceMessages);
            _cfgValKeeper.AddPreValue("IsLogUserAction", true, chkIsLogUserAction);
            _cfgValKeeper.AddPreValue("AppFontScale", true, null);
            _cfgValKeeper.AddPreValue("OrdersColumnsCount", true, tbxOrdersColumnsCount);

            if (setStatusCordElements() == false)
            {
                rbCook.IsChecked = true;   // по умолчанию
                _cfgValKeeper.AddPreValueDirectly("KDSMode", "null");
            }
            else
            {
                _cfgValKeeper.AddPreValueDirectly("KDSMode", getKDSModeFromRadioButtons());
            }

            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> allowedStates = getStatesCheckBoxes();
            string sStates = StateGraphHelper.StatusCordsToString(allowedStates);
            _cfgValKeeper.AddPreValueDirectly("KDSModeSpecialStates", sStates);
        }

        private string getKDSModeFromRadioButtons()
        {
            string retVal = null;
            List<RadioButton> rbList = lbxKDSMode.Items.OfType<RadioButton>().ToList();
            foreach (RadioButton item in rbList)
            {
                if (item.IsChecked ?? false)
                {
                    KDSModeEnum mode;
                    if (Enum.TryParse<KDSModeEnum>(item.Tag.ToString(), out mode)) retVal = mode.ToString();
                }
            }
            return retVal;
        }

        private bool setStatusCordElements()
        {
            bool retVal = false;
            // предопределенная роль - lbxKDSMode
            string cfgValue = AppLib.GetAppSetting("KDSMode");
            if (!cfgValue.IsNull())
            {
                KDSModeEnum mode;
                if (Enum.TryParse<KDSModeEnum>(cfgValue, out mode))
                {
                    string sMode = ((int)mode).ToString();  // числовое значение режима в символьном виде
                    List<RadioButton> rbList = lbxKDSMode.Items.OfType<RadioButton>().ToList();
                    RadioButton rb = rbList.FirstOrDefault(e => e.Tag.ToString().Equals(sMode));
                    if (rb != null) rb.IsChecked = true;

                    // флажки состояний
                    List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> tList = StateGraphHelper.GetAllowedStatesFromConfigFile();
                    setStatesCheckBoxes(tList);
                    setEnableStateCordsListBox(mode);

                    retVal = true;
                }  // if
            }  // if

            return retVal;
        }  // method


        //  запретить изменять состояния, если роль предопределенная
        private void setEnableStateCordsListBox(KDSModeEnum mode)
        {
            lbxStateCords.IsEnabled = !((mode == KDSModeEnum.Cook) || (mode == KDSModeEnum.Chef) || (mode == KDSModeEnum.Waiter));
        }


        private List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> getStatesCheckBoxes()
        {
            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> retVal = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>();

            List<CheckBox> cbList = lbxStateCords.Items.OfType<CheckBox>().ToList();
            foreach (CheckBox item in cbList)
            {
                if (item.IsChecked??false)
                {
                    retVal.Add(StateGraphHelper.StringToStatusCord(item.Tag.ToString()));
                }
            }

            return retVal;
        }

        private void setStatesCheckBoxes(List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> tList)
        {
            List<CheckBox> cbList = lbxStateCords.Items.OfType<CheckBox>().ToList();
            cbList.ForEach(e => e.IsChecked = false);  // снять все флажки

            if (tList == null) return;

            CheckBox cb;
            string sBuf;
            foreach (KeyValuePair<OrderStatusEnum, OrderStatusEnum> item in tList)
            {
                sBuf = (int)item.Key + "," + (int)item.Value;
                if (cbList != null)
                {
                    cb = cbList.FirstOrDefault(e => e.Tag.Equals(sBuf));
                    if (cb != null) cb.IsChecked = true;
                }
            }
        }

        private string getIsViewDepIds()
        {
            StringBuilder sb = new StringBuilder();
            foreach (DepartmentViewModel item in _deps.Values)
            {
                if (item.IsViewOnKDS == true)
                {
                    if (sb.Length > 0) sb.Append(",");
                    sb.Append(item.UID.ToString());
                }
            }
            return sb.ToString();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            //this.Close();
            DialogResult = false;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // новые значения из списков
            string sBuf = getIsViewDepIds();
            _cfgValKeeper.PutNewValueDirectly("depUIDs", sBuf);

            _cfgValKeeper.PutNewValueDirectly("KDSMode", getKDSModeFromRadioButtons());
            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> allowedStates = getStatesCheckBoxes();
            // сохранить в свойствах приложения
            AppLib.SetAppGlobalValue("StatesAllowedForMove", allowedStates);
            sBuf = StateGraphHelper.StatusCordsToString(allowedStates);
            _cfgValKeeper.PutNewValueDirectly("KDSModeSpecialStates", sBuf);

            // новые значения из контролов
            _cfgValKeeper.PutNewValueFromControls();

            // собрать словарь новых значений
            Dictionary<string, string> appSettings = _cfgValKeeper.GetNewValuesDict();
            // сделаны ли какие-нибудь изменения?
            if (appSettings.Count > 0)
            {
                string errMsg = null;
                if (AppLib.SaveAppSettings(appSettings, out errMsg))
                {
                    _cfgValKeeper.SaveToAppProps();
                    MessageBox.Show("Настройки сохранены успешно!", "Сохранение настроек в config-файле", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Ошибка сохранения настроек приложения:" + Environment.NewLine + "\t" + errMsg, "Сохранение настроек в config-файле", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            this.DialogResult = true;
        }

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

            public void AddPreValueDirectly(string key, string value)
            {
                _values.Add(new CfgValue(key, value));
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
                    if (item.IsChanged()) { retVal = true;  break; }
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

                putValueToField(fromAppProps);
            }

            public CfgValue(string key, string value)
            {
                _key = key; _control = null; _fromAppProps = false;
                _preValue = value;
            }
            
            public void PutNewStringValue(string value)
            {
                _newValue = value;
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
                        case "Double":
                        case "Byte":
                        case "Float":
                        case "Decimal":
                            if (_control is TextBox)
                            {
                                _newValue = (_control as TextBox).Text.ToInt();
                            }
                            break;
                        case "Boolean":
                            if (_control is CheckBox)
                            {
                                _newValue = (_control as CheckBox).IsChecked??false;
                            }
                            break;
                        case "String":
                            if (_control is TextBox)
                            {
                                _newValue = (_control as TextBox).Text;
                            }
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

            private void putValueToField(bool fromAppProps)
            {
                _preValue = (fromAppProps)? AppLib.GetAppGlobalValue(_key): AppLib.GetAppSetting(_key);
                 
                _typeName = _preValue.GetType().Name;
                switch (_typeName)
                {
                    case "Int32":
                    case "Int16":
                    case "Int64":
                    case "Double":
                    case "Byte":
                    case "Float":
                    case "Decimal":
                        if ((_control != null) && (_control is TextBox))
                            (_control as TextBox).Text = _preValue.ToString();
                        break;
                    case "Boolean":
                        if ((_control != null) && (_control is CheckBox))
                            (_control as CheckBox).IsChecked = (bool)_preValue;
                        break;
                    case "String":
                        if ((_control != null) && (_control is TextBox))
                            (_control as TextBox).Text = _preValue.ToString();
                        break;
                    case "DateTime":
                        if ((_control != null) && (_control is TextBox))
                            (_control as TextBox).Text = Convert.ToDateTime(_preValue).ToString(CultureInfo.InvariantCulture);
                        break;
                    default:
                        break;
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
                if (IsChanged())
                {
                    if (_fromAppProps) AppLib.SetAppGlobalValue(_key, _newValue);
                }
            }

        } // class CfgValue
        #endregion

        private void rbKDSMode_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rbChecked = (RadioButton)sender;
            KDSModeEnum kdsMode;
            if (Enum.TryParse<KDSModeEnum>(rbChecked.Tag.ToString(), out kdsMode))
            {
                List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> tList = StateGraphHelper.GetAllowedStatesForKDSMode(kdsMode);
                setStatesCheckBoxes(tList);
                setEnableStateCordsListBox(kdsMode);
            }

        }
    }  // class ConfigEdit

}
