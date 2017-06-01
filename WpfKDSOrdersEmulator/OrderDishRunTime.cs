//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace WpfKDSOrdersEmulator
{
    using System;
    using System.Collections.Generic;
    
    public partial class OrderDishRunTime
    {
        public int Id { get; set; }
        public int OrderDishId { get; set; }
        public Nullable<System.DateTime> InitDate { get; set; }
        public Nullable<int> WaitingCookTS { get; set; }
        public Nullable<System.DateTime> CookingStartDate { get; set; }
        public Nullable<int> CookingTS { get; set; }
        public Nullable<System.DateTime> ReadyDate { get; set; }
        public Nullable<int> WaitingTakeTS { get; set; }
        public Nullable<System.DateTime> TakeDate { get; set; }
        public Nullable<int> WaitingCommitTS { get; set; }
        public Nullable<System.DateTime> CommitDate { get; set; }
        public Nullable<System.DateTime> CancelDate { get; set; }
        public Nullable<System.DateTime> CancelConfirmedDate { get; set; }
    
        public virtual OrderDish OrderDish { get; set; }
    }
}