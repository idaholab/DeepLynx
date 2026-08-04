using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace deeplynx.models;

public class UpdateOrganizationRequestDto
{
    [MaxLength(50)] public string? Name { get; set; }

    [MaxLength(250)] public string? Description { get; set; }

    public bool? DefaultOrg { get; set; }

    public string? Banner { get; set; }

    public bool? RequireSensitivityLabel { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<OrganizationTheme>))]
    public OrganizationTheme? Theme { get; set; }
    public bool? CreateContainerPerProject { get; set; } = false;
}