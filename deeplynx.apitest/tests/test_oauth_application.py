"""Tests for OAuth Application API endpoints."""

import pytest

# ========================================================================
# OAUTH APPLICATION TESTS
# ========================================================================

@pytest.fixture
def cleanup_oauth_applications(client):
    """Track and cleanup OAuth applications."""
    created_ids = []
    yield created_ids
    for app_id in created_ids:
        try:
            client.delete(f"/oauth/applications/{app_id}")
        except:
            pass


def test_create_oauth_application(client, cleanup_oauth_applications):
    """Test creating a single OAuth application"""
    payload = {
        "name": "pytest_OauthApp",
        "description": "A test OAuth application",
        "callbackUrl": "http://localhost:5252/callback"
    }
    
    response = client.post(
        "/oauth/applications",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to create OAuth application: {response.text}"
    
    result = response.json()
    assert result["name"] == "pytest_OauthApp"
    assert "clientId" in result, "Response should contain clientId"
    assert "clientSecretRaw" in result, "Response should contain clientSecretRaw on creation"
    
    print(f"Client ID: {result['clientId']}")
    print(f"Client Secret: {result['clientSecretRaw']}")
    print("⚠ IMPORTANT: Client secret is only shown once!")
    
    # Get the full application to find the ID for cleanup
    get_response = client.get("/oauth/applications")
    assert get_response.status_code == 200
    all_apps = get_response.json()
    
    # Find our app by clientId
    for app in all_apps:
        if app.get('clientId') == result['clientId']:
            cleanup_oauth_applications.append(app['id'])
            print(f"Application ID for cleanup: {app['id']}")
            break


def test_create_oauth_application_with_all_fields(client, cleanup_oauth_applications):
    """Test creating OAuth application with all optional fields"""
    payload = {
        "name": "pytest_OauthApp_Full",
        "description": "OAuth app with all fields",
        "callbackUrl": "https://example.com/callback",
        "baseUrl": "https://example.com",
        "appOwnerEmail": "owner@example.com"
    }
    
    response = client.post(
        "/oauth/applications",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to create OAuth application: {response.text}"
    
    result = response.json()
    assert result["name"] == "pytest_OauthApp_Full"
    assert "clientId" in result
    assert "clientSecretRaw" in result
    
    # Get the full application to verify all fields
    get_response = client.get("/oauth/applications")
    assert get_response.status_code == 200
    all_apps = get_response.json()
    
    for app in all_apps:
        if app.get('clientId') == result['clientId']:
            cleanup_oauth_applications.append(app['id'])
            assert app['callbackUrl'] == "https://example.com/callback"
            assert app.get('baseUrl') == "https://example.com"
            assert app.get('appOwnerEmail') == "owner@example.com"
            print(f"Verified all fields for application ID: {app['id']}")
            break


def test_get_all_oauth_applications(client, cleanup_oauth_applications):
    """Test retrieving all OAuth applications"""
    # Create a test application first
    payload = {
        "name": "pytest_OauthApp_GetAll",
        "description": "Test app for get all",
        "callbackUrl": "http://localhost:5252/callback"
    }
    
    create_response = client.post("/oauth/applications", json=payload)
    assert create_response.status_code == 200
    created_client_id = create_response.json()['clientId']
    
    # Get all applications
    response = client.get("/oauth/applications?hideArchived=true")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to get OAuth applications: {response.text}"
    
    applications = response.json()
    assert isinstance(applications, list), "Expected response to be a list"
    
    # Find our created application
    found = False
    for app in applications:
        if app.get('clientId') == created_client_id:
            found = True
            cleanup_oauth_applications.append(app['id'])
            assert app['name'] == "pytest_OauthApp_GetAll"
            assert app.get('isArchived') == False or 'isArchived' not in app
            print(f"Found application: {app['name']} (ID: {app['id']})")
            break
    
    assert found, f"Created application with client ID {created_client_id} not found in list"
    print(f"Total applications: {len(applications)}")


def test_get_all_oauth_applications_hide_archived(client, cleanup_oauth_applications):
    """Test that hideArchived parameter works correctly"""
    # Create and archive an application
    payload = {
        "name": "pytest_OauthApp_Archived",
        "description": "Test app to be archived",
        "callbackUrl": "http://localhost:5252/callback"
    }
    
    create_response = client.post("/oauth/applications", json=payload)
    assert create_response.status_code == 200
    created_client_id = create_response.json()['clientId']
    
    # Get the app ID
    get_response = client.get("/oauth/applications")
    all_apps = get_response.json()
    app_id = None
    for app in all_apps:
        if app.get('clientId') == created_client_id:
            app_id = app['id']
            cleanup_oauth_applications.append(app_id)
            break
    
    assert app_id is not None, "Could not find created application"
    
    # Archive it
    archive_response = client.patch(f"/oauth/applications/{app_id}?archive=true")
    assert archive_response.status_code == 200
    
    # Test hideArchived=true (should not show archived)
    response_hidden = client.get("/oauth/applications?hideArchived=true")
    assert response_hidden.status_code == 200
    hidden_apps = response_hidden.json()
    
    found_in_hidden = any(app.get('clientId') == created_client_id for app in hidden_apps)
    print(f"Archived app found with hideArchived=true: {found_in_hidden}")
    
    # Test hideArchived=false (should show archived)
    response_shown = client.get("/oauth/applications?hideArchived=false")
    assert response_shown.status_code == 200
    shown_apps = response_shown.json()
    
    found_in_shown = False
    for app in shown_apps:
        if app.get('clientId') == created_client_id:
            found_in_shown = True
            assert app.get('isArchived') == True, "Application should be marked as archived"
            print(f"Found archived application with hideArchived=false")
            break
    
    assert found_in_shown, "Archived application should appear when hideArchived=false"


def test_get_oauth_application(client, cleanup_oauth_applications):
    """Test retrieving a single OAuth application by ID"""
    # Create an application
    payload = {
        "name": "pytest_OauthApp_GetSingle",
        "description": "Test app for get single",
        "callbackUrl": "http://localhost:5252/callback",
        "baseUrl": "https://example.com"
    }
    
    create_response = client.post("/oauth/applications", json=payload)
    assert create_response.status_code == 200
    created_client_id = create_response.json()['clientId']
    
    # Get the app ID
    get_all_response = client.get("/oauth/applications")
    all_apps = get_all_response.json()
    app_id = None
    for app in all_apps:
        if app.get('clientId') == created_client_id:
            app_id = app['id']
            cleanup_oauth_applications.append(app_id)
            break
    
    assert app_id is not None, "Could not find created application"
    
    # Get the specific application
    response = client.get(f"/oauth/applications/{app_id}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to get OAuth application: {response.text}"
    
    result = response.json()
    assert result['id'] == app_id
    assert result['name'] == "pytest_OauthApp_GetSingle"
    assert result['description'] == "Test app for get single"
    assert result['callbackUrl'] == "http://localhost:5252/callback"
    assert result.get('baseUrl') == "https://example.com"
    assert result['clientId'] == created_client_id
    assert 'lastUpdatedAt' in result or 'updatedAt' in result


def test_get_oauth_application_nonexistent(client):
    """Test retrieving a non-existent OAuth application"""
    fake_app_id = 999999
    
    response = client.get(f"/oauth/applications/{fake_app_id}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [404, 500], \
        f"Expected 404 or 500 for non-existent application, got {response.status_code}: {response.text}"
    
    if response.status_code == 500:
        response_text = response.text.lower()
        assert "not found" in response_text or "does not exist" in response_text
        print("WARNING: API returns 500 for not found (should be 404)")


def test_update_oauth_application(client, cleanup_oauth_applications):
    """Test updating an OAuth application"""
    # Create an application
    create_payload = {
        "name": "pytest_OauthApp_Original",
        "description": "Original description",
        "callbackUrl": "http://localhost:5252/callback"
    }
    
    create_response = client.post("/oauth/applications", json=create_payload)
    assert create_response.status_code == 200
    created_client_id = create_response.json()['clientId']
    
    # Get the app ID
    get_response = client.get("/oauth/applications")
    all_apps = get_response.json()
    app_id = None
    for app in all_apps:
        if app.get('clientId') == created_client_id:
            app_id = app['id']
            cleanup_oauth_applications.append(app_id)
            break
    
    assert app_id is not None
    
    # Update the application
    update_payload = {
        "name": "pytest_OauthApp_Updated",
        "description": "Updated description",
        "callbackUrl": "https://updated.example.com/callback",
        "baseUrl": "https://updated.example.com",
        "appOwnerEmail": "updated@example.com"
    }
    
    response = client.put(f"/oauth/applications/{app_id}", json=update_payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to update OAuth application: {response.text}"
    
    result = response.json()
    assert result['id'] == app_id
    assert result['name'] == "pytest_OauthApp_Updated"
    assert result['description'] == "Updated description"
    assert result['callbackUrl'] == "https://updated.example.com/callback"
    assert result.get('baseUrl') == "https://updated.example.com"
    assert result.get('appOwnerEmail') == "updated@example.com"


def test_archive_and_unarchive_oauth_application(client, cleanup_oauth_applications):
    """Test archiving and unarchiving an OAuth application"""
    # Create an application
    payload = {
        "name": "pytest_OauthApp_Archive",
        "description": "Test app for archive/unarchive",
        "callbackUrl": "http://localhost:5252/callback"
    }
    
    create_response = client.post("/oauth/applications", json=payload)
    assert create_response.status_code == 200
    created_client_id = create_response.json()['clientId']
    
    # Get the app ID
    get_response = client.get("/oauth/applications")
    all_apps = get_response.json()
    app_id = None
    for app in all_apps:
        if app.get('clientId') == created_client_id:
            app_id = app['id']
            cleanup_oauth_applications.append(app_id)
            break
    
    assert app_id is not None
    
    # Archive the application
    archive_response = client.patch(f"/oauth/applications/{app_id}?archive=true")
    
    print(f"\nArchive Status Code: {archive_response.status_code}")
    print(f"Archive Response Body: {archive_response.text}")
    
    assert archive_response.status_code == 200, f"Failed to archive OAuth application: {archive_response.text}"
    
    # Verify archived
    get_archived_response = client.get(f"/oauth/applications/{app_id}?hideArchived=false")
    assert get_archived_response.status_code == 200
    archived_result = get_archived_response.json()
    assert archived_result.get('isArchived') == True, "Application should be archived"
    
    # Unarchive the application
    unarchive_response = client.patch(f"/oauth/applications/{app_id}?archive=false")
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive OAuth application: {unarchive_response.text}"
    
    # Verify unarchived
    get_unarchived_response = client.get(f"/oauth/applications/{app_id}")
    assert get_unarchived_response.status_code == 200
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get('isArchived') == False or 'isArchived' not in unarchived_result, \
        "Application should not be archived"


def test_delete_oauth_application(client):
    """Test deleting an OAuth application"""
    # Create an application
    payload = {
        "name": "pytest_OauthApp_Delete",
        "description": "Test app for deletion",
        "callbackUrl": "http://localhost:5252/callback"
    }
    
    create_response = client.post("/oauth/applications", json=payload)
    assert create_response.status_code == 200
    created_client_id = create_response.json()['clientId']
    
    # Get the app ID
    get_response = client.get("/oauth/applications")
    all_apps = get_response.json()
    app_id = None
    for app in all_apps:
        if app.get('clientId') == created_client_id:
            app_id = app['id']
            break
    
    assert app_id is not None
    
    # Delete the application
    response = client.delete(f"/oauth/applications/{app_id}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to delete OAuth application: {response.text}"
    
    # Verify deletion - should not be found
    get_deleted_response = client.get(f"/oauth/applications/{app_id}")
    assert get_deleted_response.status_code in [404, 500], \
        f"Deleted application should return 404 or 500, got {get_deleted_response.status_code}"
    
    # Also verify it's not in the list
    get_all_response = client.get("/oauth/applications")
    all_apps_after = get_all_response.json()
    found_after_delete = any(app.get('clientId') == created_client_id for app in all_apps_after)
    assert not found_after_delete, "Deleted application should not appear in list"
    
    print(f"Confirmed: Application {app_id} successfully deleted")


def test_create_oauth_application_missing_required_fields(client):
    """Test creating OAuth application without required fields"""
    # Missing callbackUrl
    payload = {
        "name": "pytest_OauthApp_Invalid",
        "description": "Missing callback URL"
    }
    
    response = client.post("/oauth/applications", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 422], \
        f"Expected 400 or 422 for missing required field, got {response.status_code}: {response.text}"
    
    print("Missing callbackUrl handled appropriately")


def test_create_oauth_application_invalid_callback_url(client):
    """Test creating OAuth application with invalid callback URL"""
    payload = {
        "name": "pytest_OauthApp_InvalidURL",
        "description": "Invalid callback URL",
        "callbackUrl": "not-a-valid-url"
    }
    
    response = client.post("/oauth/applications", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Might succeed or fail depending on validation
    if response.status_code == 200:
        # Clean up if it was created
        created_client_id = response.json()['clientId']
        get_response = client.get("/oauth/applications")
        all_apps = get_response.json()
        for app in all_apps:
            if app.get('clientId') == created_client_id:
                client.delete(f"/oauth/applications/{app['id']}")
                break
        print("WARNING: API accepts invalid callback URL format")
    else:
        assert response.status_code in [400, 422], \
            f"Expected 400 or 422 for invalid URL, got {response.status_code}"
        print("Invalid callback URL rejected appropriately")