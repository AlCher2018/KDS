﻿//------------------------------------------------------------------------------
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
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class BoardChefTestEntities : DbContext
    {
        public BoardChefTestEntities()
            : base("name=BoardChefTestEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<Order> Order { get; set; }
        public virtual DbSet<OrderDish> OrderDish { get; set; }
        public virtual DbSet<OrderDishReturnTime> OrderDishReturnTime { get; set; }
        public virtual DbSet<OrderDishRunTime> OrderDishRunTime { get; set; }
        public virtual DbSet<OrderRunTime> OrderRunTime { get; set; }
        public virtual DbSet<OrderStatus> OrderStatus { get; set; }
    }
}
