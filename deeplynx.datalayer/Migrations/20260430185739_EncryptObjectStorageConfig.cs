using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace deeplynx.datalayer.Migrations
{
    /// <inheritdoc />
    public partial class EncryptObjectStorageConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "config",
                schema: "deeplynx",
                table: "object_storages",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AddColumn<string>(
                name: "config_encrypted",
                schema: "deeplynx",
                table: "object_storages",
                type: "text",
                nullable: true);
            
            var encryptionKey = Environment.GetEnvironmentVariable("ENCRYPTION_KEY")
                ?? throw new InvalidOperationException("ENCRYPTION_KEY environment variable is not set");
            var encryptionIv = Environment.GetEnvironmentVariable("ENCRYPTION_IV")
                ?? throw new InvalidOperationException("ENCRYPTION_IV environment variable is not set");

            // encrypt existing config data
            migrationBuilder.Sql($@"
                DO $$
                DECLARE
                    encryption_key bytea := decode('{encryptionKey}', 'base64');
                    encryption_iv bytea := decode('{encryptionIv}', 'base64');
                    rec RECORD;
                    encrypted_text text;
                BEGIN
					FOR rec IN
						SELECT id, to_json(config)::text as config_text
						FROM deeplynx.object_storages
						WHERE config IS NOT NULL
					LOOP
						encrypted_text := encode(
							encrypt_iv(
								convert_to(rec.config_text, 'UTF8'),
								encryption_key,
								encryption_iv,
								'aes-cbc/pad:pkcs'
							), 'base64'
						);
					
						UPDATE deeplynx.object_storages
						SET config_encrypted = encrypted_text, config = NULL
						WHERE id = rec.id;
					END LOOP;
				END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var encryptionKey = Environment.GetEnvironmentVariable("ENCRYPTION_KEY")
                ?? throw new InvalidOperationException("ENCRYPTION_KEY environment variable is not set");
            var encryptionIv = Environment.GetEnvironmentVariable("ENCRYPTION_IV")
                ?? throw new InvalidOperationException("ENCRYPTION_IV environment variable is not set");
            
            // Decrypt data back to config column
            migrationBuilder.Sql($@"
                DO $$
                DECLARE
                    encryption_key bytea := decode('{encryptionKey}', 'base64');
                    encryption_iv bytea := decode('{encryptionIv}', 'base64');
	                rec RECORD;
                BEGIN
					FOR rec IN
						SELECT id, config_encrypted
						FROM deeplynx.object_storages
						WHERE config_encrypted IS NOT NULL
					LOOP         
						decrypted_text := convert_from(
							decrypt_iv(
								decode(rec.config_encrypted, 'base64'),
								encryption_key,
								encryption_iv,
								'aes-cbc/pad:pkcs'
							), 'UTF8'
						);
						
						UPDATE deeplynx.object_storages
						SET config = decrypted_text::jsonb, config_encrypted = NULL
						WHERE id = rec.id;
					END LOOP;
				END $$;
            ");
            
            migrationBuilder.DropColumn(
                name: "config_encrypted",
                schema: "deeplynx",
                table: "object_storages");

            migrationBuilder.AlterColumn<string>(
                name: "config",
                schema: "deeplynx",
                table: "object_storages",
                type: "jsonb",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);
        }
    }
}
