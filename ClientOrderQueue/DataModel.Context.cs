﻿//------------------------------------------------------------------------------
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
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Data.SqlClient;

    public partial class KDSContext : DbContext
    {
        public KDSContext()
            : base("name=KDSContext")
        {
            // connect timeout, default value = 15 seconds
            // in connection string: Connect Timeout=10 - 10 seconds
            // set connect timeout = 3 sec
            if (this.Database.Connection.ConnectionTimeout != 3)
            {
                string connString = this.Database.Connection.ConnectionString;
                SqlConnectionStringBuilder connStrBuilder = new SqlConnectionStringBuilder(connString);
                connStrBuilder.ConnectTimeout = 3;  // 3 seconds
                // new connection string
                this.Database.Connection.ConnectionString = connStrBuilder.ConnectionString;
            }
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<Order> vwOrderQueue { get; set; }
    }
}
