using System.ComponentModel.DataAnnotations;

namespace GarageManagementSystem.ViewModels
{
    public class FaultViewModel
    {
        public int Id { get; set; }
        public int CarId { get; set; }
        public DateTime DateReportedOn { get; set; } = DateTime.Now;

        public bool ResolutionStatus { get; set; }
        [Required]
        public string Description { get; set; }
        // Car details for easy access
        public string CarMake { get; set; }
        public string CarModel { get; set; }
        public string LicenseNumber { get; set; }
    }
    public class AssignFaultViewModel
    {
        // List of existing faults for a particular car
        public List<FaultViewModel> Faults { get; set; }

        // Model for reporting a new fault
        public FaultViewModel NewFault { get; set; }

        public List<AssignedCarViewModel> AssignedCars { get; set; } = new List<AssignedCarViewModel>();

        public List<NotificationViewModel> Notifications { get; set; }

        public List<OwnerCarViewModel> MyCars { get; set; }
    }
}
