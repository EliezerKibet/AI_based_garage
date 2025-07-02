/**
 * Garage Management System - Print Reports Functionality
 * This script handles the printing of vehicle service reports
 */

// Define printCarReports as a global function so it can be called from inline onclick attributes
window.printCarReports = function (carId, element) {
    try {
        // Get the car name from the button's parent card
        var cardHeader = document.getElementById('heading-' + carId);
        var carButton = cardHeader.querySelector('.btn-link');
        var carName = carButton.textContent.trim();

        // Ensure the accordion for this car is expanded
        var carCollapse = document.getElementById('collapse-' + carId);
        if (carCollapse && typeof bootstrap !== 'undefined') {
            var collapse = new bootstrap.Collapse(carCollapse, { toggle: false });
            collapse.show();
        } else {
            // Fallback for older Bootstrap versions or if bootstrap isn't defined
            $('#collapse-' + carId).collapse('show');
        }

        // Small delay to ensure accordion is expanded before printing
        setTimeout(function () {
            // Create print-friendly version
            var printContent = preparePrintContent(carId, carName);

            // Open new window and print
            var printWindow = window.open('', '_blank');
            if (!printWindow) {
                alert('Please allow popups for this website to print reports');
                return;
            }

            printWindow.document.write(printContent);
            printWindow.document.close();

            // Allow time for content to render then print
            setTimeout(function () {
                printWindow.focus();
                printWindow.print();
            }, 500);
        }, 300);
    } catch (error) {
        console.error('Error printing report:', error);
        alert('There was an error printing the report. Please try again.');
    }
};

