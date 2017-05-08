using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService
{
    public class OrderCommand
    {
        public int OrderId { get; set; }
        public int DishId { get; set; }
        public OrderCommandEnum Command { get; set; }
    }  // class

    public enum OrderCommandEnum
    {
        StartCooking, CancelCooking, FinishCooking, Return, TookAway
    }  // enum

}
