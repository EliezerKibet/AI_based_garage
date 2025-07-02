using GarageManagementSystem.Models;

namespace GarageManagementSystem.ViewModels
{
    public class MechanicViewModel
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public int UserId { get; set; }
        public List<CarWithFaultsViewModel> Cars { get; set; } = new List<CarWithFaultsViewModel>();
    }

  
}
