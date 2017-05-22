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
    public static class StateGraphHelper
    {
        //
        // ЗАПОЛНИТЬ СЛОВАРЬ РАЗРЕШЕННЫХ ПЕРЕХОДОВ МЕЖДУ СОСТОЯНИЯМИ ИЗ CONFIG-ФАЙЛА
        // ДЛЯ ДАННОГО КДС
        // словарь хранится в свойствах приложения (key = "StatesAllowedForMove")
        // 
        public static void SetStatesAllowedForMoveToAppProps()
        {
            // настройки взять из config-файла
            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> tList = GetAllowedStatesFromConfigFile();
            if (tList != null)
            {
                List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> statesAllowedForMove = null;
                var oProp = AppLib.GetAppGlobalValue("StatesAllowedForMove");
                // создать или очистить словарь
                if (oProp != null)
                {
                    statesAllowedForMove = (List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>)oProp;
                    statesAllowedForMove.Clear();
                }

                if (statesAllowedForMove == null)
                    statesAllowedForMove = tList;
                else
                    statesAllowedForMove.AddRange(tList);

                AppLib.SetAppGlobalValue("StatesAllowedForMove", statesAllowedForMove);
            }
        }

        public static List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> GetAllowedStatesFromConfigFile()
        {
            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> retVal = null;

            bool isSpecial = true;
            string cfgValue = AppLib.GetAppSetting("KDSMode");
            if (cfgValue.IsNull() == false)
            {
                KDSModeEnum mode;
                if (Enum.TryParse<KDSModeEnum>(cfgValue, out mode))
                {
                    retVal = GetAllowedStatesForKDSMode(mode);
                    if (retVal != null) isSpecial = false;
                }
            }

            if (isSpecial)
            {
                cfgValue = AppLib.GetAppSetting("KDSModeSpecialStates");
                if (cfgValue.IsNull() == false)
                {
                    retVal = StringToStatusCords(cfgValue);
                }
            }
            return retVal;
        }


        #region режим работы КДС из KDSModeEnum: повар, шеф-повар, официант
        public static List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> GetAllowedStatesForKDSMode(KDSModeEnum kdsMode)
        {
            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> retVal = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>();

            switch (kdsMode)
            {
                case KDSModeEnum.Special:
                    break;

                case KDSModeEnum.Cook:
                    // повар
                    retVal.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking));
                    retVal.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cooking, OrderStatusEnum.Ready));
                    break;

                case KDSModeEnum.Chef:
                    // шеф-повар
                    retVal.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking));
                    retVal.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cooking, OrderStatusEnum.Ready));
                    retVal.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cancelled, OrderStatusEnum.CancelConfirmed));
                    break;

                case KDSModeEnum.Waiter:
                    // официант
                    retVal.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.Took)); break;

                default:
                    break;
            }

            return (retVal.Count==0) ? null : retVal;
        }
        #endregion

        #region переходы состояний блюда/заказа StatusCord
        // переход - это ребра графа, соединяющие два состояния {OrderStatusEnum, OrderSatusEnum}
        // представлен структурой KeyValuePair, в которой Key - состояние ИЗ которого переходим, Value - состояние В которое переходим
        public static List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> GetStatusCordsFromConfigFile(string cfgKey)
        {
            if (cfgKey.IsNull()) return null;
            string sBuf = AppLib.GetAppSetting(cfgKey);
            if (sBuf.IsNull()) return null;

            return StringToStatusCords(sBuf);
        }

        public static void PutStatusCordsToConfigFile(List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> cords, string key)
        {
            string sCords = StatusCordsToString(cords);
            if (sCords.IsNull() == false)
            {
                string errMsg;
                Dictionary<string, string> appSetDict = new Dictionary<string, string>();
                appSetDict.Add(key, sCords);

                AppLib.SaveAppSettings(appSetDict, out errMsg);
            }
        }

        public static string StatusCordToString(KeyValuePair<OrderStatusEnum, OrderStatusEnum> cord)
        {
            return cord.Key + "," + cord.Value;
        }

        public static KeyValuePair<OrderStatusEnum, OrderStatusEnum> StringToStatusCord(string strCord)
        {
            KeyValuePair<OrderStatusEnum, OrderStatusEnum> cord = new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.None, OrderStatusEnum.None);
            if (strCord.IsNull() == false)
            {
                string[] aStr = strCord.Split(',');
                if (aStr.Length == 2)
                {
                    OrderStatusEnum eStatFrom, eStatTo;
                    if (Enum.TryParse(aStr[0], out eStatFrom) && Enum.TryParse(aStr[1], out eStatTo)) cord = new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(eStatFrom, eStatTo);
                }
            }

            return cord;
        }

        public static string StatusCordsToString(List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> cords)
        {
            string retVal = "";

            foreach (KeyValuePair<OrderStatusEnum, OrderStatusEnum> item in cords)
            {
                if (retVal.Length > 0) retVal += ";";
                retVal += StatusCordToString(item);
            }

            return retVal;
        }

        public static List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> StringToStatusCords(string strCords)
        {
            if (strCords.IsNull()) return null;

            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> retVal = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>();
            string[] astrCords = strCords.Split(';');

            foreach (string item in astrCords)
            {
                retVal.Add(StringToStatusCord(item));
            }

            return (retVal.Count == 0) ? null : retVal;
        }

        #endregion



    }  // class
}
