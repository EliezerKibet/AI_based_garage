using GarageManagementSystem.Models;
using Serilog.Filters;
using System.ComponentModel.DataAnnotations;

namespace GarageManagementSystem.Controllers
{
    public class AdminSearchViewModel
    {
        public string Query { get; set; }
        public List<Users> MatchingUsers { get; set; }
        public List<Car> MatchingCars { get; set; }
        public List<Fault> MatchingFaults { get; set; }
        public List<MechanicReport> MatchingReports { get; set; }
        public List<Appointment> MatchingAppointments { get; set; }
    }
    public class CustomerSearchViewModel
    {
        public string Query { get; set; }
        public List<Car> MatchingCars { get; set; }
        public List<Fault> MatchingFaults { get; set; }
        public List<MechanicReport> MatchingReports { get; set; }

        public List<Users> MatchingMechanics { get; set; }

        public List<Appointment> MatchingAppointments { get; set; }
    }

    public class MechanicSearchViewModel
    {
        public string Query { get; set; }
        public List<Car> MatchingCars { get; set; }
        public List<Users> MatchingCustomers { get; set; }
        public List<Fault> MatchingFaults { get; set; }
        public List<MechanicReport> MatchingReports { get; set; }
        public List<Appointment> MatchingAppointments { get; set; }
        public string AppointmentTime { get; set; }
    }

}