// Also define preparePrintContent as a global function
function preparePrintContent(carId, carName) {
    var content = `
    <!DOCTYPE html>
    <html>
    <head>
        <title>Service Reports - ${carName}</title>
        <style>
            body { font-family: Arial, sans-serif; margin: 20px; }
            .header { text-align: center; margin-bottom: 20px; }
            .header h1 { margin-bottom: 5px; }
            .header h2 { margin-bottom: 10px; }
            .company-info { text-align: center; margin-bottom: 30px; }
            .report { border: 1px solid #ddd; padding: 15px; margin-bottom: 20px; border-radius: 5px; break-inside: avoid; }
            .report-header { border-bottom: 1px solid #eee; padding-bottom: 10px; margin-bottom: 15px; display: flex; justify-content: space-between; }
            .report-date { font-weight: bold; }
            .report-details { margin-top: 15px; }
            .report-details h3 { color: #4e73df; margin-top: 15px; margin-bottom: 8px; }
            .parts-table { width: 100%; border-collapse: collapse; margin: 15px 0; break-inside: avoid; }
            .parts-table th, .parts-table td { border: 1px solid #ddd; padding: 8px; text-align: left; }
            .parts-table th { background-color: #f8f9fc; }
            .service-fee { margin-top: 20px; font-weight: bold; }
            .total-price { margin-top: 5px; font-weight: bold; color: #e74a3b; }
            .footer { text-align: center; margin-top: 30px; font-size: 0.9em; color: #666; border-top: 1px solid #eee; padding-top: 15px; }
            /* Print styles without media query */
            body { font-size: 12pt; }
            .no-print { display: none; }
        </style>
    </head>
    <body>
        <div class="header">
            <h1>Vehicle Service History</h1>
            <h2>${carName}</h2>
        </div>
        <div class="company-info">
            <p>UMI Garage Management System</p>
            <p>Generated on ${new Date().toLocaleDateString()} at ${new Date().toLocaleTimeString()}</p>
        </div>`;

    // Get all reports for this car
    var reportsContainer = document.getElementById('collapse-' + carId);
    if (!reportsContainer) {
        content += `<p style="text-align: center; color: #666;">No reports found for this vehicle.</p>`;
        content += `</body></html>`;
        return content;
    }

    var timelineItems = reportsContainer.querySelectorAll('.timeline-item');
    if (!timelineItems || timelineItems.length === 0) {
        content += `<p style="text-align: center; color: #666;">No reports available for this vehicle.</p>`;
        content += `</body></html>`;
        return content;
    }

    // Process each report
    Array.from(timelineItems).forEach(function (report) {
        var dateElement = report.querySelector('.badge-primary');
        var timeElement = report.querySelector('.badge-light');
        var date = dateElement ? dateElement.textContent.trim() : 'Unknown Date';
        var time = timeElement ? timeElement.textContent.trim() : '';

        // Get details from the collapsed content
        var detailsDiv = report.querySelector('.collapse .card-body');
        var serviceDetailsP = detailsDiv ? detailsDiv.querySelector('p') : null;
        var serviceDetails = serviceDetailsP ? serviceDetailsP.textContent.trim() : 'No details available';

        // Check if additional notes section exists
        var additionalNotes = '';
        var additionalNotesHeading = detailsDiv ? detailsDiv.querySelector('h6:nth-of-type(2)') : null;
        if (additionalNotesHeading && additionalNotesHeading.textContent.includes('Additional Notes')) {
            var additionalNotesP = additionalNotesHeading.nextElementSibling;
            if (additionalNotesP && additionalNotesP.tagName === 'P') {
                additionalNotes = additionalNotesP.textContent.trim();
            }
        }

        // Get service fee and total price
        var serviceFeeElement = report.querySelector('.text-success');
        var serviceFee = 'N/A';
        if (serviceFeeElement) {
            var serviceFeeCard = serviceFeeElement.closest('.card');
            var serviceFeeValue = serviceFeeCard ? serviceFeeCard.querySelector('.h5') : null;
            serviceFee = serviceFeeValue ? serviceFeeValue.textContent.trim() : 'N/A';
        }

        var totalPriceElement = report.querySelector('.text-warning');
        var totalPrice = 'N/A';
        if (totalPriceElement) {
            var totalPriceCard = totalPriceElement.closest('.card');
            var totalPriceValue = totalPriceCard ? totalPriceCard.querySelector('.h5') : null;
            totalPrice = totalPriceValue ? totalPriceValue.textContent.trim() : 'N/A';
        }

        content += `
        <div class="report">
            <div class="report-header">
                <div class="report-date">${date} ${time}</div>
            </div>
            <div class="report-details">
                <h3>Service Details</h3>
                <p>${serviceDetails}</p>`;

        if (additionalNotes) {
            content += `
                <h3>Additional Notes</h3>
                <p>${additionalNotes}</p>`;
        }

        // Check if there's a parts table
        var partsTable = report.querySelector('table.table-bordered');
        if (partsTable) {
            content += `
            <h3>Parts Used</h3>
            <table class="parts-table">
                <thead>
                    <tr>
                        <th>Part Name</th>
                        <th>Price</th>
                        <th>Quantity</th>
                        <th>Subtotal</th>
                    </tr>
                </thead>
                <tbody>`;

            var rows = partsTable.querySelectorAll('tbody tr');
            Array.from(rows).forEach(function (row) {
                var cells = row.querySelectorAll('td');
                if (cells.length >= 4) {
                    var partName = cells[0].textContent.trim();
                    var partPrice = cells[1].textContent.trim();
                    var quantity = cells[2].textContent.trim();
                    var subtotal = cells[3].textContent.trim();

                    content += `
                    <tr>
                        <td>${partName}</td>
                        <td>${partPrice}</td>
                        <td>${quantity}</td>
                        <td>${subtotal}</td>
                    </tr>`;
                }
            });

            content += `
                </tbody>
            </table>`;
        }

        content += `
            <div class="service-fee">Service Fee: ${serviceFee}</div>
            <div class="total-price">Total Price: ${totalPrice}</div>
        </div>
    </div>`;
    });

    content += `
        <div class="footer">
            <p>This is an official service record. Thank you for choosing our garage.</p>
        </div>
    </body>
    </html>`;

    return content;
}

$(document).ready(function () {
    // Toggle icon rotation when details are expanded/collapsed
    $('.collapse').on('show.bs.collapse', function () {
        var iconId = $(this).attr('id').replace('details-', 'icon-');
        $('#' + iconId).removeClass('fa-chevron-down').addClass('fa-chevron-up');
    });

    $('.collapse').on('hide.bs.collapse', function () {
        var iconId = $(this).attr('id').replace('details-', 'icon-');
        $('#' + iconId).removeClass('fa-chevron-up').addClass('fa-chevron-down');
    });

    // Function to perform search
    function performSearch() {
        var value = $('#reportSearch').val().toLowerCase();

        if (value.length > 2) {
            $('.report-card').each(function () {
                var cardText = $(this).text().toLowerCase();
                var match = cardText.indexOf(value) > -1;
                $(this).toggle(match);

                if (match) {
                    // Open the accordion if it contains the search term
                    $(this).find('.collapse').collapse('show');
                }
            });
        } else if (value.length === 0) {
            // If search is cleared, show all cards and collapse all accordions
            $('.report-card').show();
            $('.collapse').collapse('hide');
        }
    }

    // Search functionality
    $('#reportSearch').on('keyup', performSearch);

    // Search button click handler
    $('.input-group-append .btn-primary').on('click', function () {
        performSearch();
    });
});