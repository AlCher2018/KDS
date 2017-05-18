using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KDSWPFClient.View
{
    /// <summary>
    /// Interaction logic for ConfigEdit.xaml
    /// </summary>
    public partial class ConfigEdit : Window
    {
        Dictionary<int, DepartmentViewModel> _deps;
        public Dictionary<int, DepartmentViewModel> DepartmentsDict { set { _deps = value; } }

        private bool _isChanged = false;
        public bool IsChanged { get { return _isChanged; } }

        public ConfigEdit()
        {
            InitializeComponent();

            this.Loaded += ConfigEdit_Loaded;

        }

        private void ConfigEdit_Loaded(object sender, RoutedEventArgs e)
        {
            if (_deps != null)
            {
                var dList = _deps.Values;
                listBoxDepartments.ItemsSource = dList;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
