using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

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
        // РАЗРЕШЕННЫЕ ПЕРЕХОДЫ МЕЖДУ СОСТОЯНИЯМИ
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
                    retVal.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.Cooking));
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


        #region Визуальные элементы состояний и переходов
        // кисти фона и текста
        public static void SetStateButtonBrushes(OrderStatusEnum eState, out SolidColorBrush backgroundBrush, out SolidColorBrush foregroundBrush)
        {
            ResourceDictionary resDict = App.Current.Resources;

            switch (eState)
            {
                case OrderStatusEnum.None:
                    backgroundBrush = new SolidColorBrush(Colors.White);
                    foregroundBrush = new SolidColorBrush(Colors.Black);
                    break;

                case OrderStatusEnum.WaitingCook:
                    backgroundBrush = (SolidColorBrush)resDict["orderHeaderBackBrushWaitingCook"];
                    foregroundBrush = (SolidColorBrush)resDict["orderHeaderForeBrushWaitingCook"];
                    break;

                case OrderStatusEnum.Cooking:
                    backgroundBrush = (SolidColorBrush)resDict["orderHeaderBackBrushCooking"];
                    foregroundBrush = (SolidColorBrush)resDict["orderHeaderForeBrushCooking"];
                    break;

                case OrderStatusEnum.Ready:
                    backgroundBrush = (SolidColorBrush)resDict["orderHeaderBackBrushReady"];
                    foregroundBrush = (SolidColorBrush)resDict["orderHeaderForeBrushReady"];
                    break;

                case OrderStatusEnum.Took:
                    backgroundBrush = (SolidColorBrush)resDict["orderHeaderBackBrushTook"];
                    foregroundBrush = (SolidColorBrush)resDict["orderHeaderForeBrushTook"];
                    break;

                case OrderStatusEnum.Cancelled:
                    backgroundBrush = (SolidColorBrush)resDict["orderHeaderBackBrushCancelled"];
                    foregroundBrush = (SolidColorBrush)resDict["orderHeaderForeBrushCancelled"];
                    break;

                case OrderStatusEnum.Commit:
                    backgroundBrush = new SolidColorBrush(Colors.DarkBlue);
                    foregroundBrush = new SolidColorBrush(Colors.Yellow); 
                    break;

                case OrderStatusEnum.CancelConfirmed:
                    backgroundBrush = new SolidColorBrush(Colors.DarkBlue);
                    foregroundBrush = new SolidColorBrush(Colors.Yellow);
                    break;

                default:
                    backgroundBrush = new SolidColorBrush(Colors.White);
                    foregroundBrush = new SolidColorBrush(Colors.Black);
                    break;
            }
        }  // method

        public static void SetStateButtonTexts(OrderStatusEnum eState, out string btnText1, out string btnText2, bool isOrder, bool isReturnCooking)
        {
            btnText1 = null; btnText2 = null;

            switch (eState)
            {
                case OrderStatusEnum.None:
                    break;

                case OrderStatusEnum.WaitingCook:
                    btnText1 = "ОЖИДАНИЕ";
                    break;

                case OrderStatusEnum.Cooking:
                    btnText1 = (isReturnCooking) ? "ВЕРНУТЬ" : "ГОТОВИТЬ";
                    btnText2 = (isReturnCooking) 
                        ? string.Format("Возврат {0} в очередь приготовления", (isOrder ? "заказа" : "блюда")) 
                        : string.Format("Начать приготовление {0}", (isOrder ? "заказа" : "блюда"));
                    break;

                case OrderStatusEnum.Ready:
                    btnText1 = "ГОТОВО";
                    btnText2 = string.Format("{0} готово и может быть выдано на раздаче", (isOrder ? "Заказ" : "Блюдо"));
                    break;

                case OrderStatusEnum.Took:
                    btnText1 = "ЗАБРАТЬ";
                    btnText2 = string.Format("Забрать {0} и отнести его Клиенту", (isOrder ? "заказ" : "блюдо"));
                    break;

                case OrderStatusEnum.Cancelled:
                    btnText1 = "ОТМЕНИТЬ";
                    btnText2 = "Отменить приготовление " + (isOrder ? "заказа" : "блюда");
                    break;

                case OrderStatusEnum.Commit:
                    btnText1 = "ЗАФИКСИРОВАТЬ";
                    btnText2 = string.Format("Зафиксировать, т.е. запретить изменять статус {0}", (isOrder ? "заказа" : "блюда"));
                    break;

                case OrderStatusEnum.CancelConfirmed:
                    btnText1 = "ПОДТВЕРДИТЬ ОТМЕНУ";
                    btnText2 = string.Format("Подтвердить отмену приготовления {0}", (isOrder ? "заказа" : "блюда"));
                    break;
                default:
                    break;
            }
        }

        public static string GetStateDescription(OrderStatusEnum eState, bool isOrder = false)
        {
            string retVal = null;

            switch (eState)
            {
                case OrderStatusEnum.None:
                    break;
                case OrderStatusEnum.WaitingCook:
                    retVal = "ожидает начала приготовления";
                    break;
                case OrderStatusEnum.Cooking:
                    retVal = "находится в процессе приготовления";
                    break;
                case OrderStatusEnum.Ready:
                    retVal = (isOrder) ? "готов к выдаче" : "готово к выдаче";
                    break;
                case OrderStatusEnum.Took:
                    retVal = (isOrder) ? "выдан клиенту" : "выдано клиенту";
                    break;
                case OrderStatusEnum.Cancelled:
                    retVal = (isOrder) ? "отменен" : "отменено";
                    break;
                case OrderStatusEnum.Commit:
                    retVal = (isOrder) ? "зафиксирован" : "зафиксировано";
                    break;
                case OrderStatusEnum.CancelConfirmed:
                    retVal = "ожидает подтверждения отмены";
                    break;
                default:
                    break;
            }

            return retVal;
        }


        #endregion

    }  // class
}
