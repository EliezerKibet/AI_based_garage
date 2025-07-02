using System.ComponentModel.DataAnnotations;

namespace GarageManagementSystem.ViewModels
{
    public class ModifyResolutionViewModel
    {
        public int ReportId { get; set; }

        [Display(Name = "Car Details")]
        public string CarDetails { get; set; }

        public List<FaultViewModel> Faults { get; set; } = new List<FaultViewModel>();

        [Display(Name = "Additional Notes")]
        [StringLength(500)]
        public string AdditionalNotes { get; set; }
    }

}
