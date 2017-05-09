using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Data.Entity;
using System.Data.SqlClient;

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


    }  // class

}
