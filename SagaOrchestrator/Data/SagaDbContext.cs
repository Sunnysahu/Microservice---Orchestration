using Microsoft.EntityFrameworkCore;
using SagaOrchestrator.Models;

namespace SagaOrchestrator.Data;

public class SagaDbContext(DbContextOptions<SagaDbContext> options) : DbContext(options)
{
    public DbSet<SagaInstance> SagaInstances => Set<SagaInstance>();
}

