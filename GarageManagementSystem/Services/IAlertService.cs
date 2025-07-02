using System.Collections.Generic;
using System.Threading.Tasks;

namespace GarageManagementSystem.Services
{
    public interface IAlertService
    {
        Task SendNotificationAsync(string userId, string message);
        Task NotifyAppointmentBookingAsync(int appointmentId);
        Task NotifyCarAddedAsync(int carId);
    }
}

