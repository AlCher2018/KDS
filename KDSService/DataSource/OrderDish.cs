//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace KDSService.DataSource
{
    using System;
    using System.Collections.Generic;
    
    public partial class OrderDish
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public OrderDish()
        {
            this.OrderDishReturnTime = new HashSet<OrderDishReturnTime>();
            this.OrderDishRunTime = new HashSet<OrderDishRunTime>();
        }
    
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Nullable<int> DishStatusId { get; set; }
        public int DepartmentId { get; set; }
        public string UID { get; set; }
        public string DishName { get; set; }
        public int FilingNumber { get; set; }
        public decimal Quantity { get; set; }
        public string ParentUid { get; set; }
        public int EstimatedTime { get; set; }
        public string Comment { get; set; }
        public System.DateTime CreateDate { get; set; }
        public Nullable<System.DateTime> StartDate { get; set; }
        public string UID1C { get; set; }
        public int DelayedStartTime { get; set; }
    
        public virtual Order Order { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<OrderDishReturnTime> OrderDishReturnTime { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<OrderDishRunTime> OrderDishRunTime { get; set; }
    }
}
