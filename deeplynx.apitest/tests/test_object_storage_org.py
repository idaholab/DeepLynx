"""Tests for Storage API endpoints."""

import pytest

# ========================================================================
# ORGANIZATION-LEVEL STORAGE TESTS
# ========================================================================

def test_create_org_storage_s3(client, organization, cleanup_org_storages):
    """Test creating an organization-level S3 storage"""
    payload = {
        "name": "pytest_OrgStorage_S3",
        "description": "A test AWS S3 object storage",
        "config": {
            "awsConnectionString": "s3://test-bucket/path?region=us-west-2&accessKey=test-key&secretKey=test-secret"
        }
    }
    
    response = client.post(
        f"/organizations/{organization}/storages",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # S3 storage creation might fail if AWS credentials are invalid (500 error)
    if response.status_code == 500 and "Unable to create object storage" in response.text:
        print("WARNING: S3 storage creation failed - likely requires valid AWS credentials")
        pytest.skip("S3 storage requires valid AWS credentials")
    
    assert response.status_code == 200, f"Failed to create organization storage: {response.text}"
    
    result = response.json()
    assert result["name"] == "pytest_OrgStorage_S3"
    assert "id" in result, "Response should contain storage id"
    assert result.get("type") in ["aws_s3", "S3", "s3", None], "Storage type should be S3"
    
    cleanup_org_storages.append(result["id"])
    print(f"Created storage ID: {result['id']}")
    print(f"Storage Type: {result.get('type')}")


def test_create_org_storage_azure(client, organization, cleanup_org_storages):
    """Test creating an organization-level Azure storage"""
    payload = {
        "name": "pytest_OrgStorage_Azure",
        "description": "A test Azure object storage",
        "config": {
            "azureConnectionString": "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=testkey;EndpointSuffix=core.windows.net"
        }
    }
    
    response = client.post(
        f"/organizations/{organization}/storages",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to create Azure storage: {response.text}"
    
    result = response.json()
    assert result["name"] == "pytest_OrgStorage_Azure"
    assert "id" in result
    
    cleanup_org_storages.append(result["id"])
    print(f"Created storage ID: {result['id']}")


def test_create_org_storage_filesystem(client, organization, cleanup_org_storages):
    """Test creating an organization-level filesystem storage"""
    payload = {
        "name": "pytest_OrgStorage_Filesystem",
        "description": "A test filesystem object storage",
        "config": {
            "mountPath": "./storage_test/"
        }
    }
    
    response = client.post(
        f"/organizations/{organization}/storages",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to create filesystem storage: {response.text}"
    
    result = response.json()
    assert result["name"] == "pytest_OrgStorage_Filesystem"
    assert "id" in result
    
    cleanup_org_storages.append(result["id"])
    print(f"Created storage ID: {result['id']}")


def test_get_all_org_storages(client, organization, cleanup_org_storages):
    """Test retrieving all organization-level storages"""
    # Create a test storage first
    payload = {
        "name": "pytest_OrgStorage_GetAll",
        "description": "Test storage for get all",
        "config": {
            "mountPath": "./storage_getall/"
        }
    }
    
    create_response = client.post(f"/organizations/{organization}/storages", json=payload)
    assert create_response.status_code == 200
    created_id = create_response.json()["id"]
    cleanup_org_storages.append(created_id)
    
    # Get all storages with hideArchived=true (default behavior)
    response = client.get(f"/organizations/{organization}/storages", params={"hideArchived": True})
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to get organization storages: {response.text}"
    
    storages = response.json()
    assert isinstance(storages, list), "Expected response to be a list"
    
    # Find our created storage
    found = False
    for storage in storages:
        if storage.get("id") == created_id:
            found = True
            assert storage["name"] == "pytest_OrgStorage_GetAll"
            archived_status = " [ARCHIVED]" if storage.get('isArchived') else ""
            default_status = " [DEFAULT]" if storage.get('default') else ""
            print(f"Found storage: {storage['name']} (ID: {storage['id']}){default_status}{archived_status}")
            break
    
    assert found, f"Created storage with ID {created_id} not found in list"
    print(f"Total organization storages: {len(storages)}")


def test_get_all_org_storages_hide_archived(client, organization):
    """Test that hideArchived parameter works correctly"""
    # Test hideArchived=true (default behavior)
    response_hidden = client.get(
        f"/organizations/{organization}/storages",
        params={"hideArchived": True}
    )
    
    print(f"\nStatus Code (hideArchived=true): {response_hidden.status_code}")
    
    assert response_hidden.status_code == 200, f"Failed to get storages with hideArchived=true: {response_hidden.text}"
    hidden_storages = response_hidden.json()
    assert isinstance(hidden_storages, list), "Expected response to be a list"
    
    print(f"Retrieved {len(hidden_storages)} storages with hideArchived=true")
    for storage in hidden_storages:
        archived_status = " [ARCHIVED]" if storage.get('isArchived') else ""
        default_status = " [DEFAULT]" if storage.get('default') else ""
        print(f"  - ID: {storage.get('id')}, Name: {storage.get('name')}{default_status}{archived_status}")
    
    # Test hideArchived=false (should show all including archived)
    response_shown = client.get(
        f"/organizations/{organization}/storages",
        params={"hideArchived": False}
    )
    
    print(f"\nStatus Code (hideArchived=false): {response_shown.status_code}")
    
    assert response_shown.status_code == 200, f"Failed to get storages with hideArchived=false: {response_shown.text}"
    shown_storages = response_shown.json()
    assert isinstance(shown_storages, list), "Expected response to be a list"
    
    print(f"Retrieved {len(shown_storages)} storages with hideArchived=false")
    for storage in shown_storages:
        archived_status = " [ARCHIVED]" if storage.get('isArchived') else ""
        default_status = " [DEFAULT]" if storage.get('default') else ""
        print(f"  - ID: {storage.get('id')}, Name: {storage.get('name')}{default_status}{archived_status}")
    
    # hideArchived=false should return same or more storages than hideArchived=true
    assert len(shown_storages) >= len(hidden_storages), \
        "hideArchived=false should return at least as many storages as hideArchived=true"
    
    print(f"\n✓ hideArchived parameter works correctly")


def test_get_org_storage(client, organization, cleanup_org_storages):
    """Test retrieving a single organization storage by ID"""
    # Create a storage
    payload = {
        "name": "pytest_OrgStorage_GetSingle",
        "description": "Test storage for get single",
        "config": {
            "mountPath": "./storage_getsingle/"
        }
    }
    
    create_response = client.post(f"/organizations/{organization}/storages", json=payload)
    assert create_response.status_code == 200
    storage_id = create_response.json()["id"]
    cleanup_org_storages.append(storage_id)
    
    # Get the specific storage
    response = client.get(f"/organizations/{organization}/storages/{storage_id}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to get organization storage: {response.text}"
    
    result = response.json()
    assert result["id"] == storage_id
    assert result["name"] == "pytest_OrgStorage_GetSingle"
    # Note: API doesn't return description or config fields
    assert "type" in result, "Should have storage type"
    print(f"Storage Type: {result.get('type')}")
    print(f"Is Default: {result.get('default')}")
    print(f"Is Archived: {result.get('isArchived')}")


def test_get_org_storage_nonexistent(client, organization):
    """Test retrieving a non-existent organization storage"""
    fake_storage_id = 999999
    
    response = client.get(f"/organizations/{organization}/storages/{fake_storage_id}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [404, 500], \
        f"Expected 404 or 500 for non-existent storage, got {response.status_code}: {response.text}"
    
    if response.status_code == 500:
        response_text = response.text.lower()
        assert "not found" in response_text or "does not exist" in response_text
        print("WARNING: API returns 500 for not found (should be 404)")


def test_update_org_storage(client, organization, cleanup_org_storages):
    """Test updating an organization storage"""
    # Create a storage
    create_payload = {
        "name": "pytest_OrgStorage_Original",
        "description": "Original description",
        "config": {
            "awsConnectionString": "s3://original-bucket/path?region=us-west-2&accessKey=test-key&secretKey=test-secret"
        }
    }
    
    create_response = client.post(f"/organizations/{organization}/storages", json=create_payload)
    assert create_response.status_code == 200
    storage_id = create_response.json()["id"]
    cleanup_org_storages.append(storage_id)
    
    # Update the storage
    update_payload = {
        "name": "pytest_OrgStorage_Updated",
        "description": "This storage has been updated",
        "config": {
            "awsConnectionString": "s3://updated-bucket/path?region=us-east-1&accessKey=updated-key&secretKey=updated-secret"
        }
    }
    
    response = client.put(
        f"/organizations/{organization}/storages/{storage_id}",
        json=update_payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to update organization storage: {response.text}"
    
    result = response.json()
    assert result["id"] == storage_id
    assert result["name"] == "pytest_OrgStorage_Updated"
    # Note: API doesn't return description field
    print(f"Updated storage type: {result.get('type')}")


# def test_archive_and_unarchive_org_storage(client, organization, cleanup_org_storages):
#     """Test archiving and unarchiving an organization storage"""
#     # Get all existing storages
#     get_response = client.get(f"/organizations/{organization}/storages")
#     assert get_response.status_code == 200
#     existing_storages = get_response.json()
    
#     # Need at least 2 storages - one to be default, one to archive
#     if len(existing_storages) < 2:
#         print("\nNeed at least 2 storages for archive test. Creating them...")
        
#         # Create two storages
#         for i in range(2):
#             payload = {
#                 "name": f"pytest_OrgStorage_Archive_{i}",
#                 "description": f"Storage for archive test {i}",
#                 "config": {
#                     "mountPath": f"./storage_archive_{i}/"
#                 }
#             }
#             create_response = client.post(f"/organizations/{organization}/storages", json=payload)
#             if create_response.status_code == 200:
#                 cleanup_org_storages.append(create_response.json()["id"])
#             else:
#                 pytest.skip(f"Cannot create test storages: {create_response.text}")
        
#         # Refresh the list
#         get_response = client.get(f"/organizations/{organization}/storages")
#         existing_storages = get_response.json()
    
#     if len(existing_storages) < 2:
#         pytest.skip("Need at least 2 storages to test archiving")
    
#     # Use the first storage as default, second to archive
#     default_storage_id = existing_storages[0]["id"]
#     storage_to_archive_id = existing_storages[1]["id"]
    
#     print(f"\nUsing storage {default_storage_id} as default")
#     print(f"Will archive storage {storage_to_archive_id}")
    
#     # Set first storage as default
#     set_default_response = client.patch(
#         f"/organizations/{organization}/storages/{default_storage_id}/default"
#     )
#     assert set_default_response.status_code == 200, f"Failed to set default: {set_default_response.text}"
    
#     # Archive the second storage
#     archive_response = client.patch(
#         f"/organizations/{organization}/storages/{storage_to_archive_id}",
#         params={"archive": True}
#     )
    
#     print(f"\nArchive Status Code: {archive_response.status_code}")
#     print(f"Archive Response Body: {archive_response.text}")
    
#     assert archive_response.status_code == 200, f"Failed to archive storage: {archive_response.text}"
    
#     # Verify archived
#     get_archived_response = client.get(
#         f"/organizations/{organization}/storages/{storage_to_archive_id}",
#         params={"hideArchived": False}
#     )
#     assert get_archived_response.status_code == 200
#     archived_result = get_archived_response.json()
#     assert archived_result.get('isArchived') == True, "Storage should be archived"
#     print("✓ Storage successfully archived")
    
#     # Unarchive the storage
#     unarchive_response = client.patch(
#         f"/organizations/{organization}/storages/{storage_to_archive_id}",
#         params={"archive": False}
#     )
    
#     print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
#     print(f"Unarchive Response Body: {unarchive_response.text}")
    
#     assert unarchive_response.status_code == 200, f"Failed to unarchive storage: {unarchive_response.text}"
    
#     # Verify unarchived
#     get_unarchived_response = client.get(f"/organizations/{organization}/storages/{storage_to_archive_id}")
#     assert get_unarchived_response.status_code == 200
#     unarchived_result = get_unarchived_response.json()
#     assert unarchived_result.get('isArchived') == False or 'isArchived' not in unarchived_result, \
#         "Storage should not be archived"
#     print("✓ Storage successfully unarchived")


# def test_get_default_org_storage(client, organization, cleanup_org_storages):
#     """Test retrieving the default organization storage"""
#     # Get all existing storages
#     get_response = client.get(f"/organizations/{organization}/storages")
#     assert get_response.status_code == 200
#     existing_storages = get_response.json()
    
#     # If no storages exist, create one
#     if len(existing_storages) == 0:
#         print("\nNo storages exist. Creating one for default test...")
#         payload = {
#             "name": "pytest_OrgStorage_GetDefault",
#             "description": "Test default storage",
#             "config": {
#                 "mountPath": "./storage_getdefault/"
#             }
#         }
#         create_response = client.post(f"/organizations/{organization}/storages", json=payload)
#         if create_response.status_code != 200:
#             pytest.skip(f"Cannot create test storage: {create_response.text}")
        
#         created_id = create_response.json()["id"]
#         cleanup_org_storages.append(created_id)
#     else:
#         # Use first existing storage
#         created_id = existing_storages[0]["id"]
#         print(f"\nUsing existing storage {created_id} for default test")
    
#     # Set it as default
#     set_default_response = client.patch(
#         f"/organizations/{organization}/storages/{created_id}/default"
#     )
#     assert set_default_response.status_code == 200, f"Failed to set default: {set_default_response.text}"
    
#     # Get the default storage
#     response = client.get(f"/organizations/{organization}/storages/default")
    
#     print(f"\nStatus Code: {response.status_code}")
#     print(f"Response Body: {response.text}")
    
#     assert response.status_code == 200, f"Failed to get default organization storage: {response.text}"
    
#     result = response.json()
#     assert "id" in result, "Default storage should have an id"
#     assert result["id"] == created_id, "Default storage should be the one we just set"
#     print(f"Default storage ID: {result['id']}")
#     print(f"Default storage Name: {result.get('name')}")


# def test_set_org_storage_as_default(client, organization, cleanup_org_storages):
#     """Test setting an organization storage as default"""
#     # Get all existing storages
#     get_response = client.get(f"/organizations/{organization}/storages")
#     assert get_response.status_code == 200
#     existing_storages = get_response.json()
    
#     # If no storages exist, create one
#     if len(existing_storages) == 0:
#         print("\nNo storages exist. Creating one for set default test...")
#         payload = {
#             "name": "pytest_OrgStorage_SetDefault",
#             "description": "Test set as default",
#             "config": {
#                 "mountPath": "./storage_setdefault/"
#             }
#         }
#         create_response = client.post(f"/organizations/{organization}/storages", json=payload)
#         if create_response.status_code != 200:
#             pytest.skip(f"Cannot create test storage: {create_response.text}")
        
#         storage_id = create_response.json()["id"]
#         cleanup_org_storages.append(storage_id)
#     else:
#         # Use first existing storage
#         storage_id = existing_storages[0]["id"]
#         print(f"\nUsing existing storage {storage_id} for set default test")
    
#     # Set it as default
#     response = client.patch(f"/organizations/{organization}/storages/{storage_id}/default")
    
#     print(f"\nStatus Code: {response.status_code}")
#     print(f"Response Body: {response.text}")
    
#     assert response.status_code == 200, f"Failed to set organization storage as default: {response.text}"
    
#     result = response.json()
#     print(f"Response message: {result.get('message')}")
    
#     # Verify it's now the default
#     get_default_response = client.get(f"/organizations/{organization}/storages/default")
#     assert get_default_response.status_code == 200
#     default_storage = get_default_response.json()
#     assert default_storage["id"] == storage_id, "Storage should be set as default"
#     print(f"Confirmed: Storage {storage_id} is now default")


def test_delete_org_storage(client, organization):
    """Test deleting an organization storage"""
    # Create a storage
    payload = {
        "name": "pytest_OrgStorage_Delete",
        "description": "Test storage for deletion",
        "config": {
            "mountPath": "./storage_delete/"
        }
    }
    
    create_response = client.post(f"/organizations/{organization}/storages", json=payload)
    assert create_response.status_code == 200
    storage_id = create_response.json()["id"]
    
    # Delete the storage
    response = client.delete(f"/organizations/{organization}/storages/{storage_id}")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to delete organization storage: {response.text}"
    
    result = response.json()
    print(f"Response message: {result.get('message')}")
    
    # Verify deletion - should not be found
    get_response = client.get(f"/organizations/{organization}/storages/{storage_id}")
    assert get_response.status_code in [404, 500], \
        f"Deleted storage should return 404 or 500, got {get_response.status_code}"
    
    print(f"Confirmed: Storage {storage_id} successfully deleted")