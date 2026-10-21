using System.ComponentModel.DataAnnotations;
using ProcureTrack.Web.Services;

namespace ProcureTrack.Web.ViewModels;

/// <summary>Input for approving or rejecting a request.</summary>
public class DecisionViewModel
{
   [Required(ErrorMessage = "Enter your name to record the decision.")]
   [StringLength(ApprovalWorkflow.MaxActorNameLength, ErrorMessage = "Name cannot exceed {1} characters.")]
   [Display(Name = "Your name")]
   public string? ActedBy { get; set; }

   [StringLength(ApprovalWorkflow.MaxNotesLength, ErrorMessage = "Notes cannot exceed {1} characters.")]
   [Display(Name = "Notes or rejection reason")]
   public string? Notes { get; set; }

   ///<summary>The version of the request the approver was looking at.</summary>
   public byte[]? RowVersion { get; set; }
}