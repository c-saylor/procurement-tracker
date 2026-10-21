using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcureTrack.Web.Security;

namespace ProcureTrack.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    // GET /Account/Login?ReturnUrl=/PurchaseRequests
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(DemoUsers.All);
    }

    // POST /Account/Login
    [HttpPost]
    public async Task<IActionResult> Login(string? userKey, string? returnUrl = null)
    {
        var user = DemoUsers.Find(userKey);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Choose one of the demo users.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(DemoUsers.All);
        }

        // Claims are facts about the user, stored in the encrypted authentication cookie.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Key),
            new(ClaimTypes.Name, user.DisplayName),
            new("title", user.Title),
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        // Only follow local return URLs; otherwise a crafted link could bounce users to a phishing site.
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // POST /Account/Logout (POST + antiforgery, so a malicious link cannot sign users out)
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // GET /Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}