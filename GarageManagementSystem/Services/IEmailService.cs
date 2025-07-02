using System.Threading.Tasks;
using GarageManagementSystem.Models;

namespace GarageManagementSystem.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetLinkAsync(string email, string callbackUrl);
        Task SendEmailConfirmationLinkAsync(string email, string callbackUrl);
        Task SendAppointmentApprovalEmailAsync(Users customer, Appointment appointment);
        Task SendAppointmentCancellationEmailAsync(Users customer, Appointment appointment, string cancellationReason);
        Task SendAppointmentRescheduleEmailAsync(Users customer, Appointment appointment, DateTime oldDate, TimeSpan oldTime);
        Task SendEmailAsync(string email, string subject, string htmlMessage);
    }
}