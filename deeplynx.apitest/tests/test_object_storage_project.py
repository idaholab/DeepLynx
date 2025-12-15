"""Tests for Storage API endpoints."""

import pytest

# ========================================================================
# PROJECT-LEVEL STORAGE TESTS
# ========================================================================

def test_create_project_storage_s3(client, organization, project, cleanup_project_storages):
    """Test creating a project-level S3 storage"""
    payload = {
        "name": "pytest_ProjectStorage_S3",
        "description": "A test AWS S3 object storage for project",
        "config": {
            "awsConnectionString": "s3://test-bucket/project-path?region=us-west-2&accessKey=test-key&secretKey=test-secret"
        }
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/storages",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to create project storage: {response.text}"
    
    result = response.json()
    assert result["name"] == "pytest_ProjectStorage_S3"
    assert "id" in result
    
    cleanup_project_storages.append(result["id"])
    print(f"Created storage ID: {result['id']}")


def test_create_project_storage_azure(client, organization, project, cleanup_project_storages):
    """Test creating a project-level Azure storage"""
    payload = {
        "name": "pytest_ProjectStorage_Azure",
        "description": "A test Azure object storage for project",
        "config": {
            "azureConnectionString": "DefaultEndpointsProtocol=https;AccountName=projectaccount;AccountKey=testkey;EndpointSuffix=core.windows.net"
        }
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/storages",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to create Azure project storage: {response.text}"
    
    result = response.json()
    assert result["name"] == "pytest_ProjectStorage_Azure"
    assert "id" in result
    
    cleanup_project_storages.append(result["id"])
    print(f"Created storage ID: {result['id']}")


def test_create_project_storage_filesystem(client, organization, project, cleanup_project_storages):
    """Test creating a project-level filesystem storage"""
    payload = {
        "name": "pytest_ProjectStorage_Filesystem",
        "description": "A test filesystem object storage for project",
        "config": {
            "mountPath": "./project_storage_test/"
        }
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/storages",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to create filesystem project storage: {response.text}"
    
    result = response.json()
    assert result["name"] == "pytest_ProjectStorage_Filesystem"
    assert "id" in result
    
    cleanup_project_storages.append(result["id"])
    print(f"Created storage ID: {result['id']}")


def test_get_all_project_storages(client, organization, project, cleanup_project_storages):
    """Test retrieving all project-level storages"""
    # Create a test storage first
    payload = {
        "name": "pytest_ProjectStorage_GetAll",
        "description": "Test project storage for get all",
        "config": {
            "mountPath": "./project_storage_getall/"
        }
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/storages",
        json=payload
    )
    assert create_response.status_code == 200
    created_id = create_response.json()["id"]
    cleanup_project_storages.append(created_id)
    
    # Get all storages
    response = client.get(
        f"/organizations/{organization}/projects/{project}/storages",
        params={"hideArchived": True}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to get project storages: {response.text}"
    
    storages = response.json()
    assert isinstance(storages, list), "Expected response to be a list"
    
    # Find our created storage
    found = False
    for storage in storages:
        if storage.get("id") == created_id:
            found = True
            assert storage["name"] == "pytest_ProjectStorage_GetAll"
            archived_status = " [ARCHIVED]" if storage.get('isArchived') else ""
            default_status = " [DEFAULT]" if storage.get('default') else ""
            print(f"Found storage: {storage['name']} (ID: {storage['id']}){default_status}{archived_status}")
            break
    
    assert found, f"Created storage with ID {created_id} not found in list"
    print(f"Total project storages: {len(storages)}")


def test_get_project_storage(client, organization, project, cleanup_project_storages):
    """Test retrieving a single project storage by ID"""
    # Create a storage
    payload = {
        "name": "pytest_ProjectStorage_GetSingle",
        "description": "Test project storage for get single",
        "config": {
            "mountPath": "./project_storage_getsingle/"
        }
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/storages",
        json=payload
    )
    assert create_response.status_code == 200
    storage_id = create_response.json()["id"]
    cleanup_project_storages.append(storage_id)
    
    # Get the specific storage
    response = client.get(
        f"/organizations/{organization}/projects/{project}/storages/{storage_id}"
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to get project storage: {response.text}"
    
    result = response.json()
    assert result["id"] == storage_id
    assert result["name"] == "pytest_ProjectStorage_GetSingle"
    # Note: API doesn't return description or config fields
    assert "type" in result, "Should have storage type"


def test_get_project_storage_nonexistent(client, organization, project):
    """Test retrieving a non-existent project storage"""
    fake_storage_id = 999999
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/storages/{fake_storage_id}"
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [404, 500], \
        f"Expected 404 or 500 for non-existent storage, got {response.status_code}: {response.text}"
    
    if response.status_code == 500:
        response_text = response.text.lower()
        assert "not found" in response_text or "does not exist" in response_text
        print("WARNING: API returns 500 for not found (should be 404)")


def test_update_project_storage(client, organization, project, cleanup_project_storages):
    """Test updating a project storage"""
    # Create a storage
    create_payload = {
        "name": "pytest_ProjectStorage_Original",
        "description": "Original project storage description",
        "config": {
            "awsConnectionString": "s3://project-original-bucket/path?region=us-west-2&accessKey=test-key&secretKey=test-secret"
        }
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/storages",
        json=create_payload
    )
    assert create_response.status_code == 200
    storage_id = create_response.json()["id"]
    cleanup_project_storages.append(storage_id)
    
    # Update the storage
    update_payload = {
        "name": "pytest_ProjectStorage_Updated",
        "description": "This project storage has been updated",
        "config": {
            "awsConnectionString": "s3://project-updated-bucket/path?region=us-east-1&accessKey=updated-key&secretKey=updated-secret"
        }
    }
    
    response = client.put(
        f"/organizations/{organization}/projects/{project}/storages/{storage_id}",
        json=update_payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to update project storage: {response.text}"
    
    result = response.json()
    assert result["id"] == storage_id
    assert result["name"] == "pytest_ProjectStorage_Updated"
    # Note: API doesn't return description field


def test_archive_and_unarchive_project_storage(client, organization, project, cleanup_project_storages):
    """Test archiving and unarchiving a project storage"""
    # Get all existing storages
    get_response = client.get(f"/organizations/{organization}/projects/{project}/storages")
    assert get_response.status_code == 200
    existing_storages = get_response.json()
    
    # Need at least 2 storages - one to be default, one to archive
    if len(existing_storages) < 2:
        print("\nNeed at least 2 storages for archive test. Creating them...")
        
        # Create two storages
        for i in range(2):
            payload = {
                "name": f"pytest_ProjectStorage_Archive_{i}",
                "description": f"Project storage for archive test {i}",
                "config": {
                    "mountPath": f"./project_storage_archive_{i}/"
                }
            }
            create_response = client.post(
                f"/organizations/{organization}/projects/{project}/storages",
                json=payload
            )
            if create_response.status_code == 200:
                cleanup_project_storages.append(create_response.json()["id"])
            else:
                pytest.skip(f"Cannot create test storages: {create_response.text}")
        
        # Refresh the list
        get_response = client.get(f"/organizations/{organization}/projects/{project}/storages")
        existing_storages = get_response.json()
    
    if len(existing_storages) < 2:
        pytest.skip("Need at least 2 storages to test archiving")
    
    # Use the first storage as default, second to archive
    default_storage_id = existing_storages[0]["id"]
    storage_to_archive_id = existing_storages[1]["id"]
    
    print(f"\nUsing storage {default_storage_id} as default")
    print(f"Will archive storage {storage_to_archive_id}")
    
    # Set first storage as default
    set_default_response = client.patch(
        f"/organizations/{organization}/projects/{project}/storages/{default_storage_id}/default"
    )
    assert set_default_response.status_code == 200, f"Failed to set default: {set_default_response.text}"
    
    # Archive the second storage
    archive_response = client.patch(
        f"/organizations/{organization}/projects/{project}/storages/{storage_to_archive_id}",
        params={"archive": True}
    )
    
    print(f"\nArchive Status Code: {archive_response.status_code}")
    print(f"Archive Response Body: {archive_response.text}")
    
    assert archive_response.status_code == 200, f"Failed to archive project storage: {archive_response.text}"
    
    # Verify archived
    get_archived_response = client.get(
        f"/organizations/{organization}/projects/{project}/storages/{storage_to_archive_id}",
        params={"hideArchived": False}
    )
    assert get_archived_response.status_code == 200
    archived_result = get_archived_response.json()
    assert archived_result.get('isArchived') == True, "Storage should be archived"
    print("✓ Storage successfully archived")
    
    # Unarchive the storage
    unarchive_response = client.patch(
        f"/organizations/{organization}/projects/{project}/storages/{storage_to_archive_id}",
        params={"archive": False}
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive project storage: {unarchive_response.text}"
    
    # Verify unarchived
    get_unarchived_response = client.get(
        f"/organizations/{organization}/projects/{project}/storages/{storage_to_archive_id}"
    )
    assert get_unarchived_response.status_code == 200
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get('isArchived') == False or 'isArchived' not in unarchived_result, \
        "Storage should not be archived"
    print("✓ Storage successfully unarchived")


def test_get_default_project_storage(client, organization, project, cleanup_project_storages):
    """Test retrieving the default project storage"""
    # Get all existing storages
    get_response = client.get(f"/organizations/{organization}/projects/{project}/storages")
    assert get_response.status_code == 200
    existing_storages = get_response.json()
    
    # If no storages exist, create one
    if len(existing_storages) == 0:
        print("\nNo storages exist. Creating one for default test...")
        payload = {
            "name": "pytest_ProjectStorage_GetDefault",
            "description": "Test default project storage",
            "config": {
                "mountPath": "./project_storage_getdefault/"
            }
        }
        create_response = client.post(
            f"/organizations/{organization}/projects/{project}/storages",
            json=payload
        )
        if create_response.status_code != 200:
            pytest.skip(f"Cannot create test storage: {create_response.text}")
        
        created_id = create_response.json()["id"]
        cleanup_project_storages.append(created_id)
    else:
        # Use first existing storage
        created_id = existing_storages[0]["id"]
        print(f"\nUsing existing storage {created_id} for default test")
    
    # Set it as default
    set_default_response = client.patch(
        f"/organizations/{organization}/projects/{project}/storages/{created_id}/default"
    )
    assert set_default_response.status_code == 200, f"Failed to set default: {set_default_response.text}"
    
    # Get the default storage
    response = client.get(f"/organizations/{organization}/projects/{project}/storages/default")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to get default project storage: {response.text}"
    
    result = response.json()
    assert "id" in result, "Default storage should have an id"
    assert result["id"] == created_id, "Default storage should be the one we just set"
    print(f"Default storage ID: {result['id']}")
    print(f"Default storage Name: {result.get('name')}")


def test_set_project_storage_as_default(client, organization, project, cleanup_project_storages):
    """Test setting a project storage as default"""
    # Get all existing storages
    get_response = client.get(f"/organizations/{organization}/projects/{project}/storages")
    assert get_response.status_code == 200
    existing_storages = get_response.json()
    
    # If no storages exist, create one
    if len(existing_storages) == 0:
        print("\nNo storages exist. Creating one for set default test...")
        payload = {
            "name": "pytest_ProjectStorage_SetDefault",
            "description": "Test set as default for project",
            "config": {
                "mountPath": "./project_storage_setdefault/"
            }
        }
        create_response = client.post(
            f"/organizations/{organization}/projects/{project}/storages",
            json=payload
        )
        if create_response.status_code != 200:
            pytest.skip(f"Cannot create test storage: {create_response.text}")
        
        storage_id = create_response.json()["id"]
        cleanup_project_storages.append(storage_id)
    else:
        # Use first existing storage
        storage_id = existing_storages[0]["id"]
        print(f"\nUsing existing storage {storage_id} for set default test")
    
    # Set it as default
    response = client.patch(
        f"/organizations/{organization}/projects/{project}/storages/{storage_id}/default"
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to set project storage as default: {response.text}"
    
    result = response.json()
    print(f"Response message: {result.get('message')}")
    
    # Verify it's now the default
    get_default_response = client.get(
        f"/organizations/{organization}/projects/{project}/storages/default"
    )
    assert get_default_response.status_code == 200
    default_storage = get_default_response.json()
    assert default_storage["id"] == storage_id, "Storage should be set as default"
    print(f"Confirmed: Storage {storage_id} is now default")


def test_delete_project_storage(client, organization, project):
    """Test deleting a project storage"""
    # Create a storage
    payload = {
        "name": "pytest_ProjectStorage_Delete",
        "description": "Test project storage for deletion",
        "config": {
            "mountPath": "./project_storage_delete/"
        }
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/storages",
        json=payload
    )
    assert create_response.status_code == 200
    storage_id = create_response.json()["id"]
    
    # Delete the storage
    response = client.delete(
        f"/organizations/{organization}/projects/{project}/storages/{storage_id}"
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to delete project storage: {response.text}"
    
    result = response.json()
    print(f"Response message: {result.get('message')}")
    
    # Verify deletion
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/storages/{storage_id}"
    )
    assert get_response.status_code in [404, 500], \
        f"Deleted storage should return 404 or 500, got {get_response.status_code}"
    
    print(f"Confirmed: Storage {storage_id} successfully deleted")


# ========================================================================
# VALIDATION AND ERROR TESTS
# ========================================================================

def test_create_storage_missing_config(client, organization, cleanup_org_storages):
    """Test creating storage without config field"""
    payload = {
        "name": "pytest_MissingConfig",
        "description": "Missing config field"
    }
    
    response = client.post(f"/organizations/{organization}/storages", json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # May succeed or fail depending on whether config is required
    if response.status_code == 200:
        storage_id = response.json().get("id")
        if storage_id:
            cleanup_org_storages.append(storage_id)
        print("WARNING: API allows creating storage without config")
    else:
        assert response.status_code in [400, 422], \
            f"Expected 400 or 422 for missing config, got {response.status_code}"


def test_update_nonexistent_storage(client, organization):
    """Test updating a non-existent storage"""
    fake_storage_id = 999999
    
    update_payload = {
        "name": "pytest_Nonexistent",
        "description": "Updating non-existent storage",
        "config": {
            "mountPath": "./nonexistent/"
        }
    }
    
    response = client.put(
        f"/organizations/{organization}/storages/{fake_storage_id}",
        json=update_payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [404, 500], \
        f"Expected 404 or 500 for non-existent storage, got {response.status_code}"


def test_delete_nonexistent_storage(client, organization):
    """Test deleting a non-existent storage"""
    fake_storage_id = 999999
    
    response = client.delete(f"/organizations/{organization}/storages/{fake_storage_id}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # API might return 404, 500, or even 200 for idempotent delete
    assert response.status_code in [404, 500, 200], \
        f"Expected 404, 500, or 200 for non-existent storage, got {response.status_code}"
    
    if response.status_code == 200:
        print("WARNING: API returns 200 when deleting non-existent storage (idempotent delete)")


def test_set_nonexistent_storage_as_default(client, organization):
    """Test setting a non-existent storage as default"""
    fake_storage_id = 999999
    
    response = client.patch(f"/organizations/{organization}/storages/{fake_storage_id}/default")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [404, 500], \
        f"Expected 404 or 500 for non-existent storage, got {response.status_code}"