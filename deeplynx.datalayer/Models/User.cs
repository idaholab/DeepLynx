using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("users", Schema = "deeplynx")]
public partial class User
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("email")]
    public string Email { get; set; } = null!;

    [Column("username")]
    public string? Username { get; set; } = null!;

    [Column("sso_id")]
    public string? SsoId { get; set; }

    [Column("last_login", TypeName = "timestamp without time zone")]
    public DateTime? LastLogin { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("password")]
    public string? Password { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    [Column("is_sys_admin")]
    public bool IsSysAdmin { get; set; }

    [Column("account_type")]
    public string AccountType { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<OrganizationUser> OrganizationUsers { get; set; } = new List<OrganizationUser>();

    [InverseProperty("User")]
    public virtual ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();

    [InverseProperty("User")]
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    [ForeignKey("UserId")]
    [InverseProperty("Users")]
    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();

    [InverseProperty("User")]
    public virtual ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<ApiKey> CreatedApiKeys { get; set; } = new List<ApiKey>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Class> LastUpdatedClasses { get; set; } = new List<Class>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<DataSource> LastUpdatedDataSources { get; set; } = new List<DataSource>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Edge> LastUpdatedEdges { get; set; } = new List<Edge>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<HistoricalEdge> LastUpdatedHistoricalEdges { get; set; } = new List<HistoricalEdge>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Event> LastUpdatedEvents { get; set; } = new List<Event>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Group> LastUpdatedGroups { get; set; } = new List<Group>();

    [InverseProperty("User")]
    public virtual ICollection<SavedSearch> SavedSearches { get; set; } = new List<SavedSearch>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<ObjectStorage> LastUpdatedObjectStorages { get; set; } = new List<ObjectStorage>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Organization> LastUpdatedOrganizations { get; set; } = new List<Organization>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Project> LastUpdatedProjects { get; set; } = new List<Project>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Record> LastUpdatedRecords { get; set; } = new List<Record>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<RecordCollection> LastUpdatedRecordCollections { get; set; } = new List<RecordCollection>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Permission> LastUpdatedPermissions { get; set; } = new List<Permission>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Relationship> LastUpdatedRelationships { get; set; } = new List<Relationship>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Role> LastUpdatedRoles { get; set; } = new List<Role>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<SensitivityLabel> LastUpdatedSensitivityLabels { get; set; } = new List<SensitivityLabel>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Tag> LastUpdatedTags { get; set; } = new List<Tag>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Subscription> LastUpdatedSubscriptions { get; set; } = new List<Subscription>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<Action> LastUpdatedActions { get; set; } = new List<Action>();

    [InverseProperty("User")]
    public virtual ICollection<OauthToken> OauthTokens { get; set; } = new List<OauthToken>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<OauthApplication> UpdatedOauthApplications { get; set; } = new List<OauthApplication>();

    [InverseProperty("LastUpdatedByUser")]
    public virtual ICollection<AiModelConfig> LastUpdatedAiModelConfigs { get; set; } = new List<AiModelConfig>();

    [InverseProperty("User")]
    public virtual ICollection<UserModelToken> UserModelTokens { get; set; } = new List<UserModelToken>();
}
