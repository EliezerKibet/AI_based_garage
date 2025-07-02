using GarageManagementSystem.Data;
using GarageManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace GarageManagementSystem.Pages
{
    public class CarListModel : PageModel
    {
        private readonly AppDbContext _context;
        public List<CarViewModel> Cars { get; set; } = new List<CarViewModel>();
        public DataTable MyTable { get; set; } = new DataTable();

        public CarListModel(AppDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            Cars = await _context.Cars
                .Include(c => c.Owner)
                .Include(c => c.Mechanic)
                .Select(c => new CarViewModel
                {
                    Make = c.Make,
                    Model = c.Model,
                    Year = c.Year,
                    Color = c.Color,
                    OwnerName = c.Owner != null ? c.Owner.FullName : "N/A",
                    AssignedMechanicName = c.Mechanic != null ? c.Mechanic.FullName : "Not Assigned"
                })
                .ToListAsync();

            // Populate the DataTable
            MyTable.Columns.Add("Make", typeof(string));
            MyTable.Columns.Add("Model", typeof(string));
            MyTable.Columns.Add("Year", typeof(int));
            MyTable.Columns.Add("Color", typeof(string));
            MyTable.Columns.Add("Owner", typeof(string));
            MyTable.Columns.Add("Assigned Mechanic", typeof(string));

            foreach (var car in Cars)
            {
                MyTable.Rows.Add(car.Make, car.Model, car.Year, car.Color, car.OwnerName, car.AssignedMechanicName);
            }
        }
    }
}