namespace GarageManagementSystem.ViewModels
{
    public class DashboardUserViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        // If a user can have multiple roles, you might use a collection:
        public IEnumerable<string> Roles { get; set; } = new List<string>();
        // Or, if you have one primary role:
        public string Role { get; set; }
        // Cars assigned to the user
        public IEnumerable<CarViewModel> Cars { get; set; } = new List<CarViewModel>();

        public string? ProfilePicture { get; set; }

        public int TotalReportsCompleted { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
    }
}
