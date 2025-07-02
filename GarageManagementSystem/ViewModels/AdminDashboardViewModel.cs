using System;
using System.Collections.Generic;
using GarageManagementSystem.Models;

namespace GarageManagementSystem.ViewModels
{
    public class AdminDashboardViewModel
    {
        public List<Users> GarageUsers { get; set; } // ✅ Use 'Users' instead of 'ApplicationUser'
        public List<Car> Cars { get; set; }
        public List<MechanicReport> MechanicReports { get; set; }
    }
}
