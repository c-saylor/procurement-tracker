using System.ComponentModel.DataAnnotations;

namespace ProcureTrack.Web.Models;

public class Department
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string Code { get; set; } = string.Empty;

    public ICollection<PurchaseRequest> PurchaseRequests { get; set; } = new List<PurchaseRequest>();
}