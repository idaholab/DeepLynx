using Microsoft.EntityFrameworkCore;

namespace deeplynx.datalayer.Models;

public class StagingContext : DbContext
{
    public StagingContext()
    {
    }

    public StagingContext(DbContextOptions<StagingContext> options) : base(options)
    {
    }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<Edge> Edges { get; set; }

    public virtual DbSet<Record> Records { get; set; }

    public virtual DbSet<Relationship> Relationships { get; set; }

    public virtual DbSet<CrossSchemaEdge> CrossSchemaEdges { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("staging");

        // Ignore all entities not part of the staging schema to prevent transitive discovery issues
        modelBuilder.Ignore<deeplynx.datalayer.Models.Action>();
        modelBuilder.Ignore<AiModelConfig>();
        modelBuilder.Ignore<ApiKey>();
        modelBuilder.Ignore<DataSource>();
        modelBuilder.Ignore<Embedding>();
        modelBuilder.Ignore<OntologyVector>();
        modelBuilder.Ignore<Event>();
        modelBuilder.Ignore<Group>();
        modelBuilder.Ignore<HistoricalEdge>();
        modelBuilder.Ignore<HistoricalRecord>();
        modelBuilder.Ignore<OauthApplication>();
        modelBuilder.Ignore<OauthToken>();
        modelBuilder.Ignore<ObjectStorage>();
        modelBuilder.Ignore<Organization>();
        modelBuilder.Ignore<OrganizationUser>();
        modelBuilder.Ignore<Permission>();
        modelBuilder.Ignore<Project>();
        modelBuilder.Ignore<ProjectMember>();
        modelBuilder.Ignore<Role>();
        modelBuilder.Ignore<SavedSearch>();
        modelBuilder.Ignore<SensitivityLabel>();
        modelBuilder.Ignore<Subscription>();
        modelBuilder.Ignore<Tag>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<UserModelToken>();

        // Extraction lives in the deeplynx schema - referenced for FK purposes only, excluded from staging migrations
        modelBuilder.Entity<Extraction>(entity =>
        {
            entity.ToTable("extractions", "deeplynx", t => t.ExcludeFromMigrations());
            // Ignoring CreatedByUser prevents transitive discovery of User → Group/Permission many-to-many shadow join entities
            entity.Ignore(e => e.CreatedByUser);
        });

        // EF Core conventions create shadow join entities from data annotations on deeplynx entities before OnModelCreating runs.
        // Ignoring the navigation properties removes the skip navigations but leaves the shadow entities orphaned in the model.
        // Exclude them from migrations so they remain in the model snapshot without generating DDL.
        modelBuilder.Entity("RecordSensitivityLabel")
            .ToTable("RecordSensitivityLabel", "staging", t => t.ExcludeFromMigrations());
        modelBuilder.Entity("RecordTag")
            .ToTable("RecordTag", "staging", t => t.ExcludeFromMigrations());
        modelBuilder.Entity("GroupUser")
            .ToTable("GroupUser", "staging", t => t.ExcludeFromMigrations());
        modelBuilder.Entity("PermissionRole")
            .ToTable("PermissionRole", "staging", t => t.ExcludeFromMigrations());

        modelBuilder.Entity<Class>(entity =>
        {
            entity.ToTable("classes", "staging");

            entity.HasKey(e => e.Id).HasName("classes_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_classes_id");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("idx_classes_name");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_classes_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_classes_project_id");

            entity.HasIndex(e => e.Uuid)
                .HasDatabaseName("idx_classes_uuid");

            entity.HasIndex(e => e.ExtractionId)
                .HasDatabaseName("idx_classes_extraction_id");

            entity.HasIndex(e => new { e.ProjectId, e.Name })
                .HasDatabaseName("unique_class_name")
                .IsUnique();

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => new { e.OrganizationId, e.Name })
                .HasDatabaseName("unique_organization_class_name")
                .IsUnique()
                .HasFilter("project_id IS NULL");

            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.Name })
                .HasDatabaseName("unique_project_class_name")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            entity.HasOne<Extraction>()
                .WithMany()
                .HasForeignKey(e => e.ExtractionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("classes_extraction_id_fkey");

            // Ignore non-staging navigations to prevent transitive entity discovery
            entity.Ignore(e => e.Project);
            entity.Ignore(e => e.Organization);
            entity.Ignore(e => e.LastUpdatedByUser);
        });

        modelBuilder.Entity<Edge>(entity =>
        {
            entity.ToTable("edges", "staging");

            entity.HasKey(e => e.Id).HasName("edges_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_edges_id");

            entity.HasIndex(e => e.DataSourceId)
                .HasDatabaseName("idx_edges_data_source_id");

            entity.HasIndex(e => e.DestinationId)
                .HasDatabaseName("idx_edges_destination_id");

            entity.HasIndex(e => e.OriginId)
                .HasDatabaseName("idx_edges_origin_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_edges_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_edges_project_id");

            entity.HasIndex(e => e.RelationshipId)
                .HasDatabaseName("idx_edges_relationship_id");

            entity.HasIndex(e => e.ExtractionId)
                .HasDatabaseName("idx_edges_extraction_id");

            entity.HasIndex(e => new { e.ProjectId, e.OriginId, e.DestinationId })
                .HasDatabaseName("unique_edge_record_ids")
                .IsUnique();

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.ToTable(e => e.HasCheckConstraint(
                "CK_edges_origin_destination_different",
                "origin_id <> destination_id"));

            // Intra-staging navigations
            entity.HasOne(d => d.Destination).WithMany(p => p.EdgeDestinations)
                .HasConstraintName("edges_destination_id_fkey");

            entity.HasOne(d => d.Origin).WithMany(p => p.EdgeOrigins)
                .HasConstraintName("edges_origin_id_fkey");

            entity.HasOne(d => d.Relationship).WithMany(p => p.Edges)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("edges_relationship_id_fkey");

            entity.HasOne<Extraction>()
                .WithMany()
                .HasForeignKey(e => e.ExtractionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("edges_extraction_id_fkey");

            // Ignore non-staging navigations to prevent transitive entity discovery
            entity.Ignore(e => e.DataSource);
            entity.Ignore(e => e.HistoricalEdges);
            entity.Ignore(e => e.Project);
            entity.Ignore(e => e.Organization);
            entity.Ignore(e => e.LastUpdatedByUser);
        });

        modelBuilder.Entity<CrossSchemaEdge>(entity =>
        {
            entity.ToTable("cross_schema_edges", "staging");

            entity.HasKey(e => e.Id).HasName("cross_schema_edges_pkey");

            entity.HasIndex(e => e.ExtractionId)
                .HasDatabaseName("idx_cross_schema_edges_extraction_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_cross_schema_edges_project_id");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne<Extraction>()
                .WithMany()
                .HasForeignKey(e => e.ExtractionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("cross_schema_edges_extraction_id_fkey");
        });

        modelBuilder.Entity<Record>(entity =>
        {
            entity.ToTable("records", "staging");

            entity.HasKey(e => e.Id).HasName("records_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_records_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_records_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_records_project_id");

            entity.HasIndex(e => e.ClassId)
                .HasDatabaseName("idx_records_class_id");

            entity.HasIndex(e => e.DataSourceId)
                .HasDatabaseName("idx_records_data_source_id");

            entity.HasIndex(e => e.ObjectStorageId)
                .HasDatabaseName("idx_records_object_storage_id");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("idx_records_name");

            entity.HasIndex(e => e.OriginalId)
                .HasDatabaseName("idx_records_original_id");

            entity.HasIndex(e => e.ExtractionId)
                .HasDatabaseName("idx_records_extraction_id");

            entity.HasIndex(e => new { e.ProjectId, e.DataSourceId, e.OriginalId })
                .HasDatabaseName("unique_record_original_id")
                .IsUnique();

            entity.HasIndex(e => e.Properties, "idx_records_properties").HasMethod("gin");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            // Intra-staging navigation
            entity.HasOne(d => d.Class).WithMany(p => p.Records)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("records_class_id_fkey");

            entity.HasOne<Extraction>()
                .WithMany()
                .HasForeignKey(e => e.ExtractionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("records_extraction_id_fkey");

            // Shadow property: name of a deeplynx class to resolve at promotion when ClassId is null
            entity.Property<string?>("ClassName").HasColumnName("class_name");

            // Ignore non-staging navigations to prevent transitive entity discovery
            entity.Ignore(e => e.DataSource);
            entity.Ignore(e => e.HistoricalRecords);
            entity.Ignore(e => e.ObjectStorage);
            entity.Ignore(e => e.Project);
            entity.Ignore(e => e.Organization);
            entity.Ignore(e => e.Labels);
            entity.Ignore(e => e.Tags);
            entity.Ignore(e => e.LastUpdatedByUser);
        });

        modelBuilder.Entity<Relationship>(entity =>
        {
            entity.ToTable("relationships", "staging");

            entity.HasKey(e => e.Id).HasName("relationships_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_relationships_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_relationships_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_relationships_project_id");

            entity.HasIndex(e => e.OriginId)
                .HasDatabaseName("idx_relationships_origin_id");

            entity.HasIndex(e => e.DestinationId)
                .HasDatabaseName("idx_relationships_destination_id");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("idx_relationships_name");

            entity.HasIndex(e => e.Uuid)
                .HasDatabaseName("idx_relationships_uuid");

            entity.HasIndex(e => e.ExtractionId)
                .HasDatabaseName("idx_relationships_extraction_id");

            entity.HasIndex(e => new { e.ProjectId, e.Name })
                .HasDatabaseName("unique_relationship_name")
                .IsUnique();

            entity.HasIndex(e => new { e.OrganizationId, e.Name })
                .HasDatabaseName("unique_organization_relationship_name")
                .IsUnique()
                .HasFilter("project_id IS NULL");

            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.Name })
                .HasDatabaseName("unique_project_relationship_name")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            // Intra-staging navigations
            entity.HasOne(d => d.Destination).WithMany(p => p.RelationshipDestinations)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("relationships_destination_id_fkey");

            entity.HasOne(d => d.Origin).WithMany(p => p.RelationshipOrigins)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("relationships_origin_id_fkey");

            entity.HasOne<Extraction>()
                .WithMany()
                .HasForeignKey(e => e.ExtractionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("relationships_extraction_id_fkey");

            // Shadow properties: class names to resolve at promotion when OriginId/DestinationId are null
            entity.Property<string?>("OriginName").HasColumnName("origin_name");
            entity.Property<string?>("DestinationName").HasColumnName("destination_name");

            // Ignore non-staging navigations to prevent transitive entity discovery
            entity.Ignore(e => e.Project);
            entity.Ignore(e => e.Organization);
            entity.Ignore(e => e.LastUpdatedByUser);
        });
    }
}
