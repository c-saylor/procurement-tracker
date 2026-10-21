using System.ComponentModel.DataAnnotations;

namespace ProcureTrack.Web.Models;

public class Vendor
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress, StringLength(256)]
    public string? ContactEmail { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PurchaseRequest> PurchaseRequests { get; set; } = new List<PurchaseRequest>();
}