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
    
    public partial class Order
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Order()
        {
            this.OrderDish = new HashSet<OrderDish>();
            this.OrderRunTime = new HashSet<OrderRunTime>();
        }
    
        public int Id { get; set; }
        public int OrderStatusId { get; set; }
        public int DepartmentId { get; set; }
        public string UID { get; set; }
        public int Number { get; set; }
        public string TableNumber { get; set; }
        public System.DateTime CreateDate { get; set; }
        public string RoomNumber { get; set; }
        public Nullable<System.DateTime> StartDate { get; set; }
        public int SpentTime { get; set; }
        public string Waiter { get; set; }
        public int LanguageTypeId { get; set; }
        public int QueueStatusId { get; set; }
    
        public virtual OrderStatus OrderStatus { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<OrderDish> OrderDish { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<OrderRunTime> OrderRunTime { get; set; }
    }
}
