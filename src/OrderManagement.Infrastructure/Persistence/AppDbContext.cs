using Microsoft.EntityFrameworkCore;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Interfaces;

namespace OrderManagement.Infrastructure.Persistence;

public class AppDbContext : DbContext, IUnitOfWork
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(builder =>
        {
            builder.HasKey(o => o.Id);
            builder.Property(o => o.CustomerName).HasMaxLength(200).IsRequired();
            builder.Property(o => o.Status).HasConversion<string>();

            builder.OwnsOne(o => o.ShippingAddress, a =>
            {
                a.Property(p => p.Street).HasMaxLength(300);
                a.Property(p => p.City).HasMaxLength(100);
                a.Property(p => p.State).HasMaxLength(100);
                a.Property(p => p.ZipCode).HasMaxLength(20);
                a.Property(p => p.Country).HasMaxLength(100);
            });

            builder.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey("OrderId")
                .OnDelete(DeleteBehavior.Cascade);

            builder.Ignore(o => o.TotalAmount);
            builder.Ignore(o => o.DomainEvents);
        });

        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.HasKey(i => i.Id);
            builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();

            builder.OwnsOne(i => i.UnitPrice, m =>
            {
                m.Property(p => p.Amount);
                m.Property(p => p.Currency).HasMaxLength(3);
            });

            builder.Ignore(i => i.TotalPrice);
            builder.Ignore(i => i.DomainEvents);
        });
    }
}
