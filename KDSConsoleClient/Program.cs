using KDSConsoleClient.ServiceReference1;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace KDSConsoleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            //DateTime _dtStop, _dtStart;
            //_dtStart = DateTime.MinValue;
            //System.Threading.Thread.Sleep(2000);
            //_dtStop = DateTime.Now;
            //int sec = Convert.ToInt32((_dtStop - _dtStart).TotalSeconds);


            Type tt = typeof(ConfigurationManager);

            KDSServiceClient _getClient;
            KDSCommandServiceClient _setClient;
            List<OrderModel> _orders;

            _getClient = new KDSServiceClient();
            //_getClient.Open();

            List<OrderStatusModel> statuses = _getClient.GetOrderStatuses();


            _setClient = new KDSCommandServiceClient();

            //IContextChannel contextChannel = (_getClient.InnerChannel as IContextChannel);
            //contextChannel.OperationTimeout = TimeSpan.FromMinutes(10);  // for debug

            List<OrderModel> clientOrders = null;
            try
            {
                //ist<OrderStatusModel> statuses = _getClient.GetOrderStatuses();
                clientOrders = _getClient.GetOrders();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }


        }
    }
}
