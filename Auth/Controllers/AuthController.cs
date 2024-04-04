using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using System.Security.Claims;

namespace Auth.Controllers
{
    [ApiController]
    [Route("[controller]")]

    public class AuthController : ControllerBase
    {
        [HttpGet("/")]
        public IActionResult Index()
        {
            return Ok(HttpContext.User.Claims.Select(x => new { x.Type, x.Value }).ToList());
        }



        string some = "https://localhost:7206/callback";
        [HttpGet("/login")]
        public IResult Login()
        {
            return Results.Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = "https://localhost:7206/callback"
                },
                authenticationSchemes: new List<string> { "github" });
        }

        [HttpGet("/get-user-data")]
        public IActionResult GetUserData()
        {
            var claims = HttpContext.User.Claims;

            var id = claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            return Ok(new { id, name, email });
        }


        [Route("/logout")]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("cookie"); // Sign out from the cookie authentication scheme
            return Redirect("/"); // Redirect the user after logging out
        }



        [HttpGet("/protected")]
        public IActionResult Protected()
        {
            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                return Ok(new { message = "This is protected data only for authenticated users." });
            }
            return Redirect("/login");
        }

        



    }
}
