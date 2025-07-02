// Add these ViewModels to your ViewModels folder or namespace

using System.ComponentModel.DataAnnotations;

namespace GarageManagementSystem.ViewModels
{
    public class PartsManagementViewModel
    {
        public List<OperationCodeViewModel> OperationCodes { get; set; } = new List<OperationCodeViewModel>();
        public List<ServicePartViewModel> ServiceParts { get; set; } = new List<ServicePartViewModel>();
        public List<NotificationViewModel> Notifications { get; set; } = new List<NotificationViewModel>();

        // Summary statistics
        public int TotalOperationCodes => OperationCodes.Count;
        public int ActiveOperationCodes => OperationCodes.Count(oc => oc.IsActive);
        public int TotalServiceParts => ServiceParts.Count;
        public int AvailableServiceParts => ServiceParts.Count(sp => sp.IsAvailable);
        public decimal TotalPartsValue => ServiceParts.Where(sp => sp.IsAvailable).Sum(sp => sp.Price);
    }

    public class OperationCodeViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Operation code is required")]
        [StringLength(10, ErrorMessage = "Operation code cannot exceed 10 characters")]
        [Display(Name = "Operation Code")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Operation Name")]
        public string Name { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Description")]
        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; }

        [Display(Name = "Associated Parts")]
        public int AssociatedPartsCount { get; set; }

        public List<ServicePartViewModel> AssociatedParts { get; set; } = new List<ServicePartViewModel>();
    }

    public class ServicePartViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Part number is required")]
        [StringLength(50, ErrorMessage = "Part number cannot exceed 50 characters")]
        [Display(Name = "Part Number")]
        public string PartNumber { get; set; }

        [Required(ErrorMessage = "Part name is required")]
        [StringLength(200, ErrorMessage = "Part name cannot exceed 200 characters")]
        [Display(Name = "Part Name")]
        public string PartName { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Part Description")]
        [DataType(DataType.MultilineText)]
        public string? PartDescription { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, 99999.99, ErrorMessage = "Price must be between RM 0.01 and RM 99,999.99")]
        [Display(Name = "Price (RM)")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "Is Available")]
        public bool IsAvailable { get; set; } = true;

        public DateTime CreatedDate { get; set; }
        public DateTime? LastUpdated { get; set; }

        [Display(Name = "Associated Operation Codes")]
        public List<string> AssociatedOperationCodes { get; set; } = new List<string>();

        // Usage statistics
        [Display(Name = "Total Quantity Used")]
        public int TotalQuantityUsed { get; set; }

        [Display(Name = "Usage Count")]
        public int UsageCount { get; set; }

        [Display(Name = "Last Used Date")]
        public DateTime? LastUsedDate { get; set; }

        // For operation code relationships
        public bool IsDefault { get; set; }

        // Computed properties
        public string FormattedPrice => $"RM {Price:F2}";
        public string AvailabilityStatus => IsAvailable ? "Available" : "Unavailable";
        public string LastUsedDisplay => LastUsedDate?.ToString("MMM dd, yyyy") ?? "Never used";
    }

    public class OperationCodePartAssignmentViewModel
    {
        [Required]
        public int OperationCodeId { get; set; }

        [Required]
        public int ServicePartId { get; set; }

        [Display(Name = "Set as Default Part")]
        public bool IsDefault { get; set; }

        public string OperationCodeName { get; set; }
        public string ServicePartName { get; set; }
    }

    // For bulk operations
    public class BulkOperationViewModel
    {
        public List<int> SelectedIds { get; set; } = new List<int>();
        public string Operation { get; set; } // "activate", "deactivate", "delete"
    }

    // For import/export operations
    public class PartsImportViewModel
    {
        [Required]
        [Display(Name = "CSV File")]
        public IFormFile CsvFile { get; set; }

        [Display(Name = "Update Existing Parts")]
        public bool UpdateExisting { get; set; }

        [Display(Name = "Skip Invalid Rows")]
        public bool SkipInvalid { get; set; } = true;
    }

    public class PartsExportViewModel
    {
        [Display(Name = "Include Operation Codes")]
        public bool IncludeOperationCodes { get; set; } = true;

        [Display(Name = "Include Service Parts")]
        public bool IncludeServiceParts { get; set; } = true;

        [Display(Name = "Include Usage Statistics")]
        public bool IncludeUsageStats { get; set; } = true;

        [Display(Name = "Export Format")]
        public string ExportFormat { get; set; } = "CSV"; // CSV, Excel
    }

    // For search and filtering
    public class PartsSearchViewModel
    {
        [Display(Name = "Search Term")]
        public string SearchTerm { get; set; }

        [Display(Name = "Filter by Operation Code")]
        public string OperationCodeFilter { get; set; }

        [Display(Name = "Show Active Only")]
        public bool ActiveOnly { get; set; } = true;

        [Display(Name = "Show Available Only")]
        public bool AvailableOnly { get; set; } = true;

        [Display(Name = "Price Range From")]
        [DataType(DataType.Currency)]
        public decimal? PriceFrom { get; set; }

        [Display(Name = "Price Range To")]
        [DataType(DataType.Currency)]
        public decimal? PriceTo { get; set; }

        [Display(Name = "Sort By")]
        public string SortBy { get; set; } = "Name"; // Name, Code, Price, Usage, Date

        [Display(Name = "Sort Order")]
        public string SortOrder { get; set; } = "ASC"; // ASC, DESC
    }

    // For reporting and analytics
    public class PartsAnalyticsViewModel
    {
        public List<PartUsageStatistic> MostUsedParts { get; set; } = new List<PartUsageStatistic>();
        public List<PartUsageStatistic> LeastUsedParts { get; set; } = new List<PartUsageStatistic>();
        public List<OperationCodeUsageStatistic> OperationCodeUsage { get; set; } = new List<OperationCodeUsageStatistic>();

        public decimal TotalPartsValue { get; set; }
        public decimal AveragePartPrice { get; set; }
        public int TotalPartsInStock { get; set; }
        public int LowStockParts { get; set; }

        // Monthly statistics
        public List<MonthlyPartUsage> MonthlyUsage { get; set; } = new List<MonthlyPartUsage>();
    }

    public class PartUsageStatistic
    {
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public int TotalQuantityUsed { get; set; }
        public int UsageCount { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime? LastUsedDate { get; set; }
    }

    public class OperationCodeUsageStatistic
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public int UsageCount { get; set; }
        public int AssociatedPartsCount { get; set; }
        public decimal AverageJobValue { get; set; }
    }

    public class MonthlyPartUsage
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; }
        public int TotalParts { get; set; }
        public decimal TotalValue { get; set; }
        public int UniquePartsUsed { get; set; }
    }

    // For validation and error handling
    public class PartsValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string Summary { get; set; }
    }

}