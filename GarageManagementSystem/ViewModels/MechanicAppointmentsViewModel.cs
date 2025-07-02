using System.Collections.Generic;

namespace GarageManagementSystem.ViewModels
{
    public class MechanicAppointmentsViewModel
    {
        public List<AppointmentViewModel> Appointments { get; set; } = new List<AppointmentViewModel>();
        public Dictionary<string, int> StatusCounts { get; set; } = new Dictionary<string, int>();

        // If GetStatusClass should be a method of this class, add it here:
        public string GetStatusClass(string status)
        {
            return status switch
            {
                "Scheduled" => "bg-blue-100 text-blue-800",
                "In Progress" => "bg-yellow-100 text-yellow-800",
                "Completed" => "bg-green-100 text-green-800",
                "Cancelled" => "bg-red-100 text-red-800",
                _ => "bg-gray-100 text-gray-800"
            };
        }
        public List<AssignedCarViewModel> AssignedCars { get; set; } = new List<AssignedCarViewModel>();
        
    }
    public class CustomerAppointmentsViewModel
    {
        public List<AppointmentViewModel> Appointments { get; set; } = new List<AppointmentViewModel>();
        public Dictionary<string, int> StatusCounts { get; set; } = new Dictionary<string, int>();

        // If GetStatusClass should be a method of this class, add it here:
        public string GetStatusClass(string status)
        {
            return status switch
            {
                "Scheduled" => "bg-blue-100 text-blue-800",
                "In Progress" => "bg-yellow-100 text-yellow-800",
                "Completed" => "bg-green-100 text-green-800",
                "Cancelled" => "bg-red-100 text-red-800",
                _ => "bg-gray-100 text-gray-800"
            };
        }
        public List<AssignedCarViewModel> AssignedCars { get; set; } = new List<AssignedCarViewModel>();

    }
    public class AdminAppointmentsViewModel
    {
        public List<AppointmentViewModel> Appointments { get; set; } = new List<AppointmentViewModel>();
        public Dictionary<string, int> StatusCounts { get; set; } = new Dictionary<string, int>();

        // If GetStatusClass should be a method of this class, add it here:
        public string GetStatusClass(string status)
        {
            return status switch
            {
                "Scheduled" => "bg-blue-100 text-blue-800",
                "In Progress" => "bg-yellow-100 text-yellow-800",
                "Completed" => "bg-green-100 text-green-800",
                "Cancelled" => "bg-red-100 text-red-800",
                _ => "bg-gray-100 text-gray-800"
            };
        }

    }
}
