﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace KDSWPFClient.ServiceReference1 {
    using System.Runtime.Serialization;
    using System;
    
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="OrderStatusModel", Namespace="http://schemas.datacontract.org/2004/07/KDSService.AppModel")]
    [System.SerializableAttribute()]
    public partial class OrderStatusModel : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int IdField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string NameField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string UIDField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int Id {
            get {
                return this.IdField;
            }
            set {
                if ((this.IdField.Equals(value) != true)) {
                    this.IdField = value;
                    this.RaisePropertyChanged("Id");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Name {
            get {
                return this.NameField;
            }
            set {
                if ((object.ReferenceEquals(this.NameField, value) != true)) {
                    this.NameField = value;
                    this.RaisePropertyChanged("Name");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string UID {
            get {
                return this.UIDField;
            }
            set {
                if ((object.ReferenceEquals(this.UIDField, value) != true)) {
                    this.UIDField = value;
                    this.RaisePropertyChanged("UID");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="DepartmentModel", Namespace="http://schemas.datacontract.org/2004/07/KDSService.AppModel")]
    [System.SerializableAttribute()]
    public partial class DepartmentModel : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private decimal DishQuantityField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int IdField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private bool IsAutoStartField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string NameField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string UIDField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public decimal DishQuantity {
            get {
                return this.DishQuantityField;
            }
            set {
                if ((this.DishQuantityField.Equals(value) != true)) {
                    this.DishQuantityField = value;
                    this.RaisePropertyChanged("DishQuantity");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int Id {
            get {
                return this.IdField;
            }
            set {
                if ((this.IdField.Equals(value) != true)) {
                    this.IdField = value;
                    this.RaisePropertyChanged("Id");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public bool IsAutoStart {
            get {
                return this.IsAutoStartField;
            }
            set {
                if ((this.IsAutoStartField.Equals(value) != true)) {
                    this.IsAutoStartField = value;
                    this.RaisePropertyChanged("IsAutoStart");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Name {
            get {
                return this.NameField;
            }
            set {
                if ((object.ReferenceEquals(this.NameField, value) != true)) {
                    this.NameField = value;
                    this.RaisePropertyChanged("Name");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string UID {
            get {
                return this.UIDField;
            }
            set {
                if ((object.ReferenceEquals(this.UIDField, value) != true)) {
                    this.UIDField = value;
                    this.RaisePropertyChanged("UID");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="OrderModel", Namespace="http://schemas.datacontract.org/2004/07/KDSService.AppModel")]
    [System.SerializableAttribute()]
    public partial class OrderModel : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private System.DateTime CreateDateField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private System.Collections.Generic.Dictionary<int, KDSWPFClient.ServiceReference1.OrderDishModel> DishesField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string HallNameField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int IdField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int NumberField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private KDSWPFClient.ServiceReference1.OrderStatusEnum StatusField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string TableNameField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string UidField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string WaiterField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string WaitingTimerStringField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public System.DateTime CreateDate {
            get {
                return this.CreateDateField;
            }
            set {
                if ((this.CreateDateField.Equals(value) != true)) {
                    this.CreateDateField = value;
                    this.RaisePropertyChanged("CreateDate");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public System.Collections.Generic.Dictionary<int, KDSWPFClient.ServiceReference1.OrderDishModel> Dishes {
            get {
                return this.DishesField;
            }
            set {
                if ((object.ReferenceEquals(this.DishesField, value) != true)) {
                    this.DishesField = value;
                    this.RaisePropertyChanged("Dishes");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string HallName {
            get {
                return this.HallNameField;
            }
            set {
                if ((object.ReferenceEquals(this.HallNameField, value) != true)) {
                    this.HallNameField = value;
                    this.RaisePropertyChanged("HallName");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int Id {
            get {
                return this.IdField;
            }
            set {
                if ((this.IdField.Equals(value) != true)) {
                    this.IdField = value;
                    this.RaisePropertyChanged("Id");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int Number {
            get {
                return this.NumberField;
            }
            set {
                if ((this.NumberField.Equals(value) != true)) {
                    this.NumberField = value;
                    this.RaisePropertyChanged("Number");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public KDSWPFClient.ServiceReference1.OrderStatusEnum Status {
            get {
                return this.StatusField;
            }
            set {
                if ((this.StatusField.Equals(value) != true)) {
                    this.StatusField = value;
                    this.RaisePropertyChanged("Status");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string TableName {
            get {
                return this.TableNameField;
            }
            set {
                if ((object.ReferenceEquals(this.TableNameField, value) != true)) {
                    this.TableNameField = value;
                    this.RaisePropertyChanged("TableName");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Uid {
            get {
                return this.UidField;
            }
            set {
                if ((object.ReferenceEquals(this.UidField, value) != true)) {
                    this.UidField = value;
                    this.RaisePropertyChanged("Uid");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Waiter {
            get {
                return this.WaiterField;
            }
            set {
                if ((object.ReferenceEquals(this.WaiterField, value) != true)) {
                    this.WaiterField = value;
                    this.RaisePropertyChanged("Waiter");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string WaitingTimerString {
            get {
                return this.WaitingTimerStringField;
            }
            set {
                if ((object.ReferenceEquals(this.WaitingTimerStringField, value) != true)) {
                    this.WaitingTimerStringField = value;
                    this.RaisePropertyChanged("WaitingTimerString");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="OrderDishModel", Namespace="http://schemas.datacontract.org/2004/07/KDSService.AppModel")]
    [System.SerializableAttribute()]
    public partial class OrderDishModel : object, System.Runtime.Serialization.IExtensibleDataObject, System.ComponentModel.INotifyPropertyChanged {
        
        [System.NonSerializedAttribute()]
        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string CommentField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private System.DateTime CreateDateField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private KDSWPFClient.ServiceReference1.DepartmentModel DepartmentField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int FilingNumberField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private int IdField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string NameField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string ParentUidField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private decimal QuantityField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string ServiceErrorMessageField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private KDSWPFClient.ServiceReference1.OrderStatusEnum StatusField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string UidField;
        
        [System.Runtime.Serialization.OptionalFieldAttribute()]
        private string WaitingTimerStringField;
        
        [global::System.ComponentModel.BrowsableAttribute(false)]
        public System.Runtime.Serialization.ExtensionDataObject ExtensionData {
            get {
                return this.extensionDataField;
            }
            set {
                this.extensionDataField = value;
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Comment {
            get {
                return this.CommentField;
            }
            set {
                if ((object.ReferenceEquals(this.CommentField, value) != true)) {
                    this.CommentField = value;
                    this.RaisePropertyChanged("Comment");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public System.DateTime CreateDate {
            get {
                return this.CreateDateField;
            }
            set {
                if ((this.CreateDateField.Equals(value) != true)) {
                    this.CreateDateField = value;
                    this.RaisePropertyChanged("CreateDate");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public KDSWPFClient.ServiceReference1.DepartmentModel Department {
            get {
                return this.DepartmentField;
            }
            set {
                if ((object.ReferenceEquals(this.DepartmentField, value) != true)) {
                    this.DepartmentField = value;
                    this.RaisePropertyChanged("Department");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int FilingNumber {
            get {
                return this.FilingNumberField;
            }
            set {
                if ((this.FilingNumberField.Equals(value) != true)) {
                    this.FilingNumberField = value;
                    this.RaisePropertyChanged("FilingNumber");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public int Id {
            get {
                return this.IdField;
            }
            set {
                if ((this.IdField.Equals(value) != true)) {
                    this.IdField = value;
                    this.RaisePropertyChanged("Id");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Name {
            get {
                return this.NameField;
            }
            set {
                if ((object.ReferenceEquals(this.NameField, value) != true)) {
                    this.NameField = value;
                    this.RaisePropertyChanged("Name");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string ParentUid {
            get {
                return this.ParentUidField;
            }
            set {
                if ((object.ReferenceEquals(this.ParentUidField, value) != true)) {
                    this.ParentUidField = value;
                    this.RaisePropertyChanged("ParentUid");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public decimal Quantity {
            get {
                return this.QuantityField;
            }
            set {
                if ((this.QuantityField.Equals(value) != true)) {
                    this.QuantityField = value;
                    this.RaisePropertyChanged("Quantity");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string ServiceErrorMessage {
            get {
                return this.ServiceErrorMessageField;
            }
            set {
                if ((object.ReferenceEquals(this.ServiceErrorMessageField, value) != true)) {
                    this.ServiceErrorMessageField = value;
                    this.RaisePropertyChanged("ServiceErrorMessage");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public KDSWPFClient.ServiceReference1.OrderStatusEnum Status {
            get {
                return this.StatusField;
            }
            set {
                if ((this.StatusField.Equals(value) != true)) {
                    this.StatusField = value;
                    this.RaisePropertyChanged("Status");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Uid {
            get {
                return this.UidField;
            }
            set {
                if ((object.ReferenceEquals(this.UidField, value) != true)) {
                    this.UidField = value;
                    this.RaisePropertyChanged("Uid");
                }
            }
        }
        
        [System.Runtime.Serialization.DataMemberAttribute()]
        public string WaitingTimerString {
            get {
                return this.WaitingTimerStringField;
            }
            set {
                if ((object.ReferenceEquals(this.WaitingTimerStringField, value) != true)) {
                    this.WaitingTimerStringField = value;
                    this.RaisePropertyChanged("WaitingTimerString");
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected void RaisePropertyChanged(string propertyName) {
            System.ComponentModel.PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
            if ((propertyChanged != null)) {
                propertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name="OrderStatusEnum", Namespace="http://schemas.datacontract.org/2004/07/KDSService.AppModel")]
    public enum OrderStatusEnum : int {
        
        [System.Runtime.Serialization.EnumMemberAttribute()]
        None = -1,
        
        [System.Runtime.Serialization.EnumMemberAttribute()]
        WaitingCook = 0,
        
        [System.Runtime.Serialization.EnumMemberAttribute()]
        Cooking = 1,
        
        [System.Runtime.Serialization.EnumMemberAttribute()]
        Ready = 2,
        
        [System.Runtime.Serialization.EnumMemberAttribute()]
        Took = 3,
        
        [System.Runtime.Serialization.EnumMemberAttribute()]
        Cancelled = 4,
        
        [System.Runtime.Serialization.EnumMemberAttribute()]
        Commit = 5,
        
        [System.Runtime.Serialization.EnumMemberAttribute()]
        CancelConfirmed = 6,
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(ConfigurationName="ServiceReference1.IKDSService")]
    public interface IKDSService {
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IKDSService/GetOrderStatuses", ReplyAction="http://tempuri.org/IKDSService/GetOrderStatusesResponse")]
        System.Collections.Generic.List<KDSWPFClient.ServiceReference1.OrderStatusModel> GetOrderStatuses();
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IKDSService/GetOrderStatuses", ReplyAction="http://tempuri.org/IKDSService/GetOrderStatusesResponse")]
        System.Threading.Tasks.Task<System.Collections.Generic.List<KDSWPFClient.ServiceReference1.OrderStatusModel>> GetOrderStatusesAsync();
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IKDSService/GetDepartments", ReplyAction="http://tempuri.org/IKDSService/GetDepartmentsResponse")]
        System.Collections.Generic.List<KDSWPFClient.ServiceReference1.DepartmentModel> GetDepartments();
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IKDSService/GetDepartments", ReplyAction="http://tempuri.org/IKDSService/GetDepartmentsResponse")]
        System.Threading.Tasks.Task<System.Collections.Generic.List<KDSWPFClient.ServiceReference1.DepartmentModel>> GetDepartmentsAsync();
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IKDSService/GetOrders", ReplyAction="http://tempuri.org/IKDSService/GetOrdersResponse")]
        System.Collections.Generic.List<KDSWPFClient.ServiceReference1.OrderModel> GetOrders();
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IKDSService/GetOrders", ReplyAction="http://tempuri.org/IKDSService/GetOrdersResponse")]
        System.Threading.Tasks.Task<System.Collections.Generic.List<KDSWPFClient.ServiceReference1.OrderModel>> GetOrdersAsync();
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IKDSServiceChannel : KDSWPFClient.ServiceReference1.IKDSService, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class KDSServiceClient : System.ServiceModel.ClientBase<KDSWPFClient.ServiceReference1.IKDSService>, KDSWPFClient.ServiceReference1.IKDSService {
        
        public KDSServiceClient() {
        }
        
        public KDSServiceClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public KDSServiceClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public KDSServiceClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public KDSServiceClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        public System.Collections.Generic.List<KDSWPFClient.ServiceReference1.OrderStatusModel> GetOrderStatuses() {
            return base.Channel.GetOrderStatuses();
        }
        
        public System.Threading.Tasks.Task<System.Collections.Generic.List<KDSWPFClient.ServiceReference1.OrderStatusModel>> GetOrderStatusesAsync() {
            return base.Channel.GetOrderStatusesAsync();
        }
        
        public System.Collections.Generic.List<KDSWPFClient.ServiceReference1.DepartmentModel> GetDepartments() {
            return base.Channel.GetDepartments();
        }
        
        public System.Threading.Tasks.Task<System.Collections.Generic.List<KDSWPFClient.ServiceReference1.DepartmentModel>> GetDepartmentsAsync() {
            return base.Channel.GetDepartmentsAsync();
        }
        
        public System.Collections.Generic.List<KDSWPFClient.ServiceReference1.OrderModel> GetOrders() {
            return base.Channel.GetOrders();
        }
        
        public System.Threading.Tasks.Task<System.Collections.Generic.List<KDSWPFClient.ServiceReference1.OrderModel>> GetOrdersAsync() {
            return base.Channel.GetOrdersAsync();
        }
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(ConfigurationName="ServiceReference1.IKDSCommandService")]
    public interface IKDSCommandService {
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IKDSCommandService/ChangeOrderStatus", ReplyAction="http://tempuri.org/IKDSCommandService/ChangeOrderStatusResponse")]
        void ChangeOrderStatus(int orderId, KDSWPFClient.ServiceReference1.OrderStatusEnum orderStatus);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IKDSCommandService/ChangeOrderStatus", ReplyAction="http://tempuri.org/IKDSCommandService/ChangeOrderStatusResponse")]
        System.Threading.Tasks.Task ChangeOrderStatusAsync(int orderId, KDSWPFClient.ServiceReference1.OrderStatusEnum orderStatus);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IKDSCommandService/ChangeOrderDishStatus", ReplyAction="http://tempuri.org/IKDSCommandService/ChangeOrderDishStatusResponse")]
        void ChangeOrderDishStatus(int orderId, int orderDishId, KDSWPFClient.ServiceReference1.OrderStatusEnum orderDishStatus);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IKDSCommandService/ChangeOrderDishStatus", ReplyAction="http://tempuri.org/IKDSCommandService/ChangeOrderDishStatusResponse")]
        System.Threading.Tasks.Task ChangeOrderDishStatusAsync(int orderId, int orderDishId, KDSWPFClient.ServiceReference1.OrderStatusEnum orderDishStatus);
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IKDSCommandServiceChannel : KDSWPFClient.ServiceReference1.IKDSCommandService, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class KDSCommandServiceClient : System.ServiceModel.ClientBase<KDSWPFClient.ServiceReference1.IKDSCommandService>, KDSWPFClient.ServiceReference1.IKDSCommandService {
        
        public KDSCommandServiceClient() {
        }
        
        public KDSCommandServiceClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public KDSCommandServiceClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public KDSCommandServiceClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public KDSCommandServiceClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        public void ChangeOrderStatus(int orderId, KDSWPFClient.ServiceReference1.OrderStatusEnum orderStatus) {
            base.Channel.ChangeOrderStatus(orderId, orderStatus);
        }
        
        public System.Threading.Tasks.Task ChangeOrderStatusAsync(int orderId, KDSWPFClient.ServiceReference1.OrderStatusEnum orderStatus) {
            return base.Channel.ChangeOrderStatusAsync(orderId, orderStatus);
        }
        
        public void ChangeOrderDishStatus(int orderId, int orderDishId, KDSWPFClient.ServiceReference1.OrderStatusEnum orderDishStatus) {
            base.Channel.ChangeOrderDishStatus(orderId, orderDishId, orderDishStatus);
        }
        
        public System.Threading.Tasks.Task ChangeOrderDishStatusAsync(int orderId, int orderDishId, KDSWPFClient.ServiceReference1.OrderStatusEnum orderDishStatus) {
            return base.Channel.ChangeOrderDishStatusAsync(orderId, orderDishId, orderDishStatus);
        }
    }
}
