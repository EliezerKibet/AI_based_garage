using GarageManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GarageManagementSystem.Data
{
    public class AppDbContext : IdentityDbContext<Users, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Existing DbSets
        public DbSet<Car> Cars { get; set; }
        public DbSet<Fault> Faults { get; set; }
        public DbSet<CarMechanicAssignment> CarMechanicAssignments { get; set; }
        public DbSet<MechanicReport> MechanicReports { get; set; }
        public DbSet<MechanicReportPart> MechanicReportParts { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // NEW: Add DbSets for the new receipt entities
        public DbSet<MechanicReportLabour> MechanicReportLabours { get; set; }
        public DbSet<ServiceInspectionItem> ServiceInspectionItems { get; set; }
        public DbSet<OperationCode> OperationCodes { get; set; }
        public DbSet<ServicePart> ServiceParts { get; set; }
        public DbSet<OperationCodePart> OperationCodeParts { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure OwnerId relationship
            modelBuilder.Entity<Car>()
                .HasOne(c => c.Owner)
                .WithMany(u => u.Cars)
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure UserId relationship
            modelBuilder.Entity<Car>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Fault relationship
            modelBuilder.Entity<Fault>()
                .HasOne(f => f.Car)
                .WithMany(c => c.Faults)
                .HasForeignKey(f => f.CarId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure CarMechanicAssignment relationships
            modelBuilder.Entity<CarMechanicAssignment>()
                .HasOne(cma => cma.Car)
                .WithMany(c => c.CarMechanicAssignments)
                .HasForeignKey(cma => cma.CarId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CarMechanicAssignment>()
                .HasOne(cma => cma.Mechanic)
                .WithMany(u => u.CarMechanicAssignments)
                .HasForeignKey(cma => cma.MechanicId)
                .OnDelete(DeleteBehavior.SetNull);

            // Mechanic Report relationships
            modelBuilder.Entity<MechanicReport>()
                .HasOne(mr => mr.Car)
                .WithMany(c => c.Reports)
                .HasForeignKey(mr => mr.CarId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MechanicReport>()
                .HasOne(mr => mr.Mechanic)
                .WithMany(u => u.MechanicReports)
                .HasForeignKey(mr => mr.MechanicId)
                .OnDelete(DeleteBehavior.SetNull); // Mechanic can leave, but reports remain

            // Configure MechanicReportPart relationships
            modelBuilder.Entity<MechanicReportPart>()
                .HasOne(mrp => mrp.MechanicReport)
                .WithMany(mr => mr.Parts)
                .HasForeignKey(mrp => mrp.MechanicReportId)
                .OnDelete(DeleteBehavior.Cascade); // If report is deleted, delete parts

            modelBuilder.Entity<MechanicReportPart>()
                .HasOne(mrp => mrp.Car)
                .WithMany()
                .HasForeignKey(mrp => mrp.CarId)
                .OnDelete(DeleteBehavior.SetNull); // Car deletion does not affect parts

            // NEW: Configure MechanicReportLabour relationships
            modelBuilder.Entity<MechanicReportLabour>()
                .HasOne(mrl => mrl.MechanicReport)
                .WithMany(mr => mr.LabourItems)
                .HasForeignKey(mrl => mrl.MechanicReportId)
                .OnDelete(DeleteBehavior.Cascade); // If report is deleted, delete labour items

            // NEW: Configure ServiceInspectionItem relationships
            modelBuilder.Entity<ServiceInspectionItem>()
                .HasOne(sii => sii.MechanicReport)
                .WithMany(mr => mr.InspectionItems)
                .HasForeignKey(sii => sii.MechanicReportId)
                .OnDelete(DeleteBehavior.Cascade); // If report is deleted, delete inspection items

            // Appointment relationships
            modelBuilder.Entity<Appointment>()
               .HasOne(a => a.User)
               .WithMany(u => u.Appointments)
               .HasForeignKey(a => a.UserId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Car)
                .WithMany(c => c.Appointments)
                .HasForeignKey(a => a.CarId)
                .OnDelete(DeleteBehavior.Cascade);

            // NEW: Configure Mechanic relationship for Appointments
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Mechanic)
                .WithMany()
                .HasForeignKey(a => a.MechanicId)
                .OnDelete(DeleteBehavior.SetNull); // Mechanic can be unassigned

            // NEW: Configure Notification relationships
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade); // If user is deleted, delete notifications

            // NEW: Configure Fault-MechanicReport relationship
            modelBuilder.Entity<Fault>()
                .HasOne(f => f.MechanicReport)
                .WithMany(mr => mr.Faults)
                .HasForeignKey(f => f.MechanicReportId)
                .OnDelete(DeleteBehavior.SetNull); // Report can be deleted without affecting fault

            modelBuilder.Entity<Fault>()
                .HasOne(f => f.Mechanic)
                .WithMany()
                .HasForeignKey(f => f.MechanicId)
                .OnDelete(DeleteBehavior.SetNull); // Mechanic can be unassigned

            // NEW: Configure Operation Code system relationships
            modelBuilder.Entity<OperationCodePart>()
                .HasOne(ocp => ocp.OperationCode)
                .WithMany(oc => oc.OperationCodeParts) // OperationCode doesn't directly contain ServiceParts, it goes through OperationCodeParts
                .HasForeignKey(ocp => ocp.OperationCodeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OperationCodePart>()
                .HasOne(ocp => ocp.ServicePart)
                .WithMany(sp => sp.OperationCodeParts)
                .HasForeignKey(ocp => ocp.ServicePartId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure unique constraints for Operation Code system
            modelBuilder.Entity<OperationCode>()
                .HasIndex(o => o.Code)
                .IsUnique();

            modelBuilder.Entity<ServicePart>()
                .HasIndex(s => s.PartNumber)
                .IsUnique();

            // Configure the many-to-many relationship unique constraint
            modelBuilder.Entity<OperationCodePart>()
                .HasIndex(ocp => new { ocp.OperationCodeId, ocp.ServicePartId })
                .IsUnique();

            // NEW: Configure decimal precision for financial fields
            modelBuilder.Entity<MechanicReport>()
                .Property(mr => mr.ServiceFee)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<MechanicReportPart>()
                .Property(mrp => mrp.PartPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<MechanicReportLabour>()
                .Property(mrl => mrl.TotalAmountWithoutTax)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<MechanicReportLabour>()
                .Property(mrl => mrl.TaxAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<MechanicReportLabour>()
                .Property(mrl => mrl.TaxRate)
                .HasColumnType("decimal(5,2)");

            modelBuilder.Entity<MechanicReport>()
                .Property(mr => mr.TaxRate)
                .HasColumnType("decimal(5,2)");

            // NEW: Configure decimal precision for ServicePart
            modelBuilder.Entity<ServicePart>()
                .Property(sp => sp.Price)
                .HasColumnType("decimal(18,2)");

            // SEED DATA: Initial operation codes
            modelBuilder.Entity<OperationCode>().HasData(
                new OperationCode { Id = 1, Code = "FLRS10", Name = "Oil Change Service", Description = "Standard oil change and filter replacement service", CreatedDate = new DateTime(2024, 1, 1) },
                new OperationCode { Id = 2, Code = "PART1", Name = "General Parts", Description = "General automotive parts and accessories", CreatedDate = new DateTime(2024, 1, 1) },
                new OperationCode { Id = 3, Code = "BRKS20", Name = "Brake Service", Description = "Brake system maintenance and parts", CreatedDate = new DateTime(2024, 1, 1) },
                new OperationCode { Id = 4, Code = "TIRE30", Name = "Tire Service", Description = "Tire installation, balancing, and wheel services", CreatedDate = new DateTime(2024, 1, 1) },
                new OperationCode { Id = 5, Code = "BATRY40", Name = "Battery Service", Description = "Battery replacement and electrical system parts", CreatedDate = new DateTime(2024, 1, 1) }
            );

            // SEED DATA: Initial service parts
            modelBuilder.Entity<ServicePart>().HasData(
                // Oil Change Parts
                new ServicePart { Id = 1, PartNumber = "15601-P2A12", PartName = "ELEMENT S/A OIL FILTER", PartDescription = "Standard oil filter element", Price = 11.90m, CreatedDate = new DateTime(2024, 1, 1) },
                new ServicePart { Id = 2, PartNumber = "70010105", PartName = "PEO FULL-SYN 0W-20 API SN -3L", PartDescription = "Full synthetic motor oil 3 liters", Price = 140.50m, CreatedDate = new DateTime(2024, 1, 1) },
                new ServicePart { Id = 3, PartNumber = "90044-30281", PartName = "GASKET", PartDescription = "Oil drain plug gasket", Price = 3.80m, CreatedDate = new DateTime(2024, 1, 1) },

                // General Parts
                new ServicePart { Id = 4, PartNumber = "999-40011-00000", PartName = "BATTERY TERMINAL PROTECTOR (SMALL)", PartDescription = "Small battery terminal protector spray", Price = 3.80m, CreatedDate = new DateTime(2024, 1, 1) },
                new ServicePart { Id = 5, PartNumber = "999-50001-00000", PartName = "WINDSHIELD WASHER (30ML)", PartDescription = "Windshield washer fluid concentrate", Price = 1.70m, CreatedDate = new DateTime(2024, 1, 1) },
                new ServicePart { Id = 6, PartNumber = "999-53100-00001", PartName = "INJECTOR CLEANER 95ML", PartDescription = "Fuel injector cleaning solution", Price = 24.00m, CreatedDate = new DateTime(2024, 1, 1) },

                // Brake Parts
                new ServicePart { Id = 7, PartNumber = "43022-S5A-000", PartName = "BRAKE PAD SET FRONT", PartDescription = "Front brake pad set - ceramic", Price = 85.00m, CreatedDate = new DateTime(2024, 1, 1) },
                new ServicePart { Id = 8, PartNumber = "43022-S5A-013", PartName = "BRAKE PAD SET REAR", PartDescription = "Rear brake pad set - ceramic", Price = 65.00m, CreatedDate = new DateTime(2024, 1, 1) },

                // Tire Parts
                new ServicePart { Id = 9, PartNumber = "TYR-205-55-16", PartName = "TIRE 205/55R16", PartDescription = "All-season tire 205/55R16", Price = 180.00m, CreatedDate = new DateTime(2024, 1, 1) },
                new ServicePart { Id = 10, PartNumber = "TYR-225-45-17", PartName = "TIRE 225/45R17", PartDescription = "Performance tire 225/45R17", Price = 220.00m, CreatedDate = new DateTime(2024, 1, 1) },

                // Battery Parts
                new ServicePart { Id = 11, PartNumber = "BAT-55D23L", PartName = "BATTERY 55D23L 12V", PartDescription = "12V automotive battery 55Ah", Price = 180.00m, CreatedDate = new DateTime(2024, 1, 1) },
                new ServicePart { Id = 12, PartNumber = "BAT-75D23L", PartName = "BATTERY 75D23L 12V", PartDescription = "12V automotive battery 75Ah", Price = 220.00m, CreatedDate = new DateTime(2024, 1, 1) }
            );

            // SEED DATA: Operation code-part relationships
            modelBuilder.Entity<OperationCodePart>().HasData(
                // FLRS10 - Oil Change Service
                new OperationCodePart { Id = 1, OperationCodeId = 1, ServicePartId = 1, IsDefault = true, AssignedDate = new DateTime(2024, 1, 1) },
                new OperationCodePart { Id = 2, OperationCodeId = 1, ServicePartId = 2, IsDefault = true, AssignedDate = new DateTime(2024, 1, 1) },
                new OperationCodePart { Id = 3, OperationCodeId = 1, ServicePartId = 3, IsDefault = true, AssignedDate = new DateTime(2024, 1, 1) },

                // PART1 - General Parts
                new OperationCodePart { Id = 4, OperationCodeId = 2, ServicePartId = 4, IsDefault = false, AssignedDate = new DateTime(2024, 1, 1) },
                new OperationCodePart { Id = 5, OperationCodeId = 2, ServicePartId = 5, IsDefault = false, AssignedDate = new DateTime(2024, 1, 1) },
                new OperationCodePart { Id = 6, OperationCodeId = 2, ServicePartId = 6, IsDefault = false, AssignedDate = new DateTime(2024, 1, 1) },

                // BRKS20 - Brake Service
                new OperationCodePart { Id = 7, OperationCodeId = 3, ServicePartId = 7, IsDefault = true, AssignedDate = new DateTime(2024, 1, 1) },
                new OperationCodePart { Id = 8, OperationCodeId = 3, ServicePartId = 8, IsDefault = true, AssignedDate = new DateTime(2024, 1, 1) },

                // TIRE30 - Tire Service
                new OperationCodePart { Id = 9, OperationCodeId = 4, ServicePartId = 9, IsDefault = false, AssignedDate = new DateTime(2024, 1, 1) },
                new OperationCodePart { Id = 10, OperationCodeId = 4, ServicePartId = 10, IsDefault = false, AssignedDate = new DateTime(2024, 1, 1) },

                // BATRY40 - Battery Service
                new OperationCodePart { Id = 11, OperationCodeId = 5, ServicePartId = 11, IsDefault = true, AssignedDate = new DateTime(2024, 1, 1) },
                new OperationCodePart { Id = 12, OperationCodeId = 5, ServicePartId = 12, IsDefault = false, AssignedDate = new DateTime(2024, 1, 1) }
            );
        }
    }
}