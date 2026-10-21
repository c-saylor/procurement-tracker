using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ProcureTrack.Web.ViewModels;

/// <summary>
/// Fields a user may submit when creating or editing a request. Status, SubmittedDate, and Id
/// are deliberately absent, so they cannot be over-posted from the form.
/// </summary>
public class PurchaseRequestFormViewModel
{
    [Required(ErrorMessage = "Enter a title.")]
    [StringLength(150, ErrorMessage = "Title cannot exceed 150 characters.")]
    public string? Title { get; set; }

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2,000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Select a department.")]
    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Required(ErrorMessage = "Select a vendor.")]
    [Display(Name = "Vendor")]
    public int? VendorId { get; set; }

    [Required(ErrorMessage = "Enter an amount.")]
    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Amount must be between $0.01 and $1,000,000.00.")]
    [Display(Name = "Amount (USD)")]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Enter the date the purchase is required by.")]
    [Display(Name = "Required by")]
    public DateOnly? RequiredByDate { get; set; }

    [Required(ErrorMessage = "Explain the business need for this purchase.")]
    [StringLength(2000, ErrorMessage = "Justification cannot exceed 2,000 characters.")]
    [Display(Name = "Business justification")]
    public string? BusinessJustification { get; set; }

    /// <summary>Concurrency token from when the edit form was opened (edit only).</summary>
    public byte[]? RowVersion { get; set; }

    // Display-only values. [BindNever] means they are never read from posted form data.
    [BindNever] public int Id { get; set; }
    [BindNever] public string? RequestNumber { get; set; }

    [ValidateNever] public IEnumerable<SelectListItem> Departments { get; set; } = [];
    [ValidateNever] public IEnumerable<SelectListItem> Vendors { get; set; } = [];
}