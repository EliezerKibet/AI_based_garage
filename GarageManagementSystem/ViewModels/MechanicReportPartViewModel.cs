namespace GarageManagementSystem.ViewModels
{
    public class MechanicReportPartViewModel
    {
        public int Id { get; set; }
        public string PartName { get; set; }
        public decimal PartPrice { get; set; }
        public int Quantity { get; set; }

        // NEW: Receipt-specific fields
        public string? OperationCode { get; set; }
        public string? PartNumber { get; set; }
        public string? PartDescription { get; set; }

        // Navigation properties
        public MechanicReportViewModel MechanicReport { get; set; }
        public int MechanicReportId { get; set; }
        public string CarModel { get; set; }

        // NEW: Calculated properties for receipt functionality
        public decimal TotalAmount => PartPrice * Quantity;
        public decimal RetailPrice => PartPrice; // Alias for backward compatibility

        // NEW: Display properties for receipt formatting
        public string FormattedPartPrice => $"${PartPrice:F2}";
        public string FormattedTotalAmount => $"${TotalAmount:F2}";
        public string PartDisplayName => !string.IsNullOrEmpty(PartDescription) ? PartDescription : PartName;

        // NEW: Helper properties for receipt display
        public string FullPartInfo
        {
            get
            {
                var parts = new List<string>();

                if (!string.IsNullOrEmpty(OperationCode))
                    parts.Add($"[{OperationCode}]");

                if (!string.IsNullOrEmpty(PartNumber))
                    parts.Add($"#{PartNumber}");

                parts.Add(PartDisplayName);

                return string.Join(" ", parts);
            }
        }

        public bool HasOperationCode => !string.IsNullOrEmpty(OperationCode);
        public bool HasPartNumber => !string.IsNullOrEmpty(PartNumber);
        public bool HasDescription => !string.IsNullOrEmpty(PartDescription);
    }
}