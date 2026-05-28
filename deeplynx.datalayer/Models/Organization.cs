using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

[Table("organizations", Schema = "deeplynx")]
public partial class Organization
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("last_updated_by")]
    public long? LastUpdatedBy { get; set; }

    [Column("last_updated_at", TypeName = "timestamp without time zone")]
    public DateTime LastUpdatedAt { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    [Column("default_org")]
    public bool DefaultOrg { get; set; } = false;
    
    [Column("banner")]
    [MaxLength(50)]
    public string? Banner { get; set; }
    
    [Column("require_sensitivity_label")]
    public bool RequireSensitivityLabel { get; set; } = false;

    [InverseProperty("Organization")]
    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();

    [InverseProperty("Organization")]
    public virtual ICollection<OrganizationUser> OrganizationUsers { get; set; } = new List<OrganizationUser>();

    [InverseProperty("Organization")]
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    [InverseProperty("Organization")]
    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<Action> Actions { get; set; } = new List<Action>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<DataSource> DataSources { get; set; } = new List<DataSource>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<Edge> Edges { get; set; } = new List<Edge>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<ObjectStorage> ObjectStorages { get; set; } = new List<ObjectStorage>();

    [InverseProperty("Organization")]
    public virtual ICollection<SensitivityLabel> SensitivityLabels { get; set; } = new List<SensitivityLabel>();

    [InverseProperty("Organization")]
    public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();

    [InverseProperty("LastUpdatedOrganizations")]
    public virtual User? LastUpdatedByUser { get; set; }
    
    [InverseProperty("Organization")]
    public virtual ICollection<Event> Events { get; set; } = new List<Event>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<HistoricalEdge> HistoricalEdges { get; set; } = new List<HistoricalEdge>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<HistoricalRecord> HistoricalRecords { get; set; } = new List<HistoricalRecord>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<Record> Records { get; set; } = new List<Record>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<RecordCollection> RecordCollections { get; set; } = new List<RecordCollection>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<Relationship> Relationships { get; set; } = new List<Relationship>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
    
    [InverseProperty("Organization")]
    public virtual ICollection<AiModelConfig> AiModelConfigs { get; set; } = new List<AiModelConfig>();
}
