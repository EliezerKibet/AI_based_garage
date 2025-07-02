namespace GarageManagementSystem.ViewModels
{
    public class CustomerListViewModel
    {
        public IEnumerable<CustomerCarViewModel> Customers { get; set; }
        public IEnumerable<CustomerCarViewModel> Mechanics { get; set; } // Add this

        public IEnumerable<NotificationViewModel> Notifications { get; set; }

        // Added pre-calculated statistics to avoid lambda expressions in the view
        public int TotalCustomers { get; set; }
        public int CustomersWithCars { get; set; }
        public int CustomersWithoutCars { get; set; }
        public int TotalVehicles { get; set; }
    }


}
