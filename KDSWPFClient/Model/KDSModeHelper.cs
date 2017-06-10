using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace KDSWPFClient.Model
{
    public static class KDSModeHelper
    {
        // разрешенные состояния (видимые на КДС) и разрешенные действия предопределенных ролей КДС
        // заполняется в статическом конструкторе
        private static Dictionary<KDSModeEnum, KDSModeStates> _definedKDSModes;

        static KDSModeHelper()
        {
            bool useReadyConfirmedState = (bool)AppLib.GetAppGlobalValue("UseReadyConfirmedState", false);

            // повар
            #region Повар
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
            modeCook.CreateUserStateSets();
            #endregion

            // шеф-повар
            #region Шеф-повар
            KDSModeStates modeChef = new KDSModeStates() { KDSMode = KDSModeEnum.Chef };
            if (useReadyConfirmedState)
            {
                modeChef.AllowedStates.AddRange(new[]
                {
                OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking,
                OrderStatusEnum.Ready, OrderStatusEnum.ReadyConfirmed,
                OrderStatusEnum.Cancelled
                });
                modeChef.AllowedActions.AddRange(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>[]
                {
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cooking, OrderStatusEnum.ReadyConfirmed),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.Cooking),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.ReadyConfirmed),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.ReadyConfirmed, OrderStatusEnum.Cooking),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cancelled, OrderStatusEnum.CancelConfirmed),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.Took),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.ReadyConfirmed, OrderStatusEnum.Took)
                });
            }
            else
            {
                modeChef.AllowedStates.AddRange(new[]
                {
                OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking,
                OrderStatusEnum.Ready, OrderStatusEnum.Cancelled
                });
                modeChef.AllowedActions.AddRange(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>[]
                {
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cooking, OrderStatusEnum.Ready),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.Cooking),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cancelled, OrderStatusEnum.CancelConfirmed),
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.Took)
                });
            }
            modeChef.CreateUserStateSets();
            #endregion

            // официант
            #region Официант
            KDSModeStates modeWaiter = new KDSModeStates() { KDSMode = KDSModeEnum.Waiter };
            if (useReadyConfirmedState)
            {
                modeWaiter.AllowedStates.AddRange(new OrderStatusEnum[]
                {
                OrderStatusEnum.ReadyConfirmed
                });
                modeWaiter.AllowedActions.AddRange(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>[]
                {
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.ReadyConfirmed, OrderStatusEnum.Took)
                });
            }
            else
            {
                modeWaiter.AllowedStates.AddRange(new OrderStatusEnum[]
                {
                OrderStatusEnum.Ready
                });
                modeWaiter.AllowedActions.AddRange(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>[]
                {
                new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.Took)
                });
            }
            modeWaiter.CreateUserStateSets();
            #endregion

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


        // пользовательские наборы состояний для их фильтрации
        public static List<KDSUserStatesSet> CreateUserStatesList(List<OrderStatusEnum> statesList)
        {
            if ((statesList == null) || (statesList.Count < 2)) return null;

            List<KDSUserStatesSet> retVal = new List<Model.KDSUserStatesSet>();

            KDSUserStatesSet curSet;
            int initIdx;  // начальный индекс
            Brush bBrush, fBrush;

            // отсортировать по значению перечисления
            //List<OrderStatusEnum> sortedStates = statesList.OrderBy(s => (int)s).ToList(); 

            // если есть состояния Ожидание и Готовка, то объединить их в один набор "В процессе"
            if ((statesList.Count >= 2) && (statesList[0] == OrderStatusEnum.WaitingCook) && (statesList[1] == OrderStatusEnum.Cooking))
            {
                StateGraphHelper.SetStateButtonBrushes(OrderStatusEnum.Cooking, out bBrush, out fBrush);
                curSet = new KDSUserStatesSet() { Name = "В процессе", BackBrush = bBrush, FontBrush = fBrush };
                curSet.States.AddRange(new[] { OrderStatusEnum.WaitingCook , OrderStatusEnum.Cooking });

                retVal.Add(curSet);
                initIdx = 2;
            }
            else initIdx = 0;

            // добавить состояния по одному
            string tabName;
            for (int i = initIdx; i < statesList.Count; i++)
            {
                OrderStatusEnum curState = statesList[i];
                StateGraphHelper.SetStateButtonBrushes(curState, out bBrush, out fBrush);
                tabName = StateGraphHelper.GetStateTabName(curState);

                curSet = new KDSUserStatesSet() { Name = tabName, BackBrush = bBrush, FontBrush = fBrush };
                curSet.States.Add(curState);

                retVal.Add(curSet);
            }

            // первый элемент - все состояния, если есть более одного состояния в списке
            if (statesList.Count > 1)
            {
                curSet = new KDSUserStatesSet()
                {
                    Name = "Все статусы",
                    BackBrush = new SolidColorBrush(Colors.MediumSeaGreen),
                    FontBrush = new SolidColorBrush(Colors.Navy)
                };
                curSet.States.AddRange(statesList);

                retVal.Insert(0, curSet);
            }
            
            // список из менее 2-х элементов не рассматриваем
            return (retVal.Count < 2) ? null : retVal;
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

        public static List<OrderStatusEnum> GetStatusesListFromSring(string sList)
        {
            if (sList.IsNull()) return null;

            List<OrderStatusEnum> retVal = new List<OrderStatusEnum>();
            string[] aVal = sList.Split(',');
            OrderStatusEnum eStatus;
            foreach (string item in aVal)
            {
                if (Enum.TryParse<OrderStatusEnum>(item, out eStatus)) retVal.Add(eStatus);
            }
            return (retVal.Count == 0) ? null : retVal;
        }

        public static List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> GetActionsListFromSring(string sList)
        {
            if (sList.IsNull()) return null;

            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> retVal = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>();

            string[] aVal = sList.Split(';');
            foreach (string item in aVal)
            {
                if (item.IsNull() == false)
                {
                    string[] aStr = item.Split(',');
                    if (aStr.Length == 2)
                    {
                        OrderStatusEnum eStatFrom, eStatTo;
                        if (Enum.TryParse(aStr[0], out eStatFrom) && Enum.TryParse(aStr[1], out eStatTo))
                            retVal.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(eStatFrom, eStatTo));
                    }
                }
            }

            return (retVal.Count == 0) ? null : retVal;
        }


        // вернуть действие (пара состояний ИЗ - В) из строки с числовыми/строковыми значениями перечисления OrderStatusEnum
        // разделенными запятой
        public static KeyValuePair<OrderStatusEnum, OrderStatusEnum> GetStatusPairFromIntPair(string intPair)
        {
            string[] aStr = intPair.Split(',');
            if (aStr.Length == 2)
            {
                OrderStatusEnum eStatFrom, eStatTo;
                if (Enum.TryParse(aStr[0], out eStatFrom) && Enum.TryParse(aStr[1], out eStatTo))
                    return new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(eStatFrom, eStatTo);
            }
            return new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.None, OrderStatusEnum.None);
        }

        // получение строк состояний и действий (пара состояний ИЗ - В)
        public static string StatesListToString(List<OrderStatusEnum> statusList)
        {
            if (statusList == null) return null;

            string retVal = "";
            foreach (OrderStatusEnum item in statusList)
            {
                if (retVal.Length > 0) retVal += ",";
                retVal += item.ToString();
            }
            return retVal;
        }
        public static string ActionsListToString(List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> actionsList)
        {
            if (actionsList == null) return null;

            string retVal = "";
            foreach (KeyValuePair<OrderStatusEnum, OrderStatusEnum> item in actionsList)
            {
                if (retVal.Length > 0) retVal += ";";
                retVal += item.Key.ToString() + "," + item.Value.ToString();
            }
            return retVal;

        }

        public static void PutCfgKDSModeToAppProps()
        {
            string cfgValue = AppLib.GetAppSetting("KDSMode");
            if (cfgValue.IsNull()) return;  // нет такого элемента

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
                modeStates.CreateUserStateSets();
            }
        }  // method


    }  // class


    // класс, объединяющий разрешенные состояния и разрешенные действия (переходы между состояниями) для режима КДС
    // а также пользовательские наборы состояний (пользовательские фильтры)
    public class KDSModeStates
    {
        public KDSModeEnum KDSMode { get; set; }

        private List<OrderStatusEnum> _allowedStates;
        public List<OrderStatusEnum> AllowedStates { get { return _allowedStates; } }

        // переход - это ребра графа, соединяющие два состояния {OrderStatusEnum, OrderSatusEnum}
        // представлен структурой KeyValuePair, в которой Key - состояние ИЗ которого переходим, Value - состояние В которое переходим
        private List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> _allowedActions;
        public List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> AllowedActions { get { return _allowedActions; } }

        // каждый набор имеет имя, кисти для фона и текста и список состояний
        private List<KDSUserStatesSet> _stateSets;
        public List<KDSUserStatesSet> StateSets { get { return _stateSets; } }

        public KDSModeStates()
        {
            _allowedStates = new List<OrderStatusEnum>();
            _allowedActions = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>();
            _stateSets = new List<KDSUserStatesSet>();
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

        public void CreateUserStateSets()
        {
            _stateSets.Clear();
            var result = KDSModeHelper.CreateUserStatesList(_allowedStates);

            if (result != null) _stateSets.AddRange(result);
        }

        public string AllowedStatesToString()
        {
            return KDSModeHelper.StatesListToString(_allowedStates);
        }

        public string AllowedActionsToString()
        {
            return KDSModeHelper.ActionsListToString(_allowedActions);
        }


    }  // class KDSModeStates


    // пользовательский набор состояний - пользовательский фильтр
    public class KDSUserStatesSet
    {
        public string Name { get; set; }
        public Brush BackBrush { get; set; }
        public Brush FontBrush { get; set; }

        private List<OrderStatusEnum> _states;
        public List<OrderStatusEnum> States
        {
            get { return _states; }
        }

        public KDSUserStatesSet()
        {
            _states = new List<OrderStatusEnum>();
        }

    }  // class KDSUserStatesSet

}
