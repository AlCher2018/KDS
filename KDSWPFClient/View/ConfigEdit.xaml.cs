using KDSWPFClient.Lib;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private bool _isChanged = false;
        private Dictionary<int, bool> _isViewKDSInitValues;

        Dictionary<int, DepartmentViewModel> _deps;
        public Dictionary<int, DepartmentViewModel> DepartmentsDict { get { return _deps; } set { _deps = value; } }

        private double _fontScale;
        private int _cntCols;

        public ConfigEdit()
        {
            InitializeComponent();

            this.Loaded += ConfigEdit_Loaded;

            _isViewKDSInitValues = new Dictionary<int, bool>();
        }

        private void ConfigEdit_Loaded(object sender, RoutedEventArgs e)
        {
            if (_deps != null)
            {
                var dList = _deps.Values;
                listBoxDepartments.ItemsSource = dList;
                // сохранить значение поля IsViewOnKDS для всех отделов
                _isViewKDSInitValues.Clear();
                foreach (DepartmentViewModel item in dList) _isViewKDSInitValues.Add(item.Id, item.IsViewOnKDS);
            }

            // получить сохраненные значения из ресурсного словаря приложения
            chkIsWriteTraceMessages.IsChecked = (bool)AppLib.GetAppGlobalValue("IsWriteTraceMessages");
            chkIsLogUserAction.IsChecked = (bool)AppLib.GetAppGlobalValue("IsLogUserAction");
            // масштаб шрифта
            _fontScale = (double)AppLib.GetAppGlobalValue("AppFontScale");
            // размеры элементов панели заказа
            //   кол-во столбцов заказов
            _cntCols = (int)AppLib.GetAppGlobalValue("OrdersColumnsCount");
            tbxOrdersColumnsCount.Text = _cntCols.ToString();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            //this.Close();
            DialogResult = false;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // сделаны ли какие-нибудь изменения?
            _isChanged = false;
            foreach (DepartmentViewModel item in _deps.Values)   // в _deps.Values изменения уже сделаны
            {
                if (item.IsViewOnKDS != _isViewKDSInitValues[item.Id]) { _isChanged = true; break; }
            }

            if ((_isChanged == false) && (chkIsWriteTraceMessages.IsChecked != (bool)AppLib.GetAppGlobalValue("IsWriteTraceMessages")))
                _isChanged = true;
            if ((_isChanged == false) && (chkIsLogUserAction.IsChecked != (bool)AppLib.GetAppGlobalValue("IsLogUserAction")))
                _isChanged = true;

            if ((_isChanged == false) && (_fontScale != (double)AppLib.GetAppGlobalValue("AppFontScale")))
                _isChanged = true;
            if ((_isChanged == false) && (tbxOrdersColumnsCount.Text.IsNull() == false))
            {
                int cntCols = tbxOrdersColumnsCount.Text.ToInt();
                if (cntCols != _cntCols) _isChanged = true;
            }

            // сохранить в config-файле
            if (_isChanged == true)  
            {
                // создать словарь значений
                Dictionary<string, string> appSettings = new Dictionary<string, string>();
                //   получить строку UIDов через запятую для сохранения в config-е
                string[] selectedUID = _deps.Values.Where(d => d.IsViewOnKDS == true).Select(d => d.Id.ToString()).ToArray();
                string appSetValue = string.Join(",", selectedUID);
                appSettings.Add("depUIDs", appSetValue);

                bool bVal = chkIsWriteTraceMessages.IsChecked ?? false;
                AppLib.SetAppGlobalValue("IsWriteTraceMessages", bVal);
                appSettings.Add("IsWriteTraceMessages", bVal.ToString());

                bVal = chkIsLogUserAction.IsChecked ?? false;
                AppLib.SetAppGlobalValue("IsLogUserAction", bVal);
                appSettings.Add("IsLogUserAction", bVal.ToString());

                AppLib.SetAppGlobalValue("AppFontScale", _fontScale);
                appSettings.Add("AppFontScale", _fontScale.ToString());

                int iVal = tbxOrdersColumnsCount.Text.ToInt();
                AppLib.GetAppGlobalValue("OrdersColumnsCount", iVal);
                appSettings.Add("OrdersColumnsCount", iVal.ToString());

                string errMsg = null;
                if (ConfigHelper.SaveAppSettings(appSettings, out errMsg))
                {
                    MessageBox.Show("Настройки сохранены успешно!", "Сохранение настроек в config-файле", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Ошибка сохранения настроек приложения:" + Environment.NewLine + "\t" + errMsg, "Сохранение настроек в config-файле", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }


            this.DialogResult = true;

            // нет необходимости, т.к. сам контрол меняет значение поля IsViewOnKDS в _deps  !!!
            //DepartmentViewModel curDep;
            //foreach (var item in listBoxDepartments.Items)
            //{
            //    if (item is DepartmentViewModel)
            //    {
            //        curDep = (DepartmentViewModel)item;
            //        Debug.Print("id {0}, in _deps {1}, in listControl {2}",curDep.Id, _deps[curDep.Id].IsViewOnKDS, curDep.IsViewOnKDS);
            //        if ((_isChanged == false) && (_deps[curDep.Id].IsViewOnKDS != curDep.IsViewOnKDS)) _isChanged = true;
            //        _deps[curDep.Id].IsViewOnKDS = curDep.IsViewOnKDS;
            //    }
            //}
        }


        // внутренний класс для сохранения первоначальных значений настроек
        // для универсальности значения хранятся в объектной форме
        private class CfgValueKeeper
        {
            public object PrevValue { get; set; }

            public object MyProperty { get; set; }

        }


    }  // class


}
