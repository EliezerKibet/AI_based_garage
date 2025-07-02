using System.Data;

namespace GarageManagementSystem.ViewModels
{
    public class CustomerDashboardViewModel
    {
        public List<CarViewModel> Cars { get; set; } = new List<CarViewModel>();
        public List<AppointmentViewModel> Appointments { get; set; }

        public int Id { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public string LicenseNumber { get; set; }
        public int Year { get; set; }
        public string Color { get; set; }
        public string ChassisNumber { get; set; }
        public bool HasReport { get; set; }
        public string FuelType { get; set; }
        public List<FaultViewModel> Faults { get; set; }  // Add this

        public CarViewModel NewCar { get; set; } = new CarViewModel();
        public int CarCount { get; set; } // Add this line
        public int ReportCount { get; set; }
        public int UpcomingAppointmentsCount { get; set; } // ✅
        public DataTable MyTable { get; set; } = new DataTable();

    }

}
