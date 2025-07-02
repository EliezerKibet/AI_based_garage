using GarageManagementSystem.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace GarageManagementSystem.ViewModels
{
    public class CarListViewModel
    {
        public IEnumerable<CarViewModel> Cars { get; set; }
        public IEnumerable<FaultViewModel> CarFaults { get; set; }
        public DataTable MyTable { get; set; }

        // Statistics properties for cars and faults
        public int TotalFaults => CarFaults?.Count() ?? 0;
        public int PendingFaults => CarFaults?.Count(f => !f.ResolutionStatus) ?? 0;
        public int ResolvedFaults => CarFaults?.Count(f => f.ResolutionStatus) ?? 0;

        public int PendingFaultsPercentage => TotalFaults > 0 ? (PendingFaults * 100) / TotalFaults : 0;
        public int ResolvedFaultsPercentage => TotalFaults > 0 ? (ResolvedFaults * 100) / TotalFaults : 0;

        public int CarsWithPendingFaults => Cars?.Count(c =>
            CarFaults.Any(f => f.CarId == c.Id && !f.ResolutionStatus)) ?? 0;

        public int CarsWithoutFaults => Cars?.Count(c =>
            !CarFaults.Any(f => f.CarId == c.Id)) ?? 0;
    }


    public class CarViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "The Make field is required.")]
        public string Make { get; set; }

        [Required(ErrorMessage = "The Model field is required.")]
        public string Model { get; set; }

        [Required(ErrorMessage = "The Year field is required.")]
        public int Year { get; set; }

        public string? Color { get; set; }

        public int OwnerId { get; set; }
        public int UserId { get; set; }

        // Do not bind or validate the navigation property from the form.
        [ValidateNever]
        public Users User { get; set; }

        // Optional fields—make them nullable if you don’t want them required.


        [RegularExpression(@"^[A-Za-z0-9]*$", ErrorMessage = "Chassis number can only contain letters and numbers")]
        [StringLength(17, ErrorMessage = "Chassis number cannot exceed 17 characters")]
        public string? ChassisNumber { get; set; }

        public string? FuelType { get; set; }
        public string? LicenseNumber { get; set; }

        public bool HasReport { get; set; }
        public bool IsAssigned { get; set; }

        // Mark collection properties with [ValidateNever] so they are not expected in the form post.
        [ValidateNever]
        public List<FaultViewModel> Faults { get; set; } = new List<FaultViewModel>();

        [ValidateNever]
        public List<AppointmentViewModel> Appointments { get; set; } = new List<AppointmentViewModel>();

        public int? AssignedMechanicId { get; set; }
        public string MechanicName { get; set; } = "Not Assigned";

        public List<MechanicReportViewModel> Reports { get; set; } = new List<MechanicReportViewModel>();

        public string? OwnerName { get; set; }
        public string? AssignedMechanicName { get; set; }

    }
    public class MechanicListViewModel
    {
        public List<MechanicCarViewModel> Mechanics { get; set; }
        public List<NotificationViewModel> Notifications { get; set; }
    }

    public class MechanicCarViewModel
    {
        public string MechanicId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string PhoneNumber { get; set; }
        public int FaultsResolved { get; set; } // Add this
        public int ReportsMade { get; set; }    // Add this
        public List<CarViewModel> Cars { get; set; }
    }
}
