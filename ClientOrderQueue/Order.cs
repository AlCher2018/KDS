//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ClientOrderQueue
{
    using System;
    using System.Collections.Generic;
    
    public partial class Order
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public int QueueStatusId { get; set; }
        public int LanguageTypeId { get; set; }
        public System.DateTime CreateDate { get; set; }
        public string ClientName { get; set; }
    }
}
