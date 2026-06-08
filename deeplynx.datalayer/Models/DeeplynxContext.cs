using Microsoft.EntityFrameworkCore;

namespace deeplynx.datalayer.Models;

public partial class DeeplynxContext : DbContext
{
    public DeeplynxContext()
    {
    }

    public DeeplynxContext(DbContextOptions<DeeplynxContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Action> Actions { get; set; }

    public virtual DbSet<ApiKey> ApiKeys { get; set; }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<DataSource> DataSources { get; set; }

    public virtual DbSet<Edge> Edges { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<Extraction> Extractions { get; set; }

    public virtual DbSet<Group> Groups { get; set; }

    public virtual DbSet<HistoricalEdge> HistoricalEdges { get; set; }

    public virtual DbSet<HistoricalRecord> HistoricalRecords { get; set; }

    public virtual DbSet<OauthApplication> OauthApplications { get; set; }

    public virtual DbSet<OauthToken> OauthTokens { get; set; }

    public virtual DbSet<ObjectStorage> ObjectStorages { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<OrganizationUser> OrganizationUsers { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Project> Projects { get; set; }

    public virtual DbSet<ProjectMember> ProjectMembers { get; set; }
    
    public virtual DbSet<QueryRecord> QueryRecords { get; set; }

    public virtual DbSet<Record> Records { get; set; }

    public virtual DbSet<RecordCollection> RecordCollections { get; set; }

    public virtual DbSet<Relationship> Relationships { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SensitivityLabel> SensitivityLabels { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<SavedSearch> SavedSearches { get; set; }

    public virtual DbSet<AiModelConfig> AiModelConfigs { get; set; }

    public virtual DbSet<UserModelToken> UserModelTokens { get; set; }

    public virtual DbSet<Embedding> Embeddings { get; set; }

    public virtual DbSet<OntologyVector> OntologyVectors { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Action>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("actions_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_actions_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_actions_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_actions_project_id");

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_actions_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedActions)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Project).WithMany(p => p.Actions).HasConstraintName("actions_project_id_fkey");
            entity.HasOne(d => d.Organization).WithMany(o => o.Actions)
                .HasConstraintName("actions_organization_id_fkey");
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("api_keys_pkey");

            entity.HasIndex(e => e.ApplicationId)
                .HasDatabaseName("idx_api_keys_application_id");

            entity.HasOne(d => d.User).WithMany(p => p.ApiKeys)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("api_keys_user_id_fkey");

            entity.HasOne(d => d.OauthApplication).WithMany(p => p.ApiKeys)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("api_keys_application_id_fkey");
        });

        modelBuilder.Entity<Class>(entity =>
        {
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

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_classes_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedClasses)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Project)
                .WithMany(p => p.Classes)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("classes_project_id_fkey");

            entity.HasOne(d => d.Organization)
                .WithMany(o => o.Classes)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("classes_organization_id_fkey");

            entity.HasOne<Extraction>()
                .WithMany()
                .HasForeignKey(e => e.ExtractionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("classes_extraction_id_fkey");
        });

        modelBuilder.Entity<Extraction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("extractions_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);
        });

        modelBuilder.Entity<DataSource>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("data_sources_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_data_sources_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_data_sources_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_data_sources_project_id");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_data_sources_last_updated_by");

            entity.HasIndex(e => new { e.OrganizationId, e.Name })
                .HasDatabaseName("unique_organization_data_source_name")
                .IsUnique()
                .HasFilter("project_id IS NULL");

            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.Name })
                .HasDatabaseName("unique_project_data_source_name")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedDataSources)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Project)
                .WithMany(p => p.DataSources)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("data_sources_project_id_fkey");

            entity.HasOne(d => d.Organization)
                .WithMany(o => o.DataSources)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("data_sources_organization_id_fkey");
        });

        modelBuilder.Entity<Edge>(entity =>
        {
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

            // Composite unique index - ensures no duplicate edges in a project
            entity.HasIndex(e => new { e.ProjectId, e.OriginId, e.DestinationId })
                .HasDatabaseName("unique_edge_record_ids")
                .IsUnique();

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.ToTable(e => e.HasCheckConstraint(
                "CK_edges_origin_destination_different",
                "origin_id <> destination_id"));

            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_edges_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedEdges)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.DataSource).WithMany(p => p.Edges).HasConstraintName("edges_data_source_id_fkey");

            entity.HasOne(d => d.Destination).WithMany(p => p.EdgeDestinations)
                .HasConstraintName("edges_destination_id_fkey");

            entity.HasOne(d => d.Origin).WithMany(p => p.EdgeOrigins).HasConstraintName("edges_origin_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.Edges).HasConstraintName("edges_project_id_fkey");
            entity.HasOne(d => d.Organization).WithMany(o => o.Edges).HasConstraintName("edges_organization_id_fkey");

            entity.HasOne(d => d.Relationship).WithMany(p => p.Edges)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("edges_relationship_id_fkey");

            entity.HasOne<Extraction>()
                .WithMany()
                .HasForeignKey(e => e.ExtractionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("edges_extraction_id_fkey");
        });

        // Org-level entity - kinda \\
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("events_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_events_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_events_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_events_project_id");

            entity.HasIndex(e => e.DataSourceId)
                .HasDatabaseName("idx_events_data_source_id");

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_events_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedEvents)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            // Add cascade delete for Project
            entity.HasOne(d => d.Project)
                .WithMany(p => p.Events)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("events_project_id_fkey");

            // Add cascade delete for DataSource
            entity.HasOne(d => d.DataSource)
                .WithMany(p => p.Events)
                .HasForeignKey(d => d.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("events_data_source_id_fkey");

            // Add cascade delete for Organization
            entity.HasOne(d => d.Organization)
                .WithMany(p => p.Events)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("events_organization_id_fkey");
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("groups_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_groups_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_groups_organization_id");

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_groups_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedGroups)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Organization).WithMany(p => p.Groups).HasConstraintName("groups_organization_id_fkey");

            entity.HasMany(d => d.Users).WithMany(p => p.Groups)
                .UsingEntity<Dictionary<string, object>>(
                    "GroupUser",
                    r => r.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .HasConstraintName("group_users_group_id_fkey"),
                    l => l.HasOne<Group>().WithMany()
                        .HasForeignKey("GroupId")
                        .HasConstraintName("group_users_user_id_fkey"),
                    j =>
                    {
                        j.HasKey("GroupId", "UserId").HasName("group_users_pkey");
                        j.ToTable("group_users", "deeplynx");
                        j.HasIndex(new[] { "GroupId" }, "idx_group_users_group_id");
                        j.HasIndex(new[] { "UserId" }, "idx_group_users_user_id");
                        j.IndexerProperty<long>("GroupId").HasColumnName("group_id");
                        j.IndexerProperty<long>("UserId").HasColumnName("user_id");
                    });
        });

        modelBuilder.Entity<HistoricalEdge>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("historical_edges_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_historical_edges_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_historical_edges_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_historical_edges_project_id");

            entity.HasIndex(e => e.EdgeId)
                .HasDatabaseName("idx_historical_edges_edge_id");

            entity.HasIndex(e => e.OriginId)
                .HasDatabaseName("idx_historical_edges_origin_id");

            entity.HasIndex(e => e.DestinationId)
                .HasDatabaseName("idx_historical_edges_destination_id");

            entity.HasIndex(e => e.RelationshipName)
                .HasDatabaseName("idx_historical_edges_relationship_name");

            entity.HasIndex(e => e.LastUpdatedAt)
                .HasDatabaseName("idx_historical_edges_last_updated_at");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.DataSourceName).HasDefaultValueSql("''::text");
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_historical_edges_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedHistoricalEdges)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);
            entity.Property(e => e.ProjectName).HasDefaultValueSql("''::text");

            entity.HasOne(d => d.Edge).WithMany(p => p.HistoricalEdges)
                .HasConstraintName("historical_edges_edge_id_fkey");

            entity.HasOne(d => d.Project)
                .WithMany(p => p.HistoricalEdges)
                .HasConstraintName("historical_edges_project_id_fkey");

            entity.HasOne(d => d.Organization)
                .WithMany(o => o.HistoricalEdges)
                .HasConstraintName("historical_edges_organization_id_fkey");
        });

        modelBuilder.Entity<HistoricalRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("historical_records_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_historical_records_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_historical_records_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_historical_records_project_id");

            entity.HasIndex(e => e.RecordId)
                .HasDatabaseName("idx_historical_records_record_id");

            entity.HasIndex(e => e.ClassName)
                .HasDatabaseName("idx_historical_records_class_name");

            entity.HasIndex(e => e.LastUpdatedAt)
                .HasDatabaseName("idx_historical_records_last_updated_at");

            entity.HasIndex(e => e.Properties, "idx_historical_records_properties").HasMethod("gin");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasOne(d => d.Record).WithMany(p => p.HistoricalRecords)
                .HasConstraintName("historical_records_record_id_fkey");

            entity.HasOne(d => d.Organization)
                .WithMany(o => o.HistoricalRecords)
                .HasConstraintName("historical_records_organization_id_fkey");

            entity.HasOne(d => d.Project)
                .WithMany(p => p.HistoricalRecords)
                .HasConstraintName("historical_records_project_id_fkey");
        });

        modelBuilder.Entity<OauthApplication>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("oauth_applications_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_oauth_applications_id");

            entity.HasIndex(e => e.ClientId)
                .HasDatabaseName("idx_oauth_applications_client_id");

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_oauth_applications_last_updated_by");
            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.UpdatedOauthApplications)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);
        });

        modelBuilder.Entity<OauthToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("oauth_tokens_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_oauth_tokens_id");

            entity.HasIndex(e => e.ApplicationId)
                .HasDatabaseName("idx_oauth_tokens_application_id");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_oauth_tokens_user_id");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Revoked).HasDefaultValue(false);

            entity.HasOne(d => d.OauthApplication).WithMany(p => p.OauthTokens)
                .HasConstraintName("oauth_tokens_application_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.OauthTokens)
                .HasConstraintName("oauth_tokens_user_id_fkey");
        });

        // Org-level entity \\
        modelBuilder.Entity<ObjectStorage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("object_storage_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_object_storages_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_object_storages_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_object_storages_project_id");

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_object_storages_last_updated_by");

            // entity.ToTable( e => e.HasCheckConstraint(
            //     "ck_object_storages_ProjectXorOrg",
            //     "(project_id IS NOT NULL AND organization_id IS NULL) OR (project_id IS NULL AND organization_id IS NOT NULL)"));

            // Add filtered unique indexes
            entity.HasIndex(e => new { e.OrganizationId, e.Name })
                .HasDatabaseName("unique_organization_object_storage_name")
                .IsUnique()
                .HasFilter("project_id IS NULL");

            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.Name })
                .HasDatabaseName("unique_project_object_storage_name")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedObjectStorages)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            // Add cascade delete for Project
            entity.HasOne(d => d.Project)
                .WithMany(p => p.ObjectStorages)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("object_storage_project_id_fkey");

            // Add cascade delete for Organization
            entity.HasOne(d => d.Organization)
                .WithMany(p => p.ObjectStorages)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("object_storage_organization_id_fkey");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_organizations_id");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("unique_organization_name")
                .IsUnique();

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_organizations_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedOrganizations)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);
        });

        modelBuilder.Entity<OrganizationUser>(entity =>
        {
            // Composite PK
            entity.HasKey(e => new { e.OrganizationId, e.UserId }).HasName("organization_user_pkey");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_organization_users_organization_id");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_organization_users_user_id");

            // Think this may be redundant as it's enforced by PK
            entity.HasIndex(e => new { e.OrganizationId, e.UserId })
                .HasDatabaseName("unique_organization_user_ids")
                .IsUnique();

            entity.Property(e => e.IsOrgAdmin).HasDefaultValue(false);

            entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationUsers)
                .HasConstraintName("organization_users_organization_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.OrganizationUsers)
                .HasConstraintName("organization_users_user_id_fkey");
        });

        // Org-level entity \\
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("permission_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_permissions_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_permissions_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_permissions_project_id");

            entity.HasIndex(e => e.LabelId)
                .HasDatabaseName("idx_permissions_label_id");

            entity.HasIndex(e => e.Action)
                .HasDatabaseName("idx_permissions_action");

            entity.HasIndex(e => e.Resource)
                .HasDatabaseName("idx_permissions_resource");

            entity.HasIndex(e => e.IsDefault)
                .HasDatabaseName("idx_permissions_is_default");

            // Label-based permissions
            // Organization-level (no project)
            entity.HasIndex(e => new { e.OrganizationId, e.LabelId, e.Action })
                .HasDatabaseName("permissions_unique_org_label_action")
                .IsUnique()
                .HasFilter("project_id IS NULL");

            // Project-level (with project)
            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.LabelId, e.Action })
                .HasDatabaseName("permissions_unique_project_label_action")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            // Resource-based permissions
            // Organization-level (no project)
            entity.HasIndex(e => new { e.OrganizationId, e.Resource, e.Action })
                .HasDatabaseName("permissions_unique_org_resource_action")
                .IsUnique()
                .HasFilter("project_id IS NULL");

            // Project-level (with project)
            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.Resource, e.Action })
                .HasDatabaseName("permissions_unique_project_resource_action")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_permissions_last_updated_by");

            // Check constraint: if is_default is true, organization_id, project_id, and label_id must be null
            entity.ToTable(p => p.HasCheckConstraint(
                "chk_default_permissions_no_org_project_label",
                "is_default = false OR (organization_id IS NULL AND project_id IS NULL AND label_id IS NULL)"
            ));

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedPermissions)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Label).WithMany(p => p.Permissions)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("permissions_label_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.Permissions)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("permissions_project_id_fkey");

            entity.HasOne(d => d.Organization).WithMany(p => p.Permissions)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("permissions_organization_id_fkey");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("projects_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_projects_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_projects_organization_id");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_projects_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedProjects)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Organization).WithMany(p => p.Projects)
                .HasForeignKey(p => p.OrganizationId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("projects_organization_id_fkey");
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("project_members_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_project_members_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_project_members_project_id");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_project_members_user_id");

            entity.HasIndex(e => e.GroupId)
                .HasDatabaseName("idx_project_members_group_id");

            entity.HasIndex(e => e.RoleId)
                .HasDatabaseName("idx_project_members_role_id");

            // Composite unique index - prevents duplicate project memberships
            entity.HasIndex(e => new { e.ProjectId, e.GroupId, e.RoleId, e.UserId })
                .HasDatabaseName("unique_project_member_ids")
                .IsUnique();

            entity.HasOne(d => d.Group).WithMany(p => p.ProjectMembers)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("project_members_group_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectMembers)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("project_members_project_id_fkey");

            entity.HasOne(d => d.Role).WithMany(p => p.ProjectMembers)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("project_members_role_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.ProjectMembers)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("project_members_user_id_fkey");
        });

        modelBuilder.Entity<QueryRecord>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("query_records", "deeplynx");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Uri).HasColumnName("uri");
            entity.Property(e => e.Properties).HasColumnName("properties");
            entity.Property(e => e.OriginalId).HasColumnName("original_id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.ClassName).HasColumnName("class_name");
            entity.Property(e => e.DataSourceId).HasColumnName("data_source_id");
            entity.Property(e => e.DataSourceName).HasColumnName("data_source_name");
            entity.Property(e => e.ObjectStorageId).HasColumnName("object_storage_id");
            entity.Property(e => e.ObjectStorageName).HasColumnName("object_storage_name");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ProjectName).HasColumnName("project_name");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
            entity.Property(e => e.FileType).HasColumnName("file_type");
            entity.Property(e => e.FileSize).HasColumnName("file_size");
            entity.Property(e => e.Tags).HasColumnName("tags");
            entity.Property(e => e.Labels).HasColumnName("labels");
            entity.Property(e => e.LastUpdatedAt).HasColumnName("last_updated_at");
            entity.Property(e => e.LastUpdatedBy).HasColumnName("last_updated_by");
            entity.Property(e => e.IsArchived).HasColumnName("is_archived");
        });

        modelBuilder.Entity<Record>(entity =>
        {
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

            // Composite unique index - prevents duplicate original IDs within same project/data source
            entity.HasIndex(e => new { e.ProjectId, e.DataSourceId, e.OriginalId })
                .HasDatabaseName("unique_record_original_id")
                .IsUnique();

            entity.HasIndex(e => e.Properties, "idx_records_properties").HasMethod("gin");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_records_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedRecords)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Class).WithMany(p => p.Records)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("records_class_id_fkey");

            entity.HasOne(d => d.DataSource).WithMany(p => p.Records).HasConstraintName("records_data_source_id_fkey");

            entity.HasOne(d => d.ObjectStorage).WithMany(p => p.Records)
                .HasConstraintName("records_object_storage_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.Records).HasConstraintName("records_project_id_fkey");

            entity.HasOne(d => d.Organization).WithMany(p => p.Records)
                .HasConstraintName("records_organization_id_fkey");

            entity.HasOne<Extraction>()
                .WithMany()
                .HasForeignKey(e => e.ExtractionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("records_extraction_id_fkey");

            entity.HasMany(d => d.Labels).WithMany(p => p.Records)
                .UsingEntity<Dictionary<string, object>>(
                    "RecordLabel",
                    r => r.HasOne<SensitivityLabel>().WithMany()
                        .HasForeignKey("LabelId")
                        .HasConstraintName("record_labels_label_id_fkey"),
                    l => l.HasOne<Record>().WithMany()
                        .HasForeignKey("RecordId")
                        .HasConstraintName("record_labels_record_id_fkey"),
                    j =>
                    {
                        j.HasKey("RecordId", "LabelId").HasName("record_labels_pkey");
                        j.ToTable("record_labels", "deeplynx");
                        j.HasIndex(new[] { "LabelId" }, "idx_record_labels_label_id");
                        j.HasIndex(new[] { "RecordId" }, "idx_record_labels_record_id");
                        j.IndexerProperty<long>("RecordId").HasColumnName("record_id");
                        j.IndexerProperty<long>("LabelId").HasColumnName("label_id");
                    });

            entity.HasMany(d => d.Tags).WithMany(p => p.Records)
                .UsingEntity<Dictionary<string, object>>(
                    "RecordTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .HasConstraintName("record_tags_tag_id_fkey"),
                    l => l.HasOne<Record>().WithMany()
                        .HasForeignKey("RecordId")
                        .HasConstraintName("record_tags_record_id_fkey"),
                    j =>
                    {
                        j.HasKey("RecordId", "TagId").HasName("record_tags_pkey");
                        j.ToTable("record_tags", "deeplynx");
                        j.HasIndex(new[] { "RecordId" }, "idx_record_tags_record_id");
                        j.HasIndex(new[] { "TagId" }, "idx_record_tags_tag_id");
                        j.IndexerProperty<long>("RecordId").HasColumnName("record_id");
                        j.IndexerProperty<long>("TagId").HasColumnName("tag_id");
                    });
        });

        modelBuilder.Entity<RecordCollection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("record_collections_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_record_collections_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_record_collections_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_record_collections_project_id");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("idx_record_collections_name");

            // Creates a partial unique index to make sure if a user is adding an originalID, it is unique within the project
            entity.HasIndex(e => new { e.ProjectId, e.Name })
                .HasDatabaseName("unique_record_collection_name")
                .IsUnique();

            entity.HasIndex(e => e.Properties, "idx_record_collections_properties").HasMethod("gin");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_record_collections_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedRecordCollections)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Project).WithMany(p => p.RecordCollections).HasConstraintName("record_collections_project_id_fkey");

            entity.HasOne(d => d.Organization).WithMany(p => p.RecordCollections)
                .HasConstraintName("record_collections_organization_id_fkey");

            entity.HasMany(d => d.Labels).WithMany(p => p.RecordCollections)
                .UsingEntity<Dictionary<string, object>>(
                    "RecordCollectionLabels",
                    r => r.HasOne<SensitivityLabel>().WithMany()
                        .HasForeignKey("LabelId")
                        .HasConstraintName("record_collection_labels_label_id_fkey"),
                    l => l.HasOne<RecordCollection>().WithMany()
                        .HasForeignKey("RecordCollectionId")
                        .HasConstraintName("record_collection_labels_record_collection_id_fkey"),
                    j =>
                    {
                        j.HasKey("RecordCollectionId", "LabelId").HasName("record_collection_labels_pkey");
                        j.ToTable("record_collection_labels", "deeplynx");
                        j.HasIndex(new[] { "LabelId" }, "idx_record_collection_labels_label_id");
                        j.HasIndex(new[] { "RecordCollectionId" }, "idx_record_collection_labels_record_collection_id");
                        j.IndexerProperty<long>("RecordCollectionId").HasColumnName("record_collection_id");
                        j.IndexerProperty<long>("LabelId").HasColumnName("label_id");
                    });

            entity.HasMany(d => d.Tags).WithMany(p => p.RecordCollections)
                .UsingEntity<Dictionary<string, object>>(
                    "RecordCollectionTags",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .HasConstraintName("record_collection_tags_tag_id_fkey"),
                    l => l.HasOne<RecordCollection>().WithMany()
                        .HasForeignKey("RecordCollectionId")
                        .HasConstraintName("record_collection_tags_record_collection_id_fkey"),
                    j =>
                    {
                        j.HasKey("RecordCollectionId", "TagId").HasName("record_collection_tags_pkey");
                        j.ToTable("record_collection_tags", "deeplynx");
                        j.HasIndex(new[] { "RecordCollectionId" }, "idx_record_collection_tags_record_collection_id");
                        j.HasIndex(new[] { "TagId" }, "idx_record_collection_tags_tag_id");
                        j.IndexerProperty<long>("RecordCollectionId").HasColumnName("record_collection_id");
                        j.IndexerProperty<long>("TagId").HasColumnName("tag_id");
                    });

            entity.HasMany(d => d.Records).WithMany(p => p.RecordCollections)
                .UsingEntity<Dictionary<string, object>>(
                    "RecordCollectionRecords",
                    r => r.HasOne<Record>().WithMany()
                        .HasForeignKey("RecordId")
                        .HasConstraintName("record_collection_records_record_id_fkey"),
                    l => l.HasOne<RecordCollection>().WithMany()
                        .HasForeignKey("RecordCollectionId")
                        .HasConstraintName("record_collection_records_record_collection_id_fkey"),
                    j =>
                    {
                        j.HasKey("RecordCollectionId", "RecordId").HasName("record_collection_records_pkey");
                        j.ToTable("record_collection_records", "deeplynx");
                        j.HasIndex(new[] { "RecordCollectionId" }, "idx_record_collection_records_record_collection_id");
                        j.HasIndex(new[] { "RecordId" }, "idx_record_collection_records_record_id");
                        j.IndexerProperty<long>("RecordCollectionId").HasColumnName("record_collection_id");
                        j.IndexerProperty<long>("RecordId").HasColumnName("record_id");
                    });
        });

        modelBuilder.Entity<Relationship>(entity =>
        {
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

            entity.HasIndex(e => new { e.ProjectId, e.Name })
                .HasDatabaseName("unique_relationship_name")
                .IsUnique();

            // Composite unique index - relationship names are unique within an organization or project
            entity.HasIndex(e => new { e.OrganizationId, e.Name })
                .HasDatabaseName("unique_organization_relationship_name")
                .IsUnique()
                .HasFilter("project_id IS NULL");

            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.Name })
                .HasDatabaseName("unique_project_relationship_name")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.OriginId, e.Name, e.DestinationId })
                .HasDatabaseName("unique_project_relationship_origin_name_destination")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_relationships_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedRelationships)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Destination).WithMany(p => p.RelationshipDestinations)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("relationships_destination_id_fkey");

            entity.HasOne(d => d.Origin).WithMany(p => p.RelationshipOrigins)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("relationships_origin_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.Relationships)
                .HasConstraintName("relationships_project_id_fkey");

            entity.HasOne(d => d.Organization).WithMany(p => p.Relationships)
                .HasConstraintName("relationships_organization_id_fkey");

            entity.HasOne<Extraction>()
                .WithMany()
                .HasForeignKey(e => e.ExtractionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("relationships_extraction_id_fkey");
        });

        // Org-level entity \\
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_roles_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_roles_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_roles_project_id");

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_roles_last_updated_by");

            // Add filtered unique indexes
            entity.HasIndex(e => new { e.OrganizationId, e.Name })
                .HasDatabaseName("unique_organization_role_name")
                .IsUnique()
                .HasFilter("project_id IS NULL");

            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.Name })
                .HasDatabaseName("unique_project_role_name")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedRoles)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Organization).WithMany(p => p.Roles)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("roles_organization_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.Roles)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("roles_project_id_fkey");

            entity.HasMany(d => d.Permissions).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolePermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionId")
                        .HasConstraintName("role_permissions_permission_id_fkey"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("role_permissions_role_id_fkey"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId").HasName("role_permissions_pkey");
                        j.ToTable("role_permissions", "deeplynx");
                        j.HasIndex(new[] { "PermissionId" }, "idx_role_permissions_permission_id");
                        j.HasIndex(new[] { "RoleId" }, "idx_role_permissions_role_id");
                        j.IndexerProperty<long>("RoleId").HasColumnName("role_id");
                        j.IndexerProperty<long>("PermissionId").HasColumnName("permission_id");
                    });
        });

        // Org-level entity \\
        modelBuilder.Entity<SensitivityLabel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sensitivity_labels_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_sensitivity_labels_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_sensitivity_labels_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_sensitivity_labels_project_id");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("idx_sensitivity_labels_name");

            // Filtered unique indexes
            entity.HasIndex(e => new { e.OrganizationId, e.Name })
                .HasDatabaseName("unique_organization_sensitivity_label_name")
                .IsUnique()
                .HasFilter("project_id IS NULL");

            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.Name })
                .HasDatabaseName("unique_project_sensitivity_label_name")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_sensitivity_labels_last_updated_by");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedSensitivityLabels)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Organization).WithMany(p => p.SensitivityLabels)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("sensitivity_label_organization_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.SensitivityLabels)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("sensitivity_label_project_id_fkey");
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subscriptions_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_subscriptions_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_subscriptions_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_subscriptions_project_id");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_subscriptions_user_id");

            entity.HasIndex(e => e.ActionId)
                .HasDatabaseName("idx_subscriptions_action_id");

            entity.HasIndex(e => e.DataSourceId)
                .HasDatabaseName("idx_subscriptions_data_source_id");

            entity.HasIndex(e => e.EntityType)
                .HasDatabaseName("idx_subscriptions_entity_type");

            entity.HasIndex(e => e.LastUpdatedBy)
                .HasDatabaseName("idx_subscriptions_last_updated_by");

            // Composite unique index - prevents duplicate subscriptions
            entity.HasIndex(e => new
            { e.UserId, e.ActionId, e.Operation, e.ProjectId, e.DataSourceId, e.EntityType, e.EntityId })
                .HasDatabaseName("idx_unique_subscription")
                .IsUnique();

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedSubscriptions)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);

            entity.HasOne(d => d.Action).WithMany(p => p.Subscriptions)
                .HasConstraintName("subscriptions_action_id_fkey");

            entity.HasOne(d => d.DataSource).WithMany(p => p.Subscriptions)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("subscriptions_dataSource_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.Subscriptions)
                .HasConstraintName("subscriptions_project_id_fkey");

            entity.HasOne(d => d.Organization).WithMany(p => p.Subscriptions)
                .HasConstraintName("subscriptions_organization_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Subscriptions).HasConstraintName("subscriptions_user_id_fkey");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tags_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_tags_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_tags_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_tags_project_id");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("idx_tags_name");

            entity.HasIndex(e => e.LastUpdatedBy)
                .HasDatabaseName("idx_tags_last_updated_by");

            // Uniqueness when ProjectId is NULL: (OrganizationId, Name)
            entity.HasIndex(e => new { e.OrganizationId, e.Name })
                .HasDatabaseName("unique_organization_tag_name")
                .IsUnique()
                .HasFilter("project_id IS NULL");

            // Uniqueness when ProjectId is NOT NULL: (OrganizationId, ProjectId, Name)
            entity.HasIndex(e => new { e.OrganizationId, e.ProjectId, e.Name })
                .HasDatabaseName("unique_project_tag_name")
                .IsUnique()
                .HasFilter("project_id IS NOT NULL");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.Property(e => e.ProjectId).IsRequired(false);

            entity.HasIndex(e => e.LastUpdatedBy).HasDatabaseName("idx_tags_last_updated_by");

            // Organization relationship
            entity.HasOne(t => t.Organization)
                .WithMany(o => o.Tags)
                .HasForeignKey(t => t.OrganizationId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("tags_organization_id_fkey");

            // Project relationship
            entity.HasOne(t => t.Project)
                .WithMany(p => p.Tags)
                .HasForeignKey(t => t.ProjectId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("tags_project_id_fkey");

            entity.HasOne(d => d.LastUpdatedByUser)
                .WithMany(p => p.LastUpdatedTags)
                .HasForeignKey(d => d.LastUpdatedBy)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName(null);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_users_id");

            entity.HasIndex(e => e.Email)
                .HasDatabaseName("idx_users_email")
                .IsUnique();

            entity.HasIndex(e => e.SsoId)
                .HasDatabaseName("idx_users_sso_id");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.Property(e => e.IsSysAdmin).HasDefaultValue(false);

            entity.Property(e => e.IsActive).HasDefaultValue(false);
        });

        modelBuilder.Entity<SavedSearch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("saved_searches_pkey");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.User)
                .WithMany(p => p.SavedSearches)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("saved_searches_user_id_fkey");
        });

        modelBuilder.Entity<AiModelConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_model_configs_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_ai_model_configs_id");

            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("idx_ai_model_configs_organization_id");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("idx_ai_model_configs_project_id");

            entity.HasIndex(e => e.LastUpdatedBy)
                .HasDatabaseName("idx_ai_model_configs_last_updated_by");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsArchived).HasDefaultValue(false);

            entity.Property(e => e.RequiresToken).HasDefaultValue(false);

            entity.Property(e => e.Default).HasDefaultValue(false);

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.AiModelConfigs)
                .HasForeignKey(e => e.OrganizationId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ai_model_configs_organization_id_fkey");

            entity.HasOne(e => e.Project)
                .WithMany(p => p.AiModelConfigs)
                .HasForeignKey(e => e.ProjectId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ai_model_configs_project_id_fkey");

            entity.HasOne(e => e.LastUpdatedByUser)
                .WithMany(u => u.LastUpdatedAiModelConfigs)
                .HasForeignKey(e => e.LastUpdatedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName(null);
        });

        modelBuilder.Entity<UserModelToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_model_tokens_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_user_model_tokens_id");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_user_model_tokens_user_id");

            entity.HasIndex(e => e.AiModelConfigId)
                .HasDatabaseName("idx_user_model_tokens_ai_model_config_id");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();

            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserModelTokens)
                .HasForeignKey(e => e.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_model_tokens_user_id_fkey");

            entity.HasOne(e => e.AiModelConfig)
                .WithMany(a => a.UserModelTokens)
                .HasForeignKey(e => e.AiModelConfigId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_model_tokens_ai_model_config_id_fkey");
        });

        modelBuilder.Entity<Embedding>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("embeddings_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_embeddings_id");

            entity.HasIndex(e => e.RecordId)
                .HasDatabaseName("idx_embeddings_record_id");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Vector).HasColumnType("vector");

            entity.HasOne(d => d.Record)
                .WithMany(p => p.Embeddings)
                .HasForeignKey(d => d.RecordId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("embeddings_record_id_fkey");
        });

        modelBuilder.Entity<OntologyVector>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ontology_vectors_pkey");

            entity.HasIndex(e => e.Id)
                .HasDatabaseName("idx_ontology_vectors_id");

            entity.HasIndex(e => e.ClassId)
                .HasDatabaseName("idx_ontology_vectors_class_id");

            entity.HasIndex(e => e.RelationshipId)
                .HasDatabaseName("idx_ontology_vectors_relationship_id");

            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Vector).HasColumnType("vector");

            entity.HasOne(d => d.Class)
                .WithMany(p => p.OntologyVectors)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ontology_vectors_class_id_fkey");

            entity.HasOne(d => d.Relationship)
                .WithMany(p => p.OntologyVectors)
                .HasForeignKey(d => d.RelationshipId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ontology_vectors_relationship_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}