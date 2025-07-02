using Microsoft.AspNetCore.Mvc.Rendering;

namespace GarageManagementSystem.ViewModels
{
    public class FaultAssignmentViewModel
    {
        public int FaultId { get; set; }
        public string Description { get; set; }
        public DateTime DateReportedOn { get; set; } = DateTime.UtcNow;

        // MechanicId to assign the fault to a mechanic
        public int? MechanicId { get; set; }

        public int CarId { get; set; }
        // List of available mechanics to select from
        public List<SelectListItem> Mechanics { get; set; }
        public List<FaultViewModel> Faults { get; set; } = new List<FaultViewModel>();
    }
}
