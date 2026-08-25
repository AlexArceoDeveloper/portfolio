using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api;

public sealed class CommerceDbContext(DbContextOptions<CommerceDbContext> options)
    : DbContext(options)
{
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var payment = modelBuilder.Entity<PaymentRecord>();
        payment.ToTable("payments");
        payment.HasKey(item => item.PaymentId);
        payment.HasIndex(item => item.IdempotencyKey).IsUnique();
        payment.Property(item => item.IdempotencyKey).HasMaxLength(200);
        payment.Property(item => item.OrderId).HasMaxLength(120);
        payment.Property(item => item.Currency).HasMaxLength(3);
        payment.Property(item => item.Status).HasMaxLength(80);
        payment.Property(item => item.RiskDecision).HasMaxLength(30);
        payment.Property(item => item.RiskReasonsJson).HasColumnType("jsonb");
        payment.Property(item => item.Amount).HasPrecision(18, 2);
    }
}

public sealed class PaymentRecord
{
    public Guid PaymentId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string OrderId { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
    public int RiskScore { get; set; }
    public required string RiskDecision { get; set; }
    public required string RiskReasonsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static PaymentRecord FromDomain(string idempotencyKey, PaymentResult payment) => new()
    {
        PaymentId = payment.PaymentId,
        IdempotencyKey = idempotencyKey,
        OrderId = payment.OrderId,
        Amount = payment.Amount,
        Currency = payment.Currency,
        Status = payment.Status,
        RiskScore = payment.Risk.Score,
        RiskDecision = payment.Risk.Decision,
        RiskReasonsJson = JsonSerializer.Serialize(payment.Risk.Reasons),
        CreatedAt = payment.CreatedAt
    };

    public PaymentResult ToDomain() => new(
        PaymentId,
        OrderId,
        Amount,
        Currency,
        Status,
        new RiskAssessment(
            RiskScore,
            RiskDecision,
            JsonSerializer.Deserialize<string[]>(RiskReasonsJson) ?? []),
        CreatedAt);
}

public sealed class PostgresPaymentStore(IDbContextFactory<CommerceDbContext> contextFactory)
    : IPaymentStore
{
    public async Task<PaymentResult?> FindAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await database.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdempotencyKey == idempotencyKey,
                cancellationToken);
        return record?.ToDomain();
    }

    public async Task<(PaymentResult Result, bool Created)> AddAsync(
        string idempotencyKey,
        PaymentResult payment,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        database.Payments.Add(PaymentRecord.FromDomain(idempotencyKey, payment));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return (payment, true);
        }
        catch (DbUpdateException exception)
        {
            database.ChangeTracker.Clear();
            var existing = await database.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.IdempotencyKey == idempotencyKey,
                    cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException(
                    "The payment could not be persisted and no idempotent result was found.",
                    exception);
            }

            return (existing.ToDomain(), false);
        }
    }
}
