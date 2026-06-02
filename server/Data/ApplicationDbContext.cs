using Microsoft.EntityFrameworkCore;

namespace UniPM.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options);
