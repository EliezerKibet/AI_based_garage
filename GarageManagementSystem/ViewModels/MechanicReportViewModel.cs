using GarageManagementSystem.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace GarageManagementSystem.ViewModels
{
    public class MechanicReportViewModel
    {
        public int Id { get; set; }

        public string PartUsed { get; set; }

        // Car Information
        public int CarId { get; set; }
        public string CarMake { get; set; }
        public string CarModel { get; set; }
        public int CarYear { get; set; }
        public string CarName { get; set; }
        public CarViewModel Car { get; set; }

        // Mechanic Information
        public int MechanicId { get; set; }
        public string MechanicName { get; set; }
        public MechanicViewModel Mechanic { get; set; }
        public DateTime? DateReported { get; set; } = DateTime.UtcNow; // Default to current time

        public List<AssignedCarViewModel> AssignedCars { get; set; } = new List<AssignedCarViewModel>();

        public bool ResolutionStatus { get; set; }

        [Required(ErrorMessage = "Service Details are required")]
        public string ServiceDetails { get; set; }

        public string? AdditionalNotes { get; set; }

        // NEW: Receipt-specific fields
        public string PaymentMode { get; set; } = "Cash";
        public string? CustomerRequest { get; set; }
        public string? ActionTaken { get; set; }
        public string? NextServiceAdvice { get; set; }
        public int? NextServiceKm { get; set; }
        public DateTime? NextServiceDate { get; set; }
        public decimal TaxRate { get; set; } = 6.00m;

        // Parts & Faults
        public List<MechanicReportPartViewModel> Parts { get; set; } = new List<MechanicReportPartViewModel>();
        public List<FaultViewModel> Faults { get; set; } = new List<FaultViewModel>();

        // NEW: Additional collections for receipt functionality
        public List<LabourItemViewModel> LabourItems { get; set; } = new List<LabourItemViewModel>();
        public List<InspectionItemViewModel> InspectionItems { get; set; } = new List<InspectionItemViewModel>();

        // Pricing
        public decimal TotalPrice { get; set; }
        public decimal ServiceFee { get; set; }

        // NEW: Enhanced calculated properties for receipt
        public decimal PartsSubTotal => Parts?.Sum(p => p.TotalAmount) ?? 0;
        public decimal LabourSubTotal => LabourItems?.Sum(l => l.TotalAmountWithTax) ?? 0;
        public decimal TotalAmountWithoutServiceTax => PartsSubTotal + LabourSubTotal;
        public decimal ServiceTaxAmount => TotalAmountWithoutServiceTax * (TaxRate / 100);
        public decimal TotalInvoiceAmount => TotalAmountWithoutServiceTax + ServiceFee;
        public decimal TotalAmountPayable => TotalInvoiceAmount + ServiceTaxAmount;

        // Keep backward compatibility
        public decimal EnhancedTotalPrice => TotalAmountPayable;

        // Add this computed property
        public string Description
        {
            get
            {
                return Faults != null && Faults.Any()
                    ? string.Join(", ", Faults.Select(f => f.Description))
                    : "No faults reported.";
            }
        }

        // NEW: Receipt summary properties
        public string NextServiceInfo
        {
            get
            {
                var parts = new List<string>();
                if (NextServiceKm.HasValue)
                    parts.Add($"{NextServiceKm.Value:N0} km");
                if (NextServiceDate.HasValue)
                    parts.Add(NextServiceDate.Value.ToString("dd/MM/yyyy"));

                return parts.Any() ? string.Join(" or ", parts) + " whichever comes first" : "Not specified";
            }
        }

        public bool HasInspectionIssues => InspectionItems?.Any(i => i.Status != "OK") ?? false;
        public int InspectionIssuesCount => InspectionItems?.Count(i => i.Status != "OK") ?? 0;
    }

    
}