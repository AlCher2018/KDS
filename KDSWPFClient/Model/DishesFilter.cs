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
    /// <summary>
    /// Класс, в котором хранится фильтр блюд данного КДСа.
    /// Фильтры: по цеху и по статусу блюда/ингредиента.
    /// 
    /// И проводится проверка блюда.
    /// </summary>
    public class DishesFilter
    {
        #region Singleton pattern
        private static DishesFilter instance;
        public static DishesFilter Instance
        {
            get
            {
                if (instance == null) instance = new DishesFilter();
                return instance;
            }
        }
        #endregion

        public MainWindow mainWindow { get; set; }

        // поля для фильтра по цеху
        // ФИЛЬТР ЗАКАЗОВ на данном КДС. Может быть статическим (отделы из config-файла) или динамическим (статус заказов) 
        private ValueChecker<OrderFilterValue> _valueDishChecker;
        // разрешенные статусы
        private List<int> _allowedStatuses;

        // поля для фильтра по статусу

        // instance constructor
        private DishesFilter()
        {
            _valueDishChecker = new ValueChecker<OrderFilterValue>();
            // добавить в фильтр отделы, разрешенные на данном КДС
            _valueDishChecker.Update("depId", checkAllowDepartment);
            // ... и статусы, разрешенные на данном КДС
            _valueDishChecker.Update("dishStates", checkAllowStatus);

            _allowedStatuses = new List<int>();
        }

        // коллекция разрешенных состояний для фильтра
        public void SetAllowedStatuses(KDSUserStatesSet statesSet)
        {
            _allowedStatuses.Clear();
            // для фильтра берем список Ид состояний
            _allowedStatuses = statesSet.States.Select(s => (int)s).ToList();
        }

        public bool IsStatusAllowed(int checkedStateId)
        {
            return _allowedStatuses.Contains(checkedStateId);
        }

        public bool Checked(OrderDishModel dish)
        {
            return _valueDishChecker.Checked(new OrderFilterValue()
            {
                DepartmentId = dish.DepartmentId, StatusId = dish.DishStatusId
            });
        }

        public bool Checked(OrderDishViewModel dish)
        {
            return _valueDishChecker.Checked(new OrderFilterValue()
            {
                DepartmentId = dish.DepartmentId, StatusId = dish.DishStatusId
            });
        }


        // в _dataProvider.Departments, поле IsViewOnKDS = true, если отдел разрешен для показа на этом КДСе
        private bool checkAllowDepartment(OrderFilterValue dish)
        {
            return AppLib.IsDepViewOnKDS(dish.DepartmentId);
        }

        private bool checkAllowStatus(OrderFilterValue dish)
        {
            return _allowedStatuses.Contains(dish.StatusId);
        }


        #region inner classes
        /// <summary>
        /// ValueChecker - объединяет предикаты для проверки объекта T.
        /// Сначала заполняем коллекцию предикатов.
        /// Основной метод - Checked, в который передается проверяемый объект, возвращает true, если ВСЕ предикаты возвращают true.
        /// </summary>
        /// <typeparam name="T">тип объекта, для которого будет проверяться предикат</typeparam>
        private class ValueChecker<T>
        {
            private List<KeyValuePair<string, Predicate<T>>> _predicatesList;

            public ValueChecker()
            {
                _predicatesList = new List<KeyValuePair<string, Predicate<T>>>();
            }

            // все условия должны быть true, т.е. соединяем по AND
            public bool Checked(T checkObject)
            {
                foreach (KeyValuePair<string, Predicate<T>> item in _predicatesList)
                {
                    if (item.Value(checkObject) == false) return false;
                }
                return true;
            }

            public void Add(string key, Predicate<T> predicate)
            {
                _predicatesList.Add(new KeyValuePair<string, Predicate<T>>(key, predicate));
            }

            public void Update(string key, Predicate<T> newPredicate)
            {
                KeyValuePair<string, Predicate<T>> pred = _predicatesList.FirstOrDefault(p => p.Key == key);

                if (pred.Key != null) _predicatesList.Remove(pred);

                _predicatesList.Add(new KeyValuePair<string, Predicate<T>>(key, newPredicate));
            }

            // удалить ВСЕ записи с данным ключем
            public void Remove(string key)
            {
                KeyValuePair<string, Predicate<T>> pred = _predicatesList.FirstOrDefault(p => p.Key == key);

                if (pred.Key != null)
                {
                    _predicatesList.Remove(pred);
                }
            }

            public void Clear()
            {
                _predicatesList.Clear();
            }

        }
        #endregion

    }  // class


    // структура для хранения полей заказа, необходимых для фильтрации блюд
    public struct OrderFilterValue
    {
        public int DepartmentId { get; set; }
        public int StatusId { get; set; }
    }

}
