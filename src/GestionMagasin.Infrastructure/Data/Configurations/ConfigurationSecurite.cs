using GestionMagasin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionMagasin.Infrastructure.Data.Configurations;

public class ConfigurationRole : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> constructeur)
    {
        constructeur.ToTable("roles");

        constructeur.HasKey(r => r.Id);

        constructeur.Property(r => r.Nom).IsRequired().HasMaxLength(60);
        constructeur.Property(r => r.Description).HasMaxLength(300);
        constructeur.Property(r => r.EstSysteme).HasDefaultValue(false);

        constructeur.HasIndex(r => r.Nom).IsUnique();
    }
}

public class ConfigurationPermission : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> constructeur)
    {
        constructeur.ToTable("permissions");

        constructeur.HasKey(p => p.Id);

        constructeur.Property(p => p.Code).IsRequired().HasMaxLength(80);
        constructeur.Property(p => p.Nom).IsRequired().HasMaxLength(120);
        constructeur.Property(p => p.Description).HasMaxLength(300);
        constructeur.Property(p => p.Categorie).IsRequired().HasMaxLength(60);

        constructeur.HasIndex(p => p.Code).IsUnique();
    }
}

public class ConfigurationRolePermission : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> constructeur)
    {
        constructeur.ToTable("role_permissions");

        constructeur.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        constructeur.HasOne(rp => rp.Role)
            .WithMany(r => r.Permissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        constructeur.HasOne(rp => rp.Permission)
            .WithMany(p => p.Roles)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConfigurationUtilisateur : IEntityTypeConfiguration<Utilisateur>
{
    public void Configure(EntityTypeBuilder<Utilisateur> constructeur)
    {
        constructeur.ToTable("utilisateurs");

        constructeur.HasKey(u => u.Id);

        constructeur.Property(u => u.Nom).IsRequired().HasMaxLength(80);
        constructeur.Property(u => u.Prenom).IsRequired().HasMaxLength(80);
        constructeur.Property(u => u.NomUtilisateur).IsRequired().HasMaxLength(60);
        constructeur.Property(u => u.PasswordHash).IsRequired().HasMaxLength(300);
        constructeur.Property(u => u.Actif).HasDefaultValue(true);
        constructeur.Property(u => u.DateCreation).HasColumnType("timestamp with time zone");
        constructeur.Property(u => u.DerniereConnexion).HasColumnType("timestamp with time zone");

        // La propriété calculée NomComplet n'a pas de colonne dédiée.
        constructeur.Ignore(u => u.NomComplet);

        constructeur.HasIndex(u => u.NomUtilisateur).IsUnique();

        // Un utilisateur ayant réalisé des ventes ne peut pas être supprimé :
        // il est désactivé, ce qui préserve l'historique.
        constructeur.HasOne(u => u.Role)
            .WithMany(r => r.Utilisateurs)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
