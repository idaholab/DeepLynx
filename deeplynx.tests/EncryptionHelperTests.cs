using System.Security.Cryptography;
using System.Text;
using deeplynx.helpers;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class EncryptionHelperTests : IntegrationTestBase
{
    public EncryptionHelperTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    #region Test Setup and Helpers

    /// <summary>
    /// Sets environment variables for testing
    /// </summary>
    private void SetEncryptionEnvironmentVariables(string key, string iv)
    {
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", key);
        Environment.SetEnvironmentVariable("ENCRYPTION_IV", iv);
    }

    /// <summary>
    /// Clears encryption environment variables
    /// </summary>
    private void ClearEncryptionEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", null);
        Environment.SetEnvironmentVariable("ENCRYPTION_IV", null);
    }

    /// <summary>
    /// Generates valid test encryption keys
    /// </summary>
    private (string Key, string IV) GenerateValidKeys()
    {
        return EncryptionHelper.GenerateKeyAndIV();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidKeys_CreatesInstance()
    {
        // Arrange
        var (key, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            // Act
            var helper = new EncryptionHelper();

            // Assert
            Assert.NotNull(helper);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }
    
    [Fact]
    public void Constructor_WithPredictableKeys_CreatesInstance()
    {
        // Arrange - Using the development keys from the env-sample
        var devKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("INSECURE_DEV_KEY_32_BYTES_LONG!!")); // exactly 32 bytes
        var devIv = Convert.ToBase64String(Encoding.UTF8.GetBytes("INSECURE_DEV_IV!")); // exactly 16 bytes
        SetEncryptionEnvironmentVariables(devKey, devIv);

        try
        {
            // Act
            var helper = new EncryptionHelper();

            // Assert
            Assert.NotNull(helper);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Constructor_WithMissingKey_ThrowsException()
    {
        // Arrange
        var (_, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(null!, iv);

        try
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => new EncryptionHelper());
            Assert.Contains("ENCRYPTION_KEY", exception.Message);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Constructor_WithMissingIV_ThrowsException()
    {
        // Arrange
        var (key, _) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, null!);

        try
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => new EncryptionHelper());
            Assert.Contains("ENCRYPTION_IV", exception.Message);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Constructor_WithEmptyKey_ThrowsException()
    {
        // Arrange
        var (_, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables("", iv);

        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new EncryptionHelper());
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Constructor_WithInvalidBase64Key_ThrowsException()
    {
        // Arrange
        var (_, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables("not-valid-base64!@#$", iv);

        try
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => new EncryptionHelper());
            Assert.Contains("not a valid base64 string", exception.Message);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }
    
    [Fact]
    public void Constructor_WithInvalidKeyLength_ThrowsException()
    {
        // Arrange - Create a 16-byte key (too small, needs 32 bytes)
        var invalidKey = Convert.ToBase64String(new byte[16]);
        var (_, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(invalidKey, iv);

        try
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => new EncryptionHelper());
            Assert.Contains("32 bytes", exception.Message);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Constructor_WithInvalidIVLength_ThrowsException()
    {
        // Arrange - Create an 8-byte IV (too small, needs 16 bytes)
        var (key, _) = GenerateValidKeys();
        var invalidIv = Convert.ToBase64String(new byte[8]);
        SetEncryptionEnvironmentVariables(key, invalidIv);

        try
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => new EncryptionHelper());
            Assert.Contains("16 bytes", exception.Message);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    #endregion

    #region Encryption/Decryption Tests with Valid Keys

    [Fact]
    public void EncryptDecrypt_WithValidKeys_RoundTripsSuccessfully()
    {
        // Arrange
        var (key, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            var helper = new EncryptionHelper();
            var plaintext = "Hello, World!";

            // Act
            var ciphertext = helper.Encrypt(plaintext);
            var decrypted = helper.Decrypt(ciphertext);

            // Assert
            Assert.NotEqual(plaintext, ciphertext); // Encrypted text should be different
            Assert.Equal(plaintext, decrypted); // Decrypted should match original
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Encrypt_SameInputMultipleTimes_ProducesSameCiphertext()
    {
        // Arrange
        var (key, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            var helper = new EncryptionHelper();
            var plaintext = "Consistent encryption test";

            // Act
            var ciphertext1 = helper.Encrypt(plaintext);
            var ciphertext2 = helper.Encrypt(plaintext);
            var ciphertext3 = helper.Encrypt(plaintext);

            // Assert - Should produce same ciphertext (because we use fixed IV)
            Assert.Equal(ciphertext1, ciphertext2);
            Assert.Equal(ciphertext2, ciphertext3);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    #endregion

    #region Encryption/Decryption Tests with "Insecure" Development Keys

    [Fact]
    public void EncryptDecrypt_WithInsecureDevelopmentKeys_RoundTripsSuccessfully()
    {
        // Arrange - Using the development keys from the env-sample
        var devKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("INSECURE_DEV_KEY_32_BYTES_LONG!!")); // exactly 32 bytes
        var devIv = Convert.ToBase64String(Encoding.UTF8.GetBytes("INSECURE_DEV_IV!")); // exactly 16 bytes
        
        SetEncryptionEnvironmentVariables(devKey, devIv);

        try
        {
            var helper = new EncryptionHelper();
            var plaintext = "Test with development keys";

            // Act
            var ciphertext = helper.Encrypt(plaintext);
            var decrypted = helper.Decrypt(ciphertext);

            // Assert
            Assert.NotEqual(plaintext, ciphertext);
            Assert.Equal(plaintext, decrypted);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void EncryptDecrypt_WithProperlyFormattedInsecureKeys_WorksCorrectly()
    {
        // Arrange - Create properly formatted but "insecure" keys that match our naming convention
        // but have correct byte lengths (32 bytes for key, 16 bytes for IV)
        var insecureKeyBytes = new byte[32];
        var insecureIvBytes = new byte[16];
        
        // Fill with predictable pattern (insecure but valid)
        for (int i = 0; i < 32; i++)
        {
            insecureKeyBytes[i] = (byte)(i % 256);
        }
        for (int i = 0; i < 16; i++)
        {
            insecureIvBytes[i] = (byte)(i % 256);
        }
        
        var key = Convert.ToBase64String(insecureKeyBytes);
        var iv = Convert.ToBase64String(insecureIvBytes);
        
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            var helper = new EncryptionHelper();
            var plaintext = "Testing with predictable but valid insecure keys";

            // Act
            var ciphertext = helper.Encrypt(plaintext);
            var decrypted = helper.Decrypt(ciphertext);

            // Assert
            Assert.Equal(plaintext, decrypted);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    #endregion

    #region Tests with Invalid/Mismatched Keys

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsException()
    {
        // Arrange - Encrypt with one set of keys
        var (key1, iv1) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key1, iv1);
        var helper1 = new EncryptionHelper();
        var plaintext = "Secret message";
        var ciphertext = helper1.Encrypt(plaintext);
        ClearEncryptionEnvironmentVariables();

        // Act - Try to decrypt with different keys
        var (key2, iv2) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key2, iv2);

        try
        {
            var helper2 = new EncryptionHelper();

            // Assert
            var exception = Assert.Throws<InvalidOperationException>(() => helper2.Decrypt(ciphertext));
            Assert.Contains("Failed to decrypt", exception.Message);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Decrypt_WithWrongIV_ThrowsException()
    {
        // Arrange - Encrypt with one set of keys
        var (key, iv1) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv1);
        var helper1 = new EncryptionHelper();
        var plaintext = "Secret message";
        var ciphertext = helper1.Encrypt(plaintext);
        ClearEncryptionEnvironmentVariables();

        // Act - Try to decrypt with same key but different IV
        var (_, iv2) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv2);

        try
        {
            var helper2 = new EncryptionHelper();

            // Assert
            var exception = Assert.Throws<InvalidOperationException>(() => helper2.Decrypt(ciphertext));
            Assert.Contains("Failed to decrypt", exception.Message);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Decrypt_WithCorruptedCiphertext_ThrowsException()
    {
        // Arrange
        var (key, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            var helper = new EncryptionHelper();
            var plaintext = "Original message";
            var ciphertext = helper.Encrypt(plaintext);

            // Corrupt the ciphertext by changing a character
            var corruptedCiphertext = ciphertext.Substring(0, ciphertext.Length - 1) + "X";

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => helper.Decrypt(corruptedCiphertext));
            Assert.Contains("Failed to decrypt", exception.Message);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ThrowsException()
    {
        // Arrange
        var (key, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            var helper = new EncryptionHelper();
            var invalidCiphertext = "This is not valid base64 @#$%";

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => helper.Decrypt(invalidCiphertext));
            Assert.Contains("Invalid ciphertext format", exception.Message);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public void Encrypt_WithNullPlaintext_ThrowsArgumentException()
    {
        // Arrange
        var (key, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            var helper = new EncryptionHelper();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => helper.Encrypt(null!));
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Encrypt_WithEmptyPlaintext_ThrowsArgumentException()
    {
        // Arrange
        var (key, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            var helper = new EncryptionHelper();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => helper.Encrypt(string.Empty));
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Decrypt_WithNullCiphertext_ThrowsArgumentException()
    {
        // Arrange
        var (key, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            var helper = new EncryptionHelper();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => helper.Decrypt(null!));
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    [Fact]
    public void Decrypt_WithEmptyCiphertext_ThrowsArgumentException()
    {
        // Arrange
        var (key, iv) = GenerateValidKeys();
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            var helper = new EncryptionHelper();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => helper.Decrypt(string.Empty));
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    #endregion

    #region GenerateKeyAndIV Tests

    [Fact]
    public void GenerateKeyAndIV_ReturnsValidBase64Strings()
    {
        // Act
        var (key, iv) = EncryptionHelper.GenerateKeyAndIV();

        // Assert
        Assert.NotNull(key);
        Assert.NotNull(iv);
        Assert.NotEmpty(key);
        Assert.NotEmpty(iv);

        // Should be valid base64
        var keyBytes = Convert.FromBase64String(key);
        var ivBytes = Convert.FromBase64String(iv);

        Assert.NotNull(keyBytes);
        Assert.NotNull(ivBytes);
    }
    
    [Fact]
    public void GenerateKeyAndIV_MultipleCalls_ReturnsDifferentKeys()
    {
        // Act
        var (key1, iv1) = EncryptionHelper.GenerateKeyAndIV();
        var (key2, iv2) = EncryptionHelper.GenerateKeyAndIV();
        var (key3, iv3) = EncryptionHelper.GenerateKeyAndIV();

        // Assert - Each generation should produce unique keys
        Assert.NotEqual(key1, key2);
        Assert.NotEqual(key2, key3);
        Assert.NotEqual(key1, key3);

        Assert.NotEqual(iv1, iv2);
        Assert.NotEqual(iv2, iv3);
        Assert.NotEqual(iv1, iv3);
    }

    [Fact]
    public void GenerateKeyAndIV_ProducesWorkingKeys()
    {
        // Act
        var (key, iv) = EncryptionHelper.GenerateKeyAndIV();
        SetEncryptionEnvironmentVariables(key, iv);

        try
        {
            var helper = new EncryptionHelper();
            var plaintext = "Test with generated keys";

            // Encrypt and decrypt should work
            var ciphertext = helper.Encrypt(plaintext);
            var decrypted = helper.Decrypt(ciphertext);

            // Assert
            Assert.Equal(plaintext, decrypted);
        }
        finally
        {
            ClearEncryptionEnvironmentVariables();
        }
    }

    #endregion
}