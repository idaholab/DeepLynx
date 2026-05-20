using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace deeplynx.helpers;

public class EncryptionHelper
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public EncryptionHelper()
    {
        // Validate encryption keys at startup with CheckEncryptionConfig
        CheckEncryptionConfig();
        
        // Load encryption key from environment variables if everything checks out
        _key = Convert.FromBase64String(Environment.GetEnvironmentVariable("ENCRYPTION_KEY"));
        _iv = Convert.FromBase64String(Environment.GetEnvironmentVariable("ENCRYPTION_IV"));
    }

    /// <summary>
    /// Checks if the app is configured with an encryption key/secret
    /// </summary>
    public static void CheckEncryptionConfig()
    {
        try
        {
            // Load encryption key from environment variables
            var keyBase64 = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
            var ivBase64 = Environment.GetEnvironmentVariable("ENCRYPTION_IV");

            // Check if variables are missing
            if (string.IsNullOrWhiteSpace(keyBase64) || string.IsNullOrWhiteSpace(ivBase64))
            {
                var (newKey, newIV) = GenerateKeyAndIV();

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n=====================================");
                Console.WriteLine("❌ ENCRYPTION CONFIGURATION NOT FOUND");
                Console.WriteLine("=====================================\n");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("ACTION REQUIRED:");
                Console.ResetColor();
                Console.WriteLine("  1. Follow the instructions in the README to create valid encryption keys");
                Console.WriteLine("  2. Add them to your environment variables or .env file");
                Console.WriteLine("  3. Restart the application\n");
                Console.ResetColor();

                Console.WriteLine("==========================================================\n");

                throw new InvalidOperationException(
                    "ENCRYPTION_KEY and/or ENCRYPTION_IV environment variables are not set.");
            }
            
            // Validate that they're valid base64
            byte[] key, iv;
            try
            {
                key = Convert.FromBase64String(keyBase64);
                iv = Convert.FromBase64String(ivBase64);
            }
            catch (FormatException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n=====================================");
                Console.WriteLine("❌ INVALID ENCRYPTION CONFIGURATION");
                Console.WriteLine("=====================================\n");
                Console.ResetColor();

                Console.WriteLine("ENCRYPTION_KEY or ENCRYPTION_IV is not a valid base64 string.\n");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("ACTION REQUIRED:");
                Console.ResetColor();
                Console.WriteLine("  1. Follow the instructions in the README to create valid encryption keys");
                Console.WriteLine("  2. Add them to your environment variables or .env file");
                Console.WriteLine("  3. Restart the application\n");

                Console.WriteLine("==========================================================\n");

                throw new InvalidOperationException(
                    "ENCRYPTION_KEY or ENCRYPTION_IV is not a valid base64 string.");
            }
            
            // Validate key and IV sizes
            if (key.Length != 32)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n=====================================");
                Console.WriteLine("❌ INVALID ENCRYPTION KEY SIZE");
                Console.WriteLine("=====================================\n");
                Console.ResetColor();

                Console.WriteLine($"ENCRYPTION_KEY must be 32 bytes (256 bits) for AES-256.");
                Console.WriteLine($"Current length: {key.Length} bytes\n");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("ACTION REQUIRED:");
                Console.ResetColor();
                Console.WriteLine("  1. Follow the instructions in the README to create valid encryption keys");
                Console.WriteLine("  2. Add them to your environment variables or .env file");
                Console.WriteLine("  3. Restart the application\n");

                Console.WriteLine("==========================================================\n");

                throw new InvalidOperationException(
                    $"ENCRYPTION_KEY must be 32 bytes (256 bits) for AES-256. Current length: {key.Length} bytes");
            }
            
            if (iv.Length != 16)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n=====================================");
                Console.WriteLine("❌ INVALID ENCRYPTION IV SIZE");
                Console.WriteLine("=====================================\n");
                Console.ResetColor();

                Console.WriteLine($"ENCRYPTION_IV must be 16 bytes (128 bits).");
                Console.WriteLine($"Current length: {iv.Length} bytes\n");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("ACTION REQUIRED:");
                Console.ResetColor();
                Console.WriteLine("  1. Follow the instructions in the README to create valid encryption keys");
                Console.WriteLine("  2. Add them to your environment variables or .env file");
                Console.WriteLine("  3. Restart the application\n");

                Console.WriteLine("==========================================================\n");

                throw new InvalidOperationException(
                    $"ENCRYPTION_IV must be 16 bytes (128 bits). Current length: {iv.Length} bytes");
            }
            
            // All checks passed
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Encryption configuration validated");
            Console.ResetColor();
        }
        catch (InvalidOperationException)
        {
            throw;
        } catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Unexpected error during encryption verification: {ex.Message}");
            Console.ResetColor();
            throw;
        }
    }

    /// <summary>
    /// Encrypts a plaintext string using AES-256-CBC
    /// </summary>
    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
        
        return Convert.ToBase64String(ciphertextBytes);
    }

    /// <summary>
    /// Decrypts a ciphertext string using AES-256-CBC
    /// </summary>
    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            throw new ArgumentException("Ciphertext cannot be null or empty", nameof(ciphertext));

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var ciphertextBytes = Convert.FromBase64String(ciphertext);
            var plaintextBytes = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);
            
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Failed to decrypt configuration. The data may be corrupted or the encryption key may have changed.", ex);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Invalid ciphertext format. Expected base64-encoded string.", ex);
        }
    }
    
    /// <summary>
    ///     Serialize a DTO or other json object and encrypt it
    /// </summary>
    /// <param name="data">The contents of the object</param>
    /// <typeparam name="T">The DTO or other type of the object</typeparam>
    /// <returns>Ciphertext based on the input</returns>
    public string SerializeAndEncrypt<T>(T data)
    {
        if (data == null)
            throw new ArgumentException("Data cannot be null", nameof(data));
    
        var options = new JsonSerializerOptions()
        {
            WriteIndented = false
        };
    
        var json = JsonSerializer.Serialize(data, options);
    
        // explicitly add spaces after the colon to match the Postgres formatting for consistent encryption output
        json = System.Text.RegularExpressions.Regex.Replace(json, "\":(?! )", "\": ");
    
        return Encrypt(json);
    }

    /// <summary>
    ///     Deserialize ciphertext into the specified DTO
    /// </summary>
    /// <param name="encryptedData">Ciphertext to be decrypted and deserialized</param>
    /// <typeparam name="T">DTO or other data format to deserialize the string into</typeparam>
    /// <returns>Deserialized and decrypted data in the requested format</returns>
    /// <exception cref="InvalidOperationException">Error if deserialized json does not match DTO</exception>
    public T DeserializeAndDecrypt<T>(string encryptedData)
    {
        try
        {
            var decrypted = Decrypt(encryptedData);
            return JsonSerializer.Deserialize<T>(decrypted)
                   ?? throw new InvalidOperationException(
                       $"Decrypted data for type {typeof(T).Name} is null or invalid");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Decrypted data for type {typeof(T).Name} is null or invalid", ex);
        }
    }

    /// <summary>
    /// Generates a new random encryption key and IV for AES-256
    /// Use this during initial setup to generate values for your environment variables
    /// </summary>
    public static (string Key, string IV) GenerateKeyAndIV()
    {
        using var aes = Aes.Create();
        aes.KeySize = 256; // AES-256
        aes.GenerateKey();
        aes.GenerateIV();

        return (
            Key: Convert.ToBase64String(aes.Key),
            IV: Convert.ToBase64String(aes.IV)
        );
    }
}