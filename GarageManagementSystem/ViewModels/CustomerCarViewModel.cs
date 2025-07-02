namespace GarageManagementSystem.ViewModels
{
    public class CustomerCarViewModel
    {
        public int Id { get; set; }
        public string CustomerId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string PhoneNumber { get; set; }
        public List<CarViewModel> Cars { get; set; } = new List<CarViewModel>();

        public string? ProfilePicture { get; set; }
    }
}
