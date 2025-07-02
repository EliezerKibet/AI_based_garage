using GarageManagementSystem.Models;
using GarageManagementSystem.Models.ViewModels;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace GarageManagementSystem.ViewModels
{
    public class MechanicDashboardViewModel
    {
        public int Id { get; set; }
        public int CarId { get; set; }
        public int SelectedCarId { get; set; }
        public int MechanicId { get; set; }

        [Required]
        public string ServiceDetails { get; set; }
        public string AdditionalNotes { get; set; }

        public List<AssignedCarViewModel> AssignedCars { get; set; } = new List<AssignedCarViewModel>();
        public List<MechanicReportViewModel> Reports { get; set; } = new List<MechanicReportViewModel>();

        public List<EditReportViewModel> editReportViewModels { get; set; } = new List<EditReportViewModel>();

        // ✅ Reference PartViewModel instead of redefining it
        public List<PartViewModel> Parts { get; set; } = new List<PartViewModel>();
        public List<NotificationViewModel> Notifications { get; set; }

        public List<FaultViewModel> Faults { get; set; } = new List<FaultViewModel>();

        public List<AssignFaultViewModel> AssignFaultViews { get; set; }

        public List<CarViewModel> Cars { get; set; } = new();
        public List<AppointmentViewModel> Appointments { get; set; } = new List<AppointmentViewModel>();

        public DataTable MyTable { get; set; } = new DataTable();
    }

}
