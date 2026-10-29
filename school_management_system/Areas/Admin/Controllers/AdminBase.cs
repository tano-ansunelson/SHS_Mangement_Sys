using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using school_management_system.Controllers;
using school_management_system.Data;
using school_management_system.Models.Enums;

namespace school_management_system.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles =Roles.Admin)]    
    public abstract class AdminBase(AppDbContext db) : PortalController(db) { }
   

