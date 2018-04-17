using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.DataSource
{
    public partial class OrderDishRunTime
    {
        public int Id { get; set; }
        public int OrderDishId { get; set; }
        public DateTime InitDate { get; set; }
        public int WaitingCookTS { get; set; }
        public DateTime CookingStartDate { get; set; }
        public int CookingTS { get; set; }
        public DateTime ReadyDate { get; set; }
        public int WaitingTakeTS { get; set; }
        public DateTime TakeDate { get; set; }
        public int WaitingCommitTS { get; set; }
        public DateTime CommitDate { get; set; }
        public DateTime CancelDate { get; set; }
        public DateTime CancelConfirmedDate { get; set; }
        public int ReadyTS { get; set; }
        public DateTime ReadyConfirmedDate { get; set; }
    }
}
