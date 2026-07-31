using Microsoft.EntityFrameworkCore;
using DataVisionAPI.Models;

namespace DataVisionAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Log> Logs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de Usuario
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Usuario_)
                      .HasColumnName("Usuario")
                      .HasMaxLength(50)
                      .IsRequired();
                entity.HasIndex(e => e.Usuario_).IsUnique();
                entity.Property(e => e.Password)
                      .HasMaxLength(255)
                      .IsRequired();
                entity.Property(e => e.Rol)
                      .HasMaxLength(50)
                      .IsRequired();
            });

            // Configuración de Log
            modelBuilder.Entity<Log>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FechaConsulta)
                      .HasDefaultValueSql("GETDATE()")
                      .IsRequired();
                entity.Property(e => e.EndpointConsultado)
                      .HasMaxLength(255)
                      .IsRequired();

                // Relación con Usuario
                entity.HasOne(e => e.Usuario)
                      .WithMany(u => u.Logs)
                      .HasForeignKey(e => e.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Datos iniciales
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Generar hashes dinámicos garantizados con la misma librería BCrypt del proyecto
            string adminHash = BCrypt.Net.BCrypt.HashPassword("admin123", BCrypt.Net.BCrypt.GenerateSalt(12));
            string userHash = BCrypt.Net.BCrypt.HashPassword("user123", BCrypt.Net.BCrypt.GenerateSalt(12));

            modelBuilder.Entity<Usuario>().HasData(
                new Usuario
                {
                    Id = 1,
                    Usuario_ = "admin",
                    Password = adminHash,
                    Rol = "Admin"
                },
                new Usuario
                {
                    Id = 2,
                    Usuario_ = "user",
                    Password = userHash,
                    Rol = "User"
                }
            );
        }
    }
}