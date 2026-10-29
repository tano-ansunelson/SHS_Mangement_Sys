using Microsoft.AspNetCore.Identity;

namespace school_management_system.Models
{
    public class ApplicationUser:IdentityUser
    {
        public string FullName { get; set; } = "";
        public bool MustChangePassword { get; set; } = false;
        public bool IsActive { get; set; } = true;
    }
}
