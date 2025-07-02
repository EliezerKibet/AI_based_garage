namespace GarageManagementSystem.ViewModels
{
    public class AssignedCarViewModel
    {
        public int AssignmemtId { get; set; }
        public int CarId { get; set; }
        public string CarMake { get; set; }
        public string CarModel { get; set; }
        public string LicenseNumber { get; set; }
        public int Year { get; set; }
        public string Color { get; set; }
        public string FaultDescription { get; set; }

        public int MechanicId { get; set; }
        public string MechanicFullName { get; set; }

        public string ChassisNumber { get; set; }
        public DateTime AssignedDate { get; set; }
        public bool HasReport { get; set; }

        public List<FaultViewModel> Faults { get; set; }
        public int Id { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }

        // Add any other properties that you want to display
    }
    public class OwnerCarViewModel
    {
        public int AssignmemtId { get; set; }
        public int CarId { get; set; }
        public string CarMake { get; set; }
        public string CarModel { get; set; }
        public string LicenseNumber { get; set; }
        public int Year { get; set; }
        public string Color { get; set; }
        public string FaultDescription { get; set; }

        public int MechanicId { get; set; }
        public string MechanicFullName { get; set; }

        public DateTime AssignedDate { get; set; }
        public bool HasReport { get; set; }

        public List<FaultViewModel> Faults { get; set; }
        public int Id { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }

        // Add any other properties that you want to display
    }
}
