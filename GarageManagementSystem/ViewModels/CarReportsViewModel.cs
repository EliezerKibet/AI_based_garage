namespace GarageManagementSystem.ViewModels
{
    public class CarReportsViewModel
    {
        public CarViewModel Car { get; set; }
        public List<MechanicReportViewModel> Reports { get; set; }
        public List<NotificationViewModel> Notifications { get; set; }
    }
}

