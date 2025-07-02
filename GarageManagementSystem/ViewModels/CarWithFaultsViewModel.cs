using GarageManagementSystem.Views.Mechanic;

namespace GarageManagementSystem.ViewModels
{
    public class CarWithFaultsViewModel
    {
        public int CarId { get; set; }
        public string CarMake { get; set; }
        public string CarModel { get; set; }
        public string CarYear { get; set; }
        public string LicenseNumber { get; set; }
        // Initialize the Faults collection to prevent null reference
        public List<FaultViewModel> Faults { get; set; } = new List<FaultViewModel>();


    }
    public class CarFaultViewModel
    {
        public int CarId { get; set; }
        public string CarMake { get; set; }
        public string CarModel { get; set; }
        public List<FaultModel> PendingFaults { get; set; }
        public List<FaultModel> ResolvedFaults { get; set; }

        public List<FaultViewModel> Faults { get; set; } = new List<FaultViewModel>();
    }
}
