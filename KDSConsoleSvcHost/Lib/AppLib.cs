using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Data.Entity;
using System.Data.SqlClient;
using KDSService.AppModel;

namespace KDSService
{
    public static class AppLib
    {
        #region bitwise
        public static void SetBit(ref int bitMask, int bit)
        {
            bitMask |= (1 << bit);
        }
        public static void ClearBit(ref int bitMask, int bit)
        {
            bitMask &= ~(1 << bit);
        }
        public static bool IsSetBit(int bitMask, int bit)
        {
            int val = (1 << bit);
            return (bitMask & val) == val;
        }
        #endregion

        #region OrderStatusEnum funcs
        public static OrderStatusEnum GetStatusEnumFromNullableInt(int? dbIntValue)
        {
            return (OrderStatusEnum)(dbIntValue ?? 0);
            //if (dbIntValue == null)
            //    return OrderStatusEnum.WaitingCook;
            //else
            //{
            //    OrderStatusEnum eVal;
            //    if (Enum.TryParse<OrderStatusEnum>(dbIntValue.ToString(), out eVal))
            //        return eVal;
            //    else
            //        return OrderStatusEnum.WaitingCook;
            //}
        }

        #endregion

        // вернуть признак того, что int из поля БД пустой или нулевой
        public static bool isDBIntZero(int? dbValue)
        {
            return (Convert.ToInt32(dbValue) == 0);
        }

    }  // class

}
