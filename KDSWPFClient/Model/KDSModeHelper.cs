using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSWPFClient.Model
{
    public static class KDSModeHelper
    {
        // разрешенные состояния (видимые на КДС) и разрешенные действия предопределенных ролей КДС
        // заполняется в статическом конструкторе
        private static Dictionary<KDSModeEnum, KDSModeStates> _definedKDSModes;

        static KDSModeHelper()
        {
            // повар
            KDSModeStates modeCook = new KDSModeStates() { KDSMode = KDSModeEnum.Cook };
            modeCook.AllowedStates.AddRange(new OrderStatusEnum[] 
            {
                OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking, OrderStatusEnum.Cancelled
            });
            modeCook.AllowedActions.AddRange(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>[]
            {
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cooking, OrderStatusEnum.Ready)
            });


            // шеф-повар
            KDSModeStates modeChef = new KDSModeStates() { KDSMode = KDSModeEnum.Chef };
            modeChef.AllowedStates.AddRange(new[]
            {
                OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking, OrderStatusEnum.Ready, OrderStatusEnum.Cancelled
            });
            modeChef.AllowedActions.AddRange(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>[]
            {
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cooking, OrderStatusEnum.Ready),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.Cooking),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cancelled, OrderStatusEnum.CancelConfirmed)
            });

            // официант
            KDSModeStates modeWaiter = new KDSModeStates() { KDSMode = KDSModeEnum.Waiter };
            modeWaiter.AllowedStates.AddRange(new OrderStatusEnum[] { OrderStatusEnum.Ready });
            modeWaiter.AllowedActions.AddRange(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>[]
            {
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.Took)
            });

            // особая роль
            KDSModeStates modeSpecial = new KDSModeStates() { KDSMode = KDSModeEnum.Special };

            _definedKDSModes = new Dictionary<KDSModeEnum, KDSModeStates>()
            {
                { KDSModeEnum.Cook, modeCook },
                { KDSModeEnum.Chef, modeChef },
                { KDSModeEnum.Waiter, modeWaiter },
                { KDSModeEnum.Special, modeSpecial }
            };
        }

        public static Dictionary<KDSModeEnum, KDSModeStates> DefinedKDSModes
        {
            get { return _definedKDSModes; }
        }


        #region режим работы КДС из KDSModeEnum: повар, шеф-повар, официант
        // РАЗРЕШЕННЫЕ СОСТОЯНИЯ
        public static List<OrderStatusEnum> GetKDSModeAllowedStates(KDSModeEnum kdsMode)
        {
            if (_definedKDSModes.ContainsKey(kdsMode))
                return _definedKDSModes[kdsMode].AllowedStates;
            else
                return null;
        }

        // РАЗРЕШЕННЫЕ ПЕРЕХОДЫ МЕЖДУ СОСТОЯНИЯМИ
        public static List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> GetKDSModeAllowedActions(KDSModeEnum kdsMode)
        {
            if (_definedKDSModes.ContainsKey(kdsMode))
                return _definedKDSModes[kdsMode].AllowedActions;
            else
                return null;
        }
        #endregion


        public static void PutCfgKDSModeToAppProps()
        {
            string cfgValue = AppLib.GetAppSetting("KDSMode");
            if (cfgValue.IsNull() == false) return;  // нет такого элемента

            KDSModeEnum mode;
            if (Enum.TryParse<KDSModeEnum>(cfgValue, out mode) == false) return;  // не смогли распарсить

            if (_definedKDSModes.ContainsKey(mode))
                AppLib.SetAppGlobalValue("KDSMode", mode);
            else
                AppLib.SetAppGlobalValue("KDSMode", null);  // не является допустимой ролью

            // особая роль, читаем доп.элементы в config и заполняем четвертый элемент в _definedKDSModes
            if (mode == KDSModeEnum.Special)
            {
                KDSModeStates modeStates = _definedKDSModes[KDSModeEnum.Special];

                modeStates.StringToAllowedStates(AppLib.GetAppSetting("KDSSpecialModeStates"));
                modeStates.StringToAllowedActions(AppLib.GetAppSetting("KDSSpecialModeActions"));
            }
        }  // method


    }  // class

    // класс, объединяющий разрешенные состояния и разрешенные действия (переходы между состояниями) для режима КДС
    public class KDSModeStates
    {
        public KDSModeEnum KDSMode { get; set; }

        private List<OrderStatusEnum> _allowedStates;
        public List<OrderStatusEnum> AllowedStates { get { return _allowedStates; } }

        // переход - это ребра графа, соединяющие два состояния {OrderStatusEnum, OrderSatusEnum}
        // представлен структурой KeyValuePair, в которой Key - состояние ИЗ которого переходим, Value - состояние В которое переходим
        private List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> _allowedActions;
        public List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> AllowedActions { get { return _allowedActions; } }

        public KDSModeStates()
        {
            _allowedStates = new List<OrderStatusEnum>();
            _allowedActions = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>();
        }


        public void StringToAllowedStates(string strStates)
        {
            _allowedStates.Clear();
            if (strStates.IsNull()) return;

            string[] aVal = strStates.Split(',');
            OrderStatusEnum eStatus;
            foreach (string item in aVal)
            {
                if (Enum.TryParse<OrderStatusEnum>(item, out eStatus)) _allowedStates.Add(eStatus);
            }
        }

        public void StringToAllowedActions(string strActions)
        {
            _allowedActions.Clear();
            if (strActions.IsNull()) return;

            string[] aVal = strActions.Split(';');
            foreach (string item in aVal)
            {
                if (item.IsNull() == false)
                {
                    string[] aStr = item.Split(',');
                    if (aStr.Length == 2)
                    {
                        OrderStatusEnum eStatFrom, eStatTo;
                        if (Enum.TryParse(aStr[0], out eStatFrom) && Enum.TryParse(aStr[1], out eStatTo))
                            _allowedActions.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(eStatFrom, eStatTo));
                    }
                }
            }
        }

        public string AllowedStatesToString()
        {
            string retVal = "";
            foreach (OrderStatusEnum item in _allowedStates)
            {
                if (retVal.Length > 0) retVal += ",";
                retVal += item.ToString();
            }
            return retVal;
        }

        public string AllowedActionsToString()
        {
            string retVal = "";
            foreach (KeyValuePair<OrderStatusEnum, OrderStatusEnum> item in _allowedActions)
            {
                if (retVal.Length > 0) retVal += ";";
                retVal += item.Key + "," + item.Value;
            }
            return retVal;
        }


    }  // class


}
