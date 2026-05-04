using Microsoft.EntityFrameworkCore;

namespace deeplynx.datalayer.Models;

public class LatticeContext : DbContext
{
    public LatticeContext()
    {
    }

    public LatticeContext(DbContextOptions<LatticeContext> options) : base(options)
    {
    }

    public virtual DbSet<ExtractionClass> ExtractionClasses { get; set; }
    public virtual DbSet<ExtractionRecord> ExtractionRecords { get; set; }
    public virtual DbSet<ExtractionRelationship> ExtractionRelationships { get; set; }
    public virtual DbSet<ExtractionEdge> ExtractionEdges { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("lattice");

        modelBuilder.Entity<ExtractionClass>(entity =>
        {
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasMany(e => e.OriginRelationships)
                .WithOne(r => r.OriginClass)
                .HasForeignKey(r => r.OriginClassId)
                .HasConstraintName("extraction_relationships_origin_class_id_fkey");

            entity.HasMany(e => e.DestinationRelationships)
                .WithOne(r => r.DestinationClass)
                .HasForeignKey(r => r.DestinationClassId)
                .HasConstraintName("extraction_relationships_destination_class_id_fkey");
        });

        modelBuilder.Entity<ExtractionRecord>(entity =>
        {
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.ExtractionClass)
                .WithMany(c => c.Records)
                .HasForeignKey(e => e.ExtractionClassId)
                .HasConstraintName("extraction_records_extraction_class_id_fkey");

            entity.HasMany(e => e.OriginEdges)
                .WithOne(edge => edge.OriginRecord)
                .HasForeignKey(edge => edge.OriginRecordId)
                .HasConstraintName("extraction_edges_origin_record_id_fkey");

            entity.HasMany(e => e.DestinationEdges)
                .WithOne(edge => edge.DestinationRecord)
                .HasForeignKey(edge => edge.DestinationRecordId)
                .HasConstraintName("extraction_edges_destination_record_id_fkey");
        });

        modelBuilder.Entity<ExtractionRelationship>(entity =>
        {
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasMany(e => e.Edges)
                .WithOne(edge => edge.ExtractionRelationship)
                .HasForeignKey(edge => edge.ExtractionRelationshipId)
                .HasConstraintName("extraction_edges_extraction_relationship_id_fkey");
        });

        modelBuilder.Entity<ExtractionEdge>(entity =>
        {
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}
