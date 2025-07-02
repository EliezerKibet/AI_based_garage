using System.ComponentModel.DataAnnotations;

namespace GarageManagementSystem.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Email is required ")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required. ")]
        [StringLength(40, MinimumLength = 8, ErrorMessage = "The {0} must be at {2} and at max {1} character")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        [Compare("ConfirmPassword", ErrorMessage = "Password does not match")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Full name is required. ")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Role is required. ")]
        public string Role { get; set; }

        public string PhoneNumber { get; set; }
    }
}
