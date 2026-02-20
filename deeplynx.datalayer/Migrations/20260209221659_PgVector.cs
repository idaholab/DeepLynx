using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class PgVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable pgvector extension
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            // Create schema
            migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS dl_vector;");

            // Create embeddings table with raw SQL
            migrationBuilder.Sql(@"
                CREATE TABLE dl_vector.embeddings (
                    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    record_id BIGINT NOT NULL,
                    page_number INTEGER NOT NULL,
                    text_chunk TEXT NOT NULL,
                    vector vector(1024) NOT NULL,
                    last_updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT embeddings_record_id_fkey 
                        FOREIGN KEY (record_id) 
                        REFERENCES deeplynx.records(id) 
                        ON DELETE CASCADE
                );
            ");

            // Create indexes
            migrationBuilder.Sql(@"
                CREATE INDEX idx_embeddings_id 
                ON dl_vector.embeddings(id);
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX idx_embeddings_record_id 
                ON dl_vector.embeddings(record_id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS dl_vector.embeddings CASCADE;");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS dl_vector CASCADE;");
        }
    }
}