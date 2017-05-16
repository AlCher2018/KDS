using KDSWinFormClient.ServiceReference1;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KDSWinFormClient
{
    public partial class Form1 : Form
    {
        private Timer _timer;

        KDSServiceClient _getClient;
        KDSCommandServiceClient _setClient;
        List<OrderModel> _orders;

        OrderModel _testOrder;

        public Form1()
        {
            InitializeComponent();

            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += _timer_Tick;
            _timer.Start();

            _getClient = new KDSServiceClient();
            _setClient = new KDSCommandServiceClient();

            IContextChannel contextChannel = (_getClient.InnerChannel as IContextChannel);
            contextChannel.OperationTimeout = TimeSpan.FromMinutes(10);  // for debug

            //var v2 = _getClient.Endpoint.Binding.SendTimeout;

            _orders = new List<OrderModel>();

        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            _timer.Stop();

            List<OrderModel> clientOrders = null;
            try
            {
                clientOrders = _getClient.GetOrders();
            }
            catch (Exception)
            {
                lblOrderStatus.Text = "сервис не запущен";
                _timer.Start(); return;
            }

            updateAppOrdersList(clientOrders);
            _testOrder = _orders.Find(o => o.Id == 2);

            if (_testOrder == null)
            {
                lblOrderNumber.Text = "--";
                lblOrderStatus.Text = "--";
                lblOrderTimer.Text = "--";
                dataGridView1.Rows.Clear();
            }
            else
            {
                lblOrderNumber.Text = _testOrder.Number.ToString();
                lblOrderStatus.Text = _testOrder.Status.ToString();
                lblOrderTimer.Text = _testOrder.WaitingTimerString;

                // обновление блюд
                Dictionary<int, OrderDishModel> dishes = _testOrder.Dishes;
                // словарь из строк DGV
                Dictionary<int, DataGridViewRow> rowsDict = new Dictionary<int, DataGridViewRow>();
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    int id = (int)row.Cells[0].Value;
                    rowsDict.Add(id, row);
                }
                // delete rows
                if (dataGridView1.Rows.Count > 0)
                {
                    var delIds = rowsDict.Keys.Except(dishes.Keys);
                    foreach (var item in delIds) dataGridView1.Rows.Remove(rowsDict[item]);
                }
                // update exists dishes
                DataGridViewRow curRow;  // строка для обновления
                foreach (OrderDishModel dishModel in dishes.Values)
                {
                    if (rowsDict.ContainsKey(dishModel.Id))
                    {
                        curRow = rowsDict[dishModel.Id];
                        if (curRow.Cells[1].Value.Equals(dishModel.Name) == false) curRow.Cells[1].Value = dishModel.Name;
                        if (curRow.Cells[2].Value.Equals(dishModel.FilingNumber) == false) curRow.Cells[2].Value = dishModel.FilingNumber;
                        if (curRow.Cells[3].Value.Equals(dishModel.Quantity) == false) curRow.Cells[3].Value = dishModel.Quantity;
                        if (curRow.Cells[4].Value.Equals(dishModel.Status) == false) curRow.Cells[4].Value = dishModel.Status;
                        curRow.Cells[5].Value = dishModel.WaitingTimerString;
                    }
                    else
                    {
                        int i = dataGridView1.Rows.Add();
                        curRow = dataGridView1.Rows[i];
                        curRow.Cells[0].Value = dishModel.Id;
                        curRow.Cells[1].Value = dishModel.Name;
                        curRow.Cells[2].Value = dishModel.FilingNumber;
                        curRow.Cells[3].Value = dishModel.Quantity;
                        curRow.Cells[4].Value = dishModel.Status;
                        curRow.Cells[5].Value = dishModel.WaitingTimerString;
                    }
                }
            }

            _timer.Start();
        }



        private void updateAppOrdersList(List<OrderModel> clientOrders)
        {
            int[] delIds = _orders.Select(o => o.Id).Except(clientOrders.Select(co => co.Id)).ToArray();
            foreach (int item in delIds)
            {
                _orders.Remove(_orders.First(o => o.Id == item));
            }

            OrderModel curOrder;
            foreach (OrderModel cltOrder in clientOrders)
            {
                curOrder = _orders.FirstOrDefault(o => o.Id == cltOrder.Id);
                if (curOrder == null)
                {
                    curOrder = new OrderModel();
                    curOrder.Dishes = new Dictionary<int, OrderDishModel>();
                    _orders.Add(curOrder);
                }
                curOrder.Id = cltOrder.Id;
                curOrder.Number = cltOrder.Number;
                curOrder.HallName = cltOrder.HallName;
                curOrder.TableName = cltOrder.TableName;
                curOrder.CreateDate= cltOrder.CreateDate;
                curOrder.ExtensionData = cltOrder.ExtensionData;
                curOrder.Waiter = cltOrder.Waiter;
                curOrder.Status = cltOrder.Status;
                updateOrderDishesList(curOrder.Dishes, cltOrder.Dishes);
                curOrder.WaitingTimerString = cltOrder.WaitingTimerString;
            }

        }

        private void updateOrderDishesList(Dictionary<int, OrderDishModel> appDishDict, Dictionary<int, OrderDishModel> cltDishDict)
        {
            // удалить исключенные блюда
            int[] delIds = appDishDict.Keys.Except(cltDishDict.Keys).ToArray();
            foreach (int delId in delIds) appDishDict.Remove(delId);

            OrderDishModel oDish;
            foreach (OrderDishModel curDish in cltDishDict.Values)
            {
                if (appDishDict.ContainsKey(curDish.Id))
                    oDish = appDishDict[curDish.Id];
                else
                {
                    oDish = new OrderDishModel() { Id = curDish.Id };
                    appDishDict.Add(curDish.Id, oDish);
                }

                oDish.Comment = curDish.Comment;
                oDish.CreateDate = curDish.CreateDate;
                oDish.Department = curDish.Department;
                oDish.FilingNumber = curDish.FilingNumber;
                oDish.Name = curDish.Name;
                oDish.ParentUid = curDish.ParentUid;
                oDish.Quantity = curDish.Quantity;
                oDish.ServiceErrorMessage = curDish.ServiceErrorMessage;
                oDish.Status = curDish.Status;
                oDish.Uid = curDish.Uid;
                oDish.WaitingTimerString = curDish.WaitingTimerString;
            }
        }  // method

        private void btnStartCooking_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                DataGridViewRow row = dataGridView1.SelectedRows[0];

                OrderStatusEnum dishStatus = getStatusEnumByString(row.Cells[4].Value.ToString());
                if (dishStatus == OrderStatusEnum.WaitingCook)
                {
                    int dishId = (int)row.Cells[0].Value;
                    _setClient.ChangeOrderDishStatus(_testOrder.Id, dishId, OrderStatusEnum.Cooking);
                }
            }
        }  // method

        private OrderStatusEnum getStatusEnumByString(string statusString)
        {
            OrderStatusEnum eVal;
            if ((Enum.TryParse(statusString, out eVal) == true) 
                && (Enum.IsDefined(typeof(OrderStatusEnum), eVal) == true))
                return eVal;
            else
                return OrderStatusEnum.None;
        }

        private void btnFinishCooking_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                DataGridViewRow row = dataGridView1.SelectedRows[0];

                OrderStatusEnum dishStatus = getStatusEnumByString(row.Cells[4].Value.ToString());
                if (dishStatus == OrderStatusEnum.Cooking)
                {
                    int dishId = (int)row.Cells[0].Value;
                    _setClient.ChangeOrderDishStatus(_testOrder.Id, dishId, OrderStatusEnum.Ready);
                }
            }
        }

        private void btnTake_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                DataGridViewRow row = dataGridView1.SelectedRows[0];

                OrderStatusEnum dishStatus = getStatusEnumByString(row.Cells[4].Value.ToString());
                if (dishStatus == OrderStatusEnum.Ready)
                {
                    int dishId = (int)row.Cells[0].Value;
                    _setClient.ChangeOrderDishStatus(_testOrder.Id, dishId, OrderStatusEnum.Took);
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                DataGridViewRow row = dataGridView1.SelectedRows[0];

                OrderStatusEnum dishStatus = getStatusEnumByString(row.Cells[4].Value.ToString());
                int dishId = (int)row.Cells[0].Value;
                _setClient.ChangeOrderDishStatus(_testOrder.Id, dishId, OrderStatusEnum.Cancelled);
            }
        }

        private void btnCancelConfirm_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                DataGridViewRow row = dataGridView1.SelectedRows[0];

                OrderStatusEnum dishStatus = getStatusEnumByString(row.Cells[4].Value.ToString());
                if (dishStatus == OrderStatusEnum.Cancelled)
                {
                    int dishId = (int)row.Cells[0].Value;
                    _setClient.ChangeOrderDishStatus(_testOrder.Id, dishId, OrderStatusEnum.CancelConfirmed);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OrderStatusEnum status = getStatusEnumByString(_testOrder.Status.ToString());
            if ((int)status <= 1)
            {
                _setClient.ChangeOrderStatus(_testOrder.Id, OrderStatusEnum.Cooking);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            int iStatus = (int)getStatusEnumByString(_testOrder.Status.ToString());
            if ((iStatus == 1) || (iStatus == 2))
            {
                _setClient.ChangeOrderStatus(_testOrder.Id, OrderStatusEnum.Ready);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            int iStatus = (int)getStatusEnumByString(_testOrder.Status.ToString());
            if (iStatus <= 2)
            {
                _setClient.ChangeOrderStatus(_testOrder.Id, OrderStatusEnum.Took);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            int iStatus = (int)getStatusEnumByString(_testOrder.Status.ToString());
            if (iStatus <= 3)
            {
                _setClient.ChangeOrderStatus(_testOrder.Id, OrderStatusEnum.Cancelled);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            int iStatus = (int)getStatusEnumByString(_testOrder.Status.ToString());
            if (iStatus == 4)
            {
                _setClient.ChangeOrderStatus(_testOrder.Id, OrderStatusEnum.CancelConfirmed);
            }
        }
    }  // class
}
