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
    
    public partial class OrderDishReturnTime
    {
        public int Id { get; set; }
        public int OrderDishId { get; set; }
        public System.DateTime ReturnDate { get; set; }
        public int StatusFrom { get; set; }
        public int StatusFromTimeSpan { get; set; }
        public int StatusTo { get; set; }
    }
}
