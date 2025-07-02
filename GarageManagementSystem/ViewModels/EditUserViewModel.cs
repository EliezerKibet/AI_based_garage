namespace GarageManagementSystem.ViewModels
{
    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        public string? ProfilePicture { get; set; }  // Existing profile picture path
        public IFormFile? ProfilePictureFile { get; set; }  // New upload (optional)

        // Password change fields (optional)
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
    }



}
