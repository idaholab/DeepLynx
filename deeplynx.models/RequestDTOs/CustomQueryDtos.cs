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
                public string? textSearch { get; set; }
                public CustomQueryRequestDto[] Filter { get; set; }
        }

        public class FilterSavedQueryRequestDto
        {
                public string? Name { get; set; } // savedSearches.Name column
                public string? TextSearch { get; set; } // search JSONB textSearch field 
                public DateTime? LastUpdatedBefore { get; set; } // start range to filter saved searches by last updated by field
                public DateTime? LastUpdatedAfter { get; set; } // end range to filer saved searches by the last updated by field
        }
}


