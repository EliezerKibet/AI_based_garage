using System.ComponentModel.DataAnnotations;

namespace GarageManagementSystem.ViewModels
{
    public class DashboardViewModel
    {
        public AppointmentViewModel Appointment { get; set; }
        public bool ShowMechanics { get; set; }
        public bool ShowCars { get; set; }
        public bool ShowUnassignedCars { get; set; }
        public bool ShowCustomers { get; set; }
        public bool ShowReports { get; set; }
        public bool ShowPartsUsed { get; set; }

        public List<CustomerViewModel> Customers { get; set; } = new();
        public List<NotificationViewModel> Notifications { get; set; } = new();
        public List<DashboardUserViewModel> GarageUsers { get; set; } = new();
        public List<CarViewModel> Cars { get; set; } = new();
        public List<CarViewModel> UnassignedCars { get; set; } = new();
        public List<CustomerCarViewModel> CustomerCars { get; set; } = new();
        public List<MechanicReportViewModel> MechanicReports { get; set; } = new();
        public List<MechanicReportPartViewModel> PartsUsed { get; set; } = new();
        public List<AppointmentViewModel> Appointments { get; set; } = new();
    }

    public class CustomerViewModel
    {
        [Required(ErrorMessage = "First name is required")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(40, MinimumLength = 8, ErrorMessage = "The {0} must be at least {2} and at max {1} characters")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Password does not match")]
        public string ConfirmPassword { get; set; }

        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Address")]
        public string Address { get; set; }

        // Car information (optional)
        [Display(Name = "Make")]
        public string CarMake { get; set; }

        [Display(Name = "Model")]
        public string CarModel { get; set; }

        [Display(Name = "Year")]
        [Range(1900, 2099, ErrorMessage = "Year must be between 1900 and 2099")]
        public int? CarYear { get; set; }

        [Display(Name = "License Plate")]
        public string LicensePlate { get; set; }

        // Computed property for full name
        public string FullName => $"{FirstName} {LastName}";
    }
    public class AppointmentDashboardViewModel
    {
        public int Id { get; set; }
        public AppointmentViewModel Appointment { get; set; }
        public List<NotificationViewModel>? Notifications { get; set; } = new();
        public List<AppointmentViewModel> Appointments { get; set; } = new();

        [Required(ErrorMessage = "Appointment date is required.")]
        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; }

        [Required(ErrorMessage = "Appointment time is required.")]
        public string AppointmentTime { get; set; }

        public string? Notes { get; set; }
    }

    public class AppointmentViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please select a car.")]
        public int? CarId { get; set; }

        [Required(ErrorMessage = "Appointment date is required.")]
        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; }

        [Required(ErrorMessage = "Appointment time is required.")]
        public string AppointmentTime { get; set; }

        public string? Notes { get; set; }
        public string Status { get; set; } = "Pending";

        public List<CarViewModel> Cars { get; set; } = new();
        public List<AppointmentViewModel> Appointments { get; set; } = new();
        public List<NotificationViewModel> Notifications { get; set; } = new();

        public string? MechanicName { get; set; }
        public CarViewModel? Car { get; set; }
        // Customer Details
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public DateTime? CompletionDate { get; set; }
        public string? MechanicPhone { get; set; }
        public string? MechanicEmail { get; set; }
    }
    public class NotificationViewModel
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public DateTime DateCreated { get; set; }
        public bool IsRead { get; set; }
    }
}
