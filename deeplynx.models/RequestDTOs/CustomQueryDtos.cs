namespace deeplynx.models;

public class CustomQueryDtos
{
        public class CustomQueryRequestDto
        {
                public string? Connector { get; set; } // AND, OR
                public string Filter { get; set; } // properties from historical records model
                public string Operator { get; set; } // =, <, >, LIKE, KEY_VALUE
                public string? Value { get; set; } // One selected option from listed values of Filters 

                public string? Json { get; set; }
        }


        public class CustomQueryResponseDto
        {
                public string? TextSearch { get; set; }
                public CustomQueryRequestDto[] Filter { get; set; }
        }

        public class FilterSavedQueryRequestDto
        {
                public string? Name { get; set; }
                public string? TextSearch { get; set; }
                public DateTime? LastUpdatedBefore { get; set; }
                public DateTime? LastUpdatedAfter { get; set; }
        }

        public class SavedSearchDto
        {
                public CustomQueryRequestDto[] Filter { get; set; }
                public string? TextSearch { get; set; }
        }
}


