using System.ComponentModel.DataAnnotations;
using ProcureTrack.Web.Models;
using ProcureTrack.Web.Services;

namespace ProcureTrack.Web.ViewModels;

/// <summary>Input for approving or rejecting a request. The approver's identity comes from the signed-in user.</summary>
public class DecisionViewModel
{
    [Required(ErrorMessage = "Choose Approve or Reject.")]
    [Display(Name = "Decision")]
    public ApprovalAction? Action { get; set; }

    [StringLength(ApprovalWorkflow.MaxNotesLength, ErrorMessage = "Notes cannot exceed {1} characters.")]
    [Display(Name = "Notes or rejection reason")]
    public string? Notes { get; set; }

    /// <summary>The version of the request the approver was looking at.</summary>
    public byte[]? RowVersion { get; set; }
}