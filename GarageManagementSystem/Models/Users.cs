using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace GarageManagementSystem.Models
{
    public class Users : IdentityUser<int>
    {
        public string? FullName { get; set; }

        public string? ProfilePicture { get; set; }

        public virtual ICollection<Car> Cars { get; set; } = new List<Car>();

        public virtual ICollection<MechanicReport> MechanicReports { get; set; } = new List<MechanicReport>();

        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        public virtual ICollection<CarMechanicAssignment> CarMechanicAssignments { get; set; } = new List<CarMechanicAssignment>();

        [JsonIgnore]
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    public class Car
    {
        public int Id { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public string Color { get; set; }
        public string LicenseNumber { get; set; }
        public string FuelType { get; set; }
        public string? ChassisNumber { get; set; }

        public int? OwnerId { get; set; }
        [ForeignKey("OwnerId")]
        public virtual Users Owner { get; set; }

        public int? MechanicId { get; set; }
        [ForeignKey("MechanicId")]
        public virtual Users Mechanic { get; set; }

        public int? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual Users User { get; set; }

        public virtual ICollection<Fault> Faults { get; set; }
        public virtual ICollection<MechanicReport> Reports { get; set; } = new List<MechanicReport>();
        public virtual ICollection<CarMechanicAssignment> CarMechanicAssignments { get; set; }
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }

    public class Fault
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Car is required")]
        public int CarId { get; set; }

        [DataType(DataType.Date)]
        public DateTime DateReportedOn { get; set; } = DateTime.UtcNow;

        public bool ResolutionStatus { get; set; } = false;

        [Required]
        [StringLength(500)]
        public string Description { get; set; }

        public int? MechanicId { get; set; } // Nullable mechanic
        public Users Mechanic { get; set; } // Navigation property

        public int? MechanicReportId { get; set; }
        public MechanicReport MechanicReport { get; set; }

        // Navigation properties
        [ForeignKey("CarId")]
        public virtual Car Car { get; set; }
    }

    public class CarMechanicAssignment
    {
        public int Id { get; set; }
        public int CarId { get; set; }
        [ForeignKey("CarId")]
        public virtual Car? Car { get; set; }
        public int? MechanicId { get; set; }
        [ForeignKey("MechanicId")]
        public Users? Mechanic { get; set; } // Make it nullable
        public DateTime AssignedDate { get; set; }
    }

    // Operation Code model - Admin can manage service categories
    public class OperationCode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Code { get; set; } // e.g., "FLRS10", "BRKS20"

        [Required]
        [StringLength(100)]
        public string Name { get; set; } // e.g., "Oil Change Service", "Brake Service"

        [StringLength(500)]
        public string? Description { get; set; } // Detailed description

        public bool IsActive { get; set; } = true; // Admin can deactivate codes

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation property to junction table (not directly to ServiceParts)
        public virtual ICollection<OperationCodePart> OperationCodeParts { get; set; } = new List<OperationCodePart>();
    }

    // Service Parts model - Admin can manage parts inventory
    public class ServicePart
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string PartNumber { get; set; } // e.g., "15601-P2A12"

        [Required]
        [StringLength(200)]
        public string PartName { get; set; } // e.g., "ELEMENT S/A OIL FILTER"

        [StringLength(500)]
        public string? PartDescription { get; set; } // Detailed description

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public bool IsAvailable { get; set; } = true; // Admin can mark as unavailable

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdated { get; set; }

        // Navigation properties
        public virtual ICollection<OperationCodePart> OperationCodeParts { get; set; } = new List<OperationCodePart>();
    }

    // Junction table - Links operation codes with compatible parts
    public class OperationCodePart
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OperationCodeId { get; set; }
        [ForeignKey("OperationCodeId")]
        public virtual OperationCode OperationCode { get; set; }

        [Required]
        public int ServicePartId { get; set; }
        [ForeignKey("ServicePartId")]
        public virtual ServicePart ServicePart { get; set; }

        public bool IsDefault { get; set; } = false; // Mark commonly used parts for this operation

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    }

    // ENHANCED MechanicReport with receipt functionality
    public class MechanicReport
    {
        [Key]
        public int Id { get; set; }

        public int CarId { get; set; }
        [ForeignKey("CarId")]
        public virtual Car Car { get; set; }

        public int? MechanicId { get; set; }
        [ForeignKey("MechanicId")]
        public virtual Users Mechanic { get; set; }

        [Required]
        [StringLength(500)]
        public string ServiceDetails { get; set; }

        [Required]
        public DateTime DateReported { get; set; }

        [StringLength(200)]
        public string? AdditionalNotes { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ServiceFee { get; set; }

        // NEW: Additional receipt-related properties
        [StringLength(50)]
        public string PaymentMode { get; set; } = "Cash"; // Cash, Card, etc.

        [StringLength(100)]
        public string? CustomerRequest { get; set; } // What customer requested

        [StringLength(100)]
        public string? ActionTaken { get; set; } // What was done

        [StringLength(200)]
        public string? NextServiceAdvice { get; set; } // Next service recommendations

        public int? NextServiceKm { get; set; } // Next service mileage
        public DateTime? NextServiceDate { get; set; } // Next service date

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } = 6.00m; // Tax rate (6% as shown in receipt)

        // Collections
        public ICollection<Fault> Faults { get; set; } = new List<Fault>();
        public List<MechanicReportPart> Parts { get; set; } = new List<MechanicReportPart>();
        public List<MechanicReportLabour> LabourItems { get; set; } = new List<MechanicReportLabour>();
        public List<ServiceInspectionItem> InspectionItems { get; set; } = new List<ServiceInspectionItem>();

        [NotMapped]
        public decimal PartsSubTotal => Parts?.Sum(p => p.TotalAmount) ?? 0;

        [NotMapped]
        public decimal TaxAmount => PartsSubTotal * (TaxRate / 100);

        [NotMapped]
        public decimal TotalAmountPayable => PartsSubTotal + ServiceFee + TaxAmount;

        [NotMapped]
        public decimal TotalPrice => TotalAmountPayable;

        [NotMapped]
        [System.Obsolete("Use TotalAmountPayable instead")]
        public decimal LabourSubTotal => LabourItems?.Sum(l => l.TotalAmountWithTax) ?? 0;

        [NotMapped]
        [System.Obsolete("Use TotalAmountPayable instead")]
        public decimal TotalAmountWithoutServiceTax => PartsSubTotal + LabourSubTotal;

        [NotMapped]
        [System.Obsolete("Use TaxAmount instead")]
        public decimal ServiceTaxAmount => TotalAmountWithoutServiceTax * (TaxRate / 100);

        [NotMapped]
        [System.Obsolete("Use TotalAmountPayable instead")]
        public decimal TotalInvoiceAmount => TotalAmountWithoutServiceTax + ServiceFee;
    }

    public class MechanicReportPart
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MechanicReportId { get; set; }
        [ForeignKey("MechanicReportId")]
        public MechanicReport MechanicReport { get; set; }

        [StringLength(100)]
        public string? PartName { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PartPrice { get; set; }

        [Required]
        public int Quantity { get; set; }

        public int? CarId { get; set; }
        [ForeignKey("CarId")]
        public virtual Car Car { get; set; }

        [StringLength(50)]
        public string? OperationCode { get; set; } 

        [StringLength(100)]
        public string? PartNumber { get; set; } 

        [StringLength(200)]
        public string? PartDescription { get; set; } 

        [NotMapped]
        public decimal RetailPrice => PartPrice; 

        [NotMapped]
        public decimal TotalAmount => Quantity * PartPrice;
    }

    public class MechanicReportLabour
    {
        [Key]
        public int Id { get; set; }

        public int MechanicReportId { get; set; }
        [ForeignKey("MechanicReportId")]
        public virtual MechanicReport MechanicReport { get; set; }

        [Required]
        [StringLength(50)]
        public string OperationCode { get; set; } 

        [Required]
        [StringLength(300)]
        public string Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmountWithoutTax { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } 

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [NotMapped]
        public decimal TotalAmountWithTax => TotalAmountWithoutTax + TaxAmount;
    }

    public class ServiceInspectionItem
    {
        [Key]
        public int Id { get; set; }

        public int MechanicReportId { get; set; }
        [ForeignKey("MechanicReportId")]
        public virtual MechanicReport MechanicReport { get; set; }

        [Required]
        [StringLength(100)]
        public string ItemName { get; set; }

        [Required]
        [StringLength(50)]
        public string Result { get; set; } 

        [Required]
        [StringLength(20)]
        public string Status { get; set; } 

        [StringLength(200)]
        public string? Recommendations { get; set; }
    }

    public class Appointment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime AppointmentDate { get; set; }

        public TimeSpan AppointmentTime { get; set; }

        [Required]
        public int? CarId { get; set; }

        [ForeignKey("CarId")]
        public Car Car { get; set; }

        public string? Notes { get; set; }

        public string Status { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual Users User { get; set; }

        public string MechanicName { get; set; }

        public int? MechanicId { get; set; }

        [ForeignKey("MechanicId")]
        public virtual Users Mechanic { get; set; }
    }

    public class ChatRequest
    {
        internal List<ChatMessage> Messages;

        public string Message { get; set; }
        public string ModelId { get; internal set; }
    }

    public class LlamaResponse
    {
        public string Model { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Response { get; set; }
        public bool Done { get; set; }
        public string DoneReason { get; set; }
        public List<int> Context { get; set; }
        public long TotalDuration { get; set; }
        public long LoadDuration { get; set; }
        public int PromptEvalCount { get; set; }
        public long PromptEvalDuration { get; set; }
        public int EvalCount { get; set; }
        public long EvalDuration { get; set; }
    }

    public class Notification
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required] 
        [StringLength(500)]  
        public string Message { get; set; }

        public DateTime DateCreated { get; set; }

        public bool IsRead { get; set; } = false;  

        [JsonIgnore]
        public virtual Users User { get; set; }

    }

    public static class MechanicReportExtensions
    {
        public static void AddPartWithDetails(
            this MechanicReport report,
            string operationCode,
            string partNumber,
            string partName,
            string description,
            int quantity,
            decimal retailPrice,
            int? carId = null)
        {
            report.Parts.Add(new MechanicReportPart
            {
                OperationCode = operationCode,
                PartNumber = partNumber,
                PartName = partName,
                PartDescription = description,
                Quantity = quantity,
                PartPrice = retailPrice,
                CarId = carId
            });
        }

        public static void AddInspectionItem(
            this MechanicReport report,
            string itemName,
            string result,
            string status,
            string? recommendations = null)
        {
            report.InspectionItems.Add(new ServiceInspectionItem
            {
                ItemName = itemName,
                Result = result,
                Status = status,
                Recommendations = recommendations
            });
        }

        public static void SetNextService(
            this MechanicReport report,
            int nextKm,
            DateTime nextDate,
            string advice = null)
        {
            report.NextServiceKm = nextKm;
            report.NextServiceDate = nextDate;
            report.NextServiceAdvice = advice;
        }

        public static MechanicReport CreateSimplifiedReceiptExample(
            int carId,
            int mechanicId,
            string serviceDetails = "10,000KM SERVICE")
        {
            var report = new MechanicReport
            {
                CarId = carId,
                MechanicId = mechanicId,
                ServiceDetails = serviceDetails,
                DateReported = DateTime.Now,
                PaymentMode = "Cash",
                CustomerRequest = "SERVICE 10,000KM",
                ActionTaken = "DONE MAINTENANCE SERVICE\nDONE CLEAN / LUBRICATE REAR LH MIRROR(NOISE)",
                NextServiceAdvice = "Next Service Advice",
                ServiceFee = 50.00m 
            };

            report.AddPartWithDetails("FLRS10", "15601-P2A12", "ELEMENT S/A OIL FILTER", "ELEMENT S/A OIL FILTER", 1, 11.90m, carId);
            report.AddPartWithDetails("FLRS10", "70010105", "PEO FULL-SYN 0W-20 API SN -3L", "PEO FULL-SYN 0W-20 API SN -3L", 1, 140.50m, carId);
            report.AddPartWithDetails("FLRS10", "90044-30281", "GASKET", "GASKET", 1, 3.80m, carId);
            report.AddPartWithDetails("PART1", "999-40011-00000", "BATTERY TERMINAL PROTECTOR (SMALL)", "BATTERY TERMINAL PROTECTOR (SMALL)", 1, 3.80m, carId);
            report.AddPartWithDetails("PART1", "999-50001-00000", "WINDSHIELD WASHER (30ML)", "WINDSHIELD WASHER (30ML)", 2, 1.70m, carId);

           report.AddInspectionItem("Tyre tread depth (mm)", "6MM", "OK");
            report.AddInspectionItem("Battery Result", "320A", "OK");

            report.SetNextService(19300, new DateTime(2023, 9, 16), "Your next maintenance service is due on 19300km or 16/09/2023 whichever comes first");

            return report;
        }

        [System.Obsolete("Use CreateSimplifiedReceiptExample instead")]
        public static void AddLabourItem(
            this MechanicReport report,
            string operationCode,
            string description,
            decimal amountWithoutTax = 0m,
            decimal taxRate = 6.00m)
        {
            var taxAmount = amountWithoutTax * (taxRate / 100);

            report.LabourItems.Add(new MechanicReportLabour
            {
                OperationCode = operationCode,
                Description = description,
                TotalAmountWithoutTax = amountWithoutTax,
                TaxRate = taxRate,
                TaxAmount = taxAmount
            });
        }

        [System.Obsolete("Use CreateSimplifiedReceiptExample instead")]
        public static MechanicReport CreateReceiptExample(
            int carId,
            int mechanicId,
            string serviceDetails = "10,000KM SERVICE")
        {
            return CreateSimplifiedReceiptExample(carId, mechanicId, serviceDetails);
        }
    }
}