using Microsoft.AspNetCore.Mvc.Rendering;

namespace GarageManagementSystem.ViewModels
{
    public class CarAssignmentViewModel
    {
        public int CarId { get; set; }
        public int? MechanicId { get; set; }
        public List<SelectListItem> Mechanics { get; set; } = new List<SelectListItem>();
        public string? CarMake { get; set; }
        public string? CarModel { get; set; }
        public int? CarYear { get; set; }
        public DateTime AssignedDate { get; set; }
    }
}
