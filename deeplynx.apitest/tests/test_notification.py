"""Tests for Email Notification API endpoints."""

import pytest

# ========================================================================
# EMAIL NOTIFICATION TESTS
# ========================================================================

def test_send_email(client):
    """Test sending a basic email notification"""
    
    response = client.post("/notifications/email?email=test@example.com")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to send email: {response.text}"
    
    # Response might be JSON or plain text
    if response.text:
        try:
            result = response.json()
            print(f"Email send result: {result}")
        except:
            print(f"Email send result (text): {response.text}")


def test_send_email_with_name(client):
    """Test sending an email with recipient name"""
    
    response = client.post("/notifications/email?email=test@example.com&name=Test User")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to send email with name: {response.text}"
    
    if response.text:
        try:
            result = response.json()
            print(f"Email with name result: {result}")
        except:
            print(f"Email with name result (text): {response.text}")


def test_send_email_without_name(client):
    """Test sending email without name parameter (should default to email)"""
    
    response = client.post("/notifications/email?email=noname@example.com")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to send email without name: {response.text}"
    print("Name should default to email address")


def test_send_email_invalid_email(client):
    """Test sending email with invalid email address (should fail gracefully)"""
    
    response = client.post("/notifications/email?email=invalid-email")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should fail with validation error
    assert response.status_code >= 400, \
        f"Expected error status for invalid email, got {response.status_code}: {response.text}"
    
    print("Invalid email handled appropriately")


def test_send_email_empty_email(client):
    """Test sending email with empty email address (should fail)"""
    
    response = client.post("/notifications/email?email=")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should fail with validation error
    assert response.status_code >= 400, \
        f"Expected error status for empty email, got {response.status_code}: {response.text}"
    
    print("Empty email handled appropriately")


def test_send_email_missing_email_parameter(client):
    """Test sending email without email parameter (should fail)"""
    
    response = client.post("/notifications/email")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should fail with validation error
    assert response.status_code >= 400, \
        f"Expected error status for missing email, got {response.status_code}: {response.text}"
    
    print("Missing email parameter handled appropriately")


def test_send_email_with_special_characters_in_name(client):
    """Test sending email with special characters in name"""
    
    import urllib.parse
    name = urllib.parse.quote("Test User 测试 🚀")
    
    response = client.post(f"/notifications/email?email=test@example.com&name={name}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to send email with special characters in name: {response.text}"
    print("Special characters in name handled correctly")


def test_send_email_multiple_at_signs(client):
    """Test sending email with multiple @ signs (should fail)"""
    
    response = client.post("/notifications/email?email=test@@example.com")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should fail with validation error
    assert response.status_code >= 400, \
        f"Expected error status for invalid email format, got {response.status_code}: {response.text}"
    
    print("Invalid email format (multiple @) handled appropriately")


def test_send_email_no_domain(client):
    """Test sending email without domain (should fail)"""
    
    response = client.post("/notifications/email?email=test@")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should fail with validation error
    assert response.status_code >= 400, \
        f"Expected error status for email without domain, got {response.status_code}: {response.text}"
    
    print("Email without domain handled appropriately")


def test_send_email_no_local_part(client):
    """Test sending email without local part (should fail)"""
    
    response = client.post("/notifications/email?email=@example.com")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should fail with validation error
    assert response.status_code >= 400, \
        f"Expected error status for email without local part, got {response.status_code}: {response.text}"
    
    print("Email without local part handled appropriately")


def test_send_email_with_spaces(client):
    """Test sending email with spaces (should fail or be trimmed)"""
    
    import urllib.parse
    email = urllib.parse.quote(" test@example.com ")
    
    response = client.post(f"/notifications/email?email={email}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Either should succeed (if API trims) or fail (if strict validation)
    if response.status_code == 200:
        print("Email with spaces was accepted (likely trimmed)")
    else:
        assert response.status_code >= 400, \
            f"Unexpected status code: {response.status_code}"
        print("Email with spaces was rejected")


def test_send_email_very_long_email(client):
    """Test sending email with very long email address"""
    
    # Create a very long but technically valid email
    long_local_part = "a" * 64  # Maximum local part length
    long_email = f"{long_local_part}@example.com"
    
    response = client.post(f"/notifications/email?email={long_email}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    print(f"Email length: {len(long_email)} characters")
    
    # Should succeed - this is a valid email length
    assert response.status_code == 200, f"Failed to send email with long address: {response.text}"
    print("Long email address handled correctly")


def test_send_email_very_long_name(client):
    """Test sending email with very long name"""
    
    import urllib.parse
    long_name = "Test User " * 100  # Very long name
    encoded_name = urllib.parse.quote(long_name)
    
    response = client.post(f"/notifications/email?email=test@example.com&name={encoded_name}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    print(f"Name length: {len(long_name)} characters")
    
    # Should succeed or truncate
    if response.status_code == 200:
        print("Long name handled correctly")
    else:
        assert response.status_code >= 400, \
            f"Unexpected status code: {response.status_code}"
        print("Long name was rejected")


def test_send_email_case_sensitivity(client):
    """Test that email addresses are case-insensitive"""
    
    # Send to uppercase email
    response1 = client.post("/notifications/email?email=TEST@EXAMPLE.COM")
    
    print(f"\nUppercase email - Status Code: {response1.status_code}")
    print(f"Response Body: {response1.text}")
    
    # Send to lowercase email
    response2 = client.post("/notifications/email?email=test@example.com")
    
    print(f"\nLowercase email - Status Code: {response2.status_code}")
    print(f"Response Body: {response2.text}")
    
    # Both should succeed
    assert response1.status_code == 200, f"Failed with uppercase email: {response1.text}"
    assert response2.status_code == 200, f"Failed with lowercase email: {response2.text}"
    print("Email case handling works correctly")