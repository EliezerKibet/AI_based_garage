using GarageManagementSystem.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace GarageManagementSystem.Models.ViewModels
{
    public class EditReportViewModel
    {
        public int Id { get; set; }

        [Required]
        public string ServiceDetails { get; set; } = string.Empty;

        public string AdditionalNotes { get; set; } = string.Empty;

        [Required]
        public decimal ServiceFee { get; set; }

        // New receipt fields
        public string PaymentMode { get; set; } = "Cash";
        public string? CustomerRequest { get; set; }
        public string? ActionTaken { get; set; }
        public string? NextServiceAdvice { get; set; }
        public int? NextServiceKm { get; set; }
        public DateTime? NextServiceDate { get; set; }
        public decimal TaxRate { get; set; } = 6.00m;

        // Collections
        public List<PartViewModel> Parts { get; set; } = new List<PartViewModel>();
        public List<LabourItemViewModel> LabourItems { get; set; } = new List<LabourItemViewModel>();
        public List<InspectionItemViewModel> InspectionItems { get; set; } = new List<InspectionItemViewModel>();
    }

    public class PartViewModel
    {
        public string PartName { get; set; } = string.Empty;
        public decimal PartPrice { get; set; }
        public int Quantity { get; set; }

        // New receipt fields
        public string? OperationCode { get; set; }
        public string? PartNumber { get; set; }
        public string? PartDescription { get; set; }

        // Add calculated property
        public decimal TotalAmount => PartPrice * Quantity;
    }

    public class LabourItemViewModel
    {
        public string OperationCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal TotalAmountWithoutTax { get; set; }
        public decimal TaxRate { get; set; } = 6.00m;
        public decimal TaxAmount { get; set; }

        // ADD THIS MISSING PROPERTY
        public decimal TotalAmountWithTax => TotalAmountWithoutTax + TaxAmount;
    }

    // New InspectionItemViewModel
    public class InspectionItemViewModel
    {
        public string ItemName { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string Status { get; set; } = "OK";
        public string? Recommendations { get; set; }
    }
}