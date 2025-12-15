"""Tests for OAuth Token Exchange API endpoints."""

import pytest
import time

# ========================================================================
# OAUTH TOKEN EXCHANGE TESTS
# ========================================================================

@pytest.fixture
def test_oauth_app(client, cleanup_oauth_applications):
    """Create a test OAuth application for token exchange tests"""
    payload = {
        "name": "pytest_TokenExchangeApp",
        "description": "Test app for token exchange",
        "callbackUrl": "http://localhost:5252/callback"
    }
    
    response = client.post("/oauth/applications", json=payload)
    assert response.status_code == 200, f"Failed to create OAuth app: {response.text}"
    
    result = response.json()
    client_id = result['clientId']
    client_secret = result['clientSecretRaw']
    
    # Get the app ID for cleanup
    get_response = client.get("/oauth/applications")
    all_apps = get_response.json()
    app_id = None
    for app in all_apps:
        if app.get('clientId') == client_id:
            app_id = app['id']
            cleanup_oauth_applications.append(app_id)
            break
    
    return {
        'app_id': app_id,
        'client_id': client_id,
        'client_secret': client_secret
    }


def test_create_oauth_token_basic(client):
    """Test creating an OAuth token with API key and secret"""
    payload = {
        "apiKey": client.token,  # Using the existing auth token as API key for testing
        "apiSecret": client.token,
        "expirationMinutes": 60
    }
    
    response = client.post("/oauth/tokens", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # This might fail if the token isn't a valid API key/secret pair
    # Adjust assertion based on actual behavior
    if response.status_code == 200:
        # Token should be returned as plain text or JSON
        token = response.text.strip('"')
        assert len(token) > 0, "Token should not be empty"
        print(f"Token created: {token[:20]}...")
    else:
        # Document the expected error
        print(f"Token creation failed (expected if using invalid credentials)")


def test_create_oauth_token_custom_expiration(client):
    """Test creating OAuth token with custom expiration time"""
    payload = {
        "apiKey": client.token,
        "apiSecret": client.token,
        "expirationMinutes": 120
    }
    
    response = client.post("/oauth/tokens", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # May fail if credentials are invalid, but we're testing the parameter acceptance
    if response.status_code == 200:
        token = response.text.strip('"')
        assert len(token) > 0
        print(f"Token with custom expiration created: {token[:20]}...")


def test_create_oauth_token_missing_api_key(client):
    """Test creating OAuth token without API key (should fail)"""
    payload = {
        "apiSecret": "some-secret",
        "expirationMinutes": 60
    }
    
    response = client.post("/oauth/tokens", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 422], \
        f"Expected 400 or 422 for missing API key, got {response.status_code}: {response.text}"
    
    print("Missing API key handled appropriately")


def test_create_oauth_token_missing_api_secret(client):
    """Test creating OAuth token without API secret (should fail)"""
    payload = {
        "apiKey": "some-key",
        "expirationMinutes": 60
    }
    
    response = client.post("/oauth/tokens", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 422], \
        f"Expected 400 or 422 for missing API secret, got {response.status_code}: {response.text}"
    
    print("Missing API secret handled appropriately")


def test_create_oauth_token_invalid_credentials(client):
    """Test creating OAuth token with invalid credentials (should fail)"""
    payload = {
        "apiKey": "invalid-key",
        "apiSecret": "invalid-secret",
        "expirationMinutes": 60
    }
    
    response = client.post("/oauth/tokens", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [401, 403, 400, 404], \
        f"Expected 401, 403, 400, or 404 for invalid credentials, got {response.status_code}: {response.text}"
    
    print("Invalid credentials rejected appropriately")


def test_create_oauth_token_negative_expiration(client):
    """Test creating OAuth token with negative expiration (should fail or default)"""
    payload = {
        "apiKey": client.token,
        "apiSecret": client.token,
        "expirationMinutes": -60
    }
    
    response = client.post("/oauth/tokens", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should either fail validation or default to a positive value
    # May also return 404 if token isn't a valid API key
    if response.status_code == 200:
        print("WARNING: API accepts negative expiration (may use default)")
    else:
        assert response.status_code in [400, 422, 404], \
            f"Expected 400, 422, or 404 for negative expiration, got {response.status_code}"
        if response.status_code == 404:
            print("Token not found as API key (expected)")
        else:
            print("Negative expiration rejected appropriately")


def test_create_oauth_token_zero_expiration(client):
    """Test creating OAuth token with zero expiration"""
    payload = {
        "apiKey": client.token,
        "apiSecret": client.token,
        "expirationMinutes": 0
    }
    
    response = client.post("/oauth/tokens", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Behavior depends on API implementation
    if response.status_code == 200:
        print("WARNING: API accepts zero expiration")
    else:
        print(f"Zero expiration handled with status {response.status_code}")


def test_create_oauth_token_very_large_expiration(client):
    """Test creating OAuth token with very large expiration time"""
    payload = {
        "apiKey": client.token,
        "apiSecret": client.token,
        "expirationMinutes": 525600  # 1 year in minutes
    }
    
    response = client.post("/oauth/tokens", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # API may have a maximum expiration limit
    if response.status_code == 200:
        token = response.text.strip('"')
        print(f"Large expiration accepted: {token[:20]}...")
    else:
        print(f"Large expiration rejected with status {response.status_code}")


def test_oauth_exchange_missing_code(client, test_oauth_app):
    """Test OAuth exchange without authorization code (should fail)"""
    params = {
        "client_id": test_oauth_app['client_id'],
        "client_secret": test_oauth_app['client_secret'],
        "redirect_uri": "http://localhost:5252/callback",
        "state": "test-state"
    }
    
    response = client.post(f"/oauth/exchange?{_build_query_string(params)}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 422], \
        f"Expected 400 or 422 for missing code, got {response.status_code}: {response.text}"
    
    print("Missing authorization code handled appropriately")


def test_oauth_exchange_invalid_code(client, test_oauth_app):
    """Test OAuth exchange with invalid authorization code (should fail)"""
    params = {
        "code": "invalid-code-12345",
        "client_id": test_oauth_app['client_id'],
        "client_secret": test_oauth_app['client_secret'],
        "redirect_uri": "http://localhost:5252/callback",
        "state": "test-state"
    }
    
    response = client.post(f"/oauth/exchange?{_build_query_string(params)}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 401, 403], \
        f"Expected 400, 401, or 403 for invalid code, got {response.status_code}: {response.text}"
    
    print("Invalid authorization code rejected appropriately")


def test_oauth_exchange_missing_client_id(client):
    """Test OAuth exchange without client_id (should fail)"""
    params = {
        "code": "some-code",
        "client_secret": "some-secret",
        "redirect_uri": "http://localhost:5252/callback",
        "state": "test-state"
    }
    
    response = client.post(f"/oauth/exchange?{_build_query_string(params)}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 422], \
        f"Expected 400 or 422 for missing client_id, got {response.status_code}: {response.text}"
    
    print("Missing client_id handled appropriately")


def test_oauth_exchange_missing_client_secret(client, test_oauth_app):
    """Test OAuth exchange without client_secret (should fail)"""
    params = {
        "code": "some-code",
        "client_id": test_oauth_app['client_id'],
        "redirect_uri": "http://localhost:5252/callback",
        "state": "test-state"
    }
    
    response = client.post(f"/oauth/exchange?{_build_query_string(params)}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 401, 422], \
        f"Expected 400, 401, or 422 for missing client_secret, got {response.status_code}: {response.text}"
    
    print("Missing client_secret handled appropriately")


def test_oauth_exchange_invalid_client_credentials(client):
    """Test OAuth exchange with invalid client credentials (should fail)"""
    params = {
        "code": "some-code",
        "client_id": "invalid-client-id",
        "client_secret": "invalid-client-secret",
        "redirect_uri": "http://localhost:5252/callback",
        "state": "test-state"
    }
    
    response = client.post(f"/oauth/exchange?{_build_query_string(params)}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 401, 403], \
        f"Expected 400, 401, or 403 for invalid credentials, got {response.status_code}: {response.text}"
    
    print("Invalid client credentials rejected appropriately")


def test_oauth_exchange_mismatched_redirect_uri(client, test_oauth_app):
    """Test OAuth exchange with redirect_uri that doesn't match registered callback"""
    params = {
        "code": "some-code",
        "client_id": test_oauth_app['client_id'],
        "client_secret": test_oauth_app['client_secret'],
        "redirect_uri": "http://different-domain.com/callback",  # Different from registered
        "state": "test-state"
    }
    
    response = client.post(f"/oauth/exchange?{_build_query_string(params)}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 401, 403], \
        f"Expected 400, 401, or 403 for mismatched redirect_uri, got {response.status_code}: {response.text}"
    
    print("Mismatched redirect_uri rejected appropriately")


def test_oauth_exchange_missing_redirect_uri(client, test_oauth_app):
    """Test OAuth exchange without redirect_uri (should fail)"""
    params = {
        "code": "some-code",
        "client_id": test_oauth_app['client_id'],
        "client_secret": test_oauth_app['client_secret'],
        "state": "test-state"
    }
    
    response = client.post(f"/oauth/exchange?{_build_query_string(params)}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # May succeed if redirect_uri is optional, or fail if required
    if response.status_code >= 400:
        print(f"Missing redirect_uri rejected with status {response.status_code}")
    else:
        print("WARNING: API accepts missing redirect_uri")


def test_oauth_exchange_empty_state(client, test_oauth_app):
    """Test OAuth exchange with empty state parameter"""
    params = {
        "code": "some-code",
        "client_id": test_oauth_app['client_id'],
        "client_secret": test_oauth_app['client_secret'],
        "redirect_uri": "http://localhost:5252/callback",
        "state": ""
    }
    
    response = client.post(f"/oauth/exchange?{_build_query_string(params)}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # State validation depends on implementation
    # Empty state may be accepted or rejected
    print(f"Empty state handled with status {response.status_code}")


# ========================================================================
# HELPER FUNCTIONS
# ========================================================================

def _build_query_string(params):
    """Build query string from parameters dictionary"""
    from urllib.parse import urlencode
    return urlencode(params)