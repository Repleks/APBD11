using JWT.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class JWTDbContext : IdentityDbContext<IdentityUser>
{
    public JWTDbContext(DbContextOptions<JWTDbContext> options)
        : base(options)
    {
    }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
}