"""Tests for Permission API endpoints at project level."""

import pytest

# ========================================================================
# PROJECT-LEVEL PERMISSION TESTS
# ========================================================================

def test_create_permission_project(client, organization, project, cleanup_project_permissions):
    """Test creating a single permission at project level."""
    payload = {
        "name": "pytest_ProjectTestPermission",
        "description": "A test permission for project-level API testing",
        "action": "permission testing with pytest"
    }

    response = client.post(
        f"/organizations/{organization}/projects/{project}/permissions",
        json=payload
    )

    # Register for cleanup IMMEDIATELY, before assertions
    if response.status_code == 200:
        cleanup_project_permissions.append(response.json()["id"])

    # DEBUG: Print the actual response
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert result["name"] == "pytest_ProjectTestPermission"
    assert result["description"] == payload["description"]


def test_get_all_permissions_project(client, organization, project, cleanup_project_permissions):
    """Test retrieving all permissions at project level."""
    payload = {
        "name": "pytest_ProjectTestPermission",
        "description": "A test permission for project-level API testing",
        "action": "permission testing with pytest"
    }

    # Create a permission
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/permissions",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_permissions.append(create_response.json()["id"])

    # Verify creation succeeded
    assert create_response.status_code == 200, f"Failed to create permission: {create_response.text}"
    created_permission = create_response.json()
    created_id = created_permission["id"]

    # Get all permissions
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/permissions"
    )

    # Verify GET succeeded
    assert get_response.status_code == 200, f"Failed to get permissions: {get_response.text}"
    
    all_permissions = get_response.json()
    assert isinstance(all_permissions, list), "Expected response to be a list"
    
    permission_ids = [perm["id"] for perm in all_permissions]
    assert created_id in permission_ids, f"Created permission {created_id} not found in list of permissions"
    
    our_permission = next((perm for perm in all_permissions if perm["id"] == created_id), None)
    assert our_permission is not None, f"Could not find permission with id {created_id}"
    assert our_permission["name"] == "pytest_ProjectTestPermission"
    assert our_permission["description"] == "A test permission for project-level API testing"


def test_get_permission_project(client, organization, project, cleanup_project_permissions):
    """Test retrieving a single permission by ID at project level."""
    payload = {
        "name": "pytest_ProjectTestPermission",
        "description": "A test permission for project-level API testing",
        "action": "permission testing with pytest"
    }

    # Create a permission
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/permissions",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_permissions.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create permission: {create_response.text}"
    created_id = create_response.json()["id"]

    # Get the specific permission
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/permissions/{created_id}"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get permission: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_ProjectTestPermission"
    assert result["description"] == "A test permission for project-level API testing"


def test_update_permission_project(client, organization, project, cleanup_project_permissions):
    """Test updating a permission at project level."""
    # Create a permission
    create_payload = {
        "name": "pytest_ProjectTestPermission",
        "description": "Original description",
        "action": "permission testing with pytest"
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/permissions",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_project_permissions.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create permission: {create_response.text}"
    created_id = create_response.json()["id"]

    # Update the permission
    update_payload = {
        "name": "pytest_ProjectTestPermission_Updated",
        "description": "Updated description"
    }

    update_response = client.put(
        f"/organizations/{organization}/projects/{project}/permissions/{created_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update permission: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_ProjectTestPermission_Updated"
    assert result["description"] == "Updated description"


def test_archive_and_unarchive_permission_project(client, organization, project, cleanup_project_permissions):
    """Test archiving and unarchiving a permission at project level."""
    # Create a permission
    payload = {
        "name": "pytest_ProjectTestPermission",
        "description": "A test permission for archive/unarchive testing",
        "action": "permission testing with pytest"
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/permissions",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_permissions.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create permission: {create_response.text}"
    created_id = create_response.json()["id"]

    # Archive the permission
    archive_response = client.patch(
        f"/organizations/{organization}/projects/{project}/permissions/{created_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive permission: {archive_response.text}"

    # Verify the permission is archived
    get_archived_response = client.get(
        f"/organizations/{organization}/projects/{project}/permissions/{created_id}",
        params={"hideArchived": "false"}
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived permission: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(archived_result)
    assert archived_result.get("isArchived") == True, "Permission should be archived"

    # Unarchive the permission
    unarchive_response = client.patch(
        f"/organizations/{organization}/projects/{project}/permissions/{created_id}",
        params={"archive": "false"}
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive permission: {unarchive_response.text}"

    # Verify the permission is unarchived
    get_unarchived_response = client.get(
        f"/organizations/{organization}/projects/{project}/permissions/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived permission: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Permission should not be archived"


def test_delete_permission_project(client, organization, project, cleanup_project_permissions):
    """Test permanently deleting a permission at project level."""
    # Create a permission
    payload = {
        "name": "pytest_ProjectTestPermission",
        "description": "A test permission for deletion",
        "action": "permission testing with pytest"
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/permissions",
        json=payload
    )

    # Note: We still register for cleanup in case deletion fails
    if create_response.status_code == 200:
        cleanup_project_permissions.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create permission: {create_response.text}"
    created_id = create_response.json()["id"]

    delete_response = client.delete(
        f"/organizations/{organization}/projects/{project}/permissions/{created_id}"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete permission: {delete_response.text}"

    # Verify the permission is gone
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/permissions/{created_id}"
    )
    
    # Get all permissions and confirm deleted permission is not in the list
    all_permissions_response = client.get(f"/organizations/{organization}/projects/{project}/permissions")
    assert all_permissions_response.status_code == 200, f"Failed to get all permissions: {all_permissions_response.text}"
    
    all_permissions = all_permissions_response.json()
    permission_ids = [perm["id"] for perm in all_permissions]
    
    assert created_id not in permission_ids, f"Deleted permission {created_id} should not appear in list of all permissions"
    print(f"Confirmed: Permission {created_id} not in list of {len(permission_ids)} permissions")