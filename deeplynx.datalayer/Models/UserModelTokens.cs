using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace deeplynx.datalayer.Models;

public class UserModelTokens
{
    [Key] [Column("id")] public long Id { get; set; }
    
    [Column("model_config_id")] public long ConfigId { get; set; }
    
    [Column("token")] public long Token { get; set; }
}