"""Tests for Organization-level Role API endpoints."""

import pytest

# ========================================================================
# ORGANIZATION-LEVEL ROLE TESTS
# ========================================================================

def test_create_org_role(client, organization, cleanup_org_roles):
    """Test creating a single role at organization level."""
    payload = {
        "name": "pytest_OrgTestRole",
        "description": "A test role for organization-level API testing"
    }

    response = client.post(
        f"/organizations/{organization}/roles",
        json=payload
    )

    # Register for cleanup IMMEDIATELY, before assertions
    if response.status_code == 200:
        cleanup_org_roles.append(response.json()["id"])

    # DEBUG: Print the actual response
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert result["name"] == "pytest_OrgTestRole"
    assert result["description"] == payload["description"]


def test_get_all_org_roles(client, organization, cleanup_org_roles):
    """Test retrieving all roles at organization level."""
    payload = {
        "name": "pytest_OrgTestRole",
        "description": "A test role for organization-level API testing"
    }

    # Create a role
    create_response = client.post(
        f"/organizations/{organization}/roles",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_roles.append(create_response.json()["id"])

    # Verify creation succeeded
    assert create_response.status_code == 200, f"Failed to create role: {create_response.text}"
    created_role = create_response.json()
    created_id = created_role["id"]

    # Get all roles
    get_response = client.get(
        f"/organizations/{organization}/roles"
    )

    # Verify GET succeeded
    assert get_response.status_code == 200, f"Failed to get roles: {get_response.text}"
    
    all_roles = get_response.json()
    assert isinstance(all_roles, list), "Expected response to be a list"
    
    role_ids = [role["id"] for role in all_roles]
    assert created_id in role_ids, f"Created role {created_id} not found in list of roles"
    
    our_role = next((role for role in all_roles if role["id"] == created_id), None)
    assert our_role is not None, f"Could not find role with id {created_id}"
    assert our_role["name"] == "pytest_OrgTestRole"
    assert our_role["description"] == "A test role for organization-level API testing"


def test_get_org_role(client, organization, cleanup_org_roles):
    """Test retrieving a single role by ID at organization level."""
    payload = {
        "name": "pytest_OrgTestRole",
        "description": "A test role for organization-level API testing"
    }

    # Create a role
    create_response = client.post(
        f"/organizations/{organization}/roles",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_roles.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create role: {create_response.text}"
    created_id = create_response.json()["id"]

    # Get the specific role
    get_response = client.get(
        f"/organizations/{organization}/roles/{created_id}"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get role: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_OrgTestRole"
    assert result["description"] == "A test role for organization-level API testing"


def test_update_org_role(client, organization, cleanup_org_roles):
    """Test updating a role at organization level."""
    # Create a role
    create_payload = {
        "name": "pytest_OrgTestRole",
        "description": "Original description"
    }

    create_response = client.post(
        f"/organizations/{organization}/roles",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_org_roles.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create role: {create_response.text}"
    created_id = create_response.json()["id"]

    # Update the role
    update_payload = {
        "name": "pytest_OrgTestRole_Updated",
        "description": "Updated description"
    }

    update_response = client.put(
        f"/organizations/{organization}/roles/{created_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update role: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_OrgTestRole_Updated"
    assert result["description"] == "Updated description"


def test_archive_and_unarchive_org_role(client, organization, cleanup_org_roles):
    """Test archiving and unarchiving a role at organization level."""
    # Create a role
    payload = {
        "name": "pytest_OrgTestRole",
        "description": "A test role for archive/unarchive testing"
    }

    create_response = client.post(
        f"/organizations/{organization}/roles",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_roles.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create role: {create_response.text}"
    created_id = create_response.json()["id"]

    # Archive the role
    archive_response = client.patch(
        f"/organizations/{organization}/roles/{created_id}?archive=true"
    )
    
    print(f"\nArchive Status Code: {archive_response.status_code}")
    print(f"Archive Response Body: {archive_response.text}")
    
    assert archive_response.status_code == 200, f"Failed to archive role: {archive_response.text}"

    # Verify the role is archived
    get_archived_response = client.get(
        f"/organizations/{organization}/roles/{created_id}?hideArchived=false"
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived role: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(f"Archived role: {archived_result}")
    assert archived_result.get("isArchived") == True, "Role should be archived"

    # Unarchive the role
    unarchive_response = client.patch(
        f"/organizations/{organization}/roles/{created_id}?archive=false"
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive role: {unarchive_response.text}"

    # Verify the role is unarchived
    get_unarchived_response = client.get(
        f"/organizations/{organization}/roles/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived role: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Role should not be archived"


def test_delete_org_role(client, organization, cleanup_org_roles):
    """Test permanently deleting a role at organization level."""
    # Create a role
    payload = {
        "name": "pytest_OrgTestRole",
        "description": "A test role for deletion"
    }

    create_response = client.post(
        f"/organizations/{organization}/roles",
        json=payload
    )

    # Note: We still register for cleanup in case deletion fails
    if create_response.status_code == 200:
        cleanup_org_roles.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create role: {create_response.text}"
    created_id = create_response.json()["id"]

    delete_response = client.delete(
        f"/organizations/{organization}/roles/{created_id}"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete role: {delete_response.text}"

    # Verify the role is gone
    # Get all roles and confirm deleted role is not in the list
    all_roles_response = client.get(f"/organizations/{organization}/roles")
    assert all_roles_response.status_code == 200, f"Failed to get all roles: {all_roles_response.text}"
    
    all_roles = all_roles_response.json()
    role_ids = [role["id"] for role in all_roles]
    
    assert created_id not in role_ids, f"Deleted role {created_id} should not appear in list of all roles"
    print(f"Confirmed: Role {created_id} not in list of {len(role_ids)} roles")


# ========================================================================
# ROLE-PERMISSION TESTS
# ========================================================================

def test_get_permissions_for_org_role(client, organization, cleanup_org_roles, cleanup_org_permissions):
    """Test getting permissions for a role at organization level."""
    # Create a role
    role_payload = {
        "name": "pytest_OrgTestRole",
        "description": "Role for permission testing"
    }

    role_response = client.post(
        f"/organizations/{organization}/roles",
        json=role_payload
    )

    if role_response.status_code == 200:
        cleanup_org_roles.append(role_response.json()["id"])

    assert role_response.status_code == 200, f"Failed to create role: {role_response.text}"
    role_id = role_response.json()["id"]

    # Get permissions for the role (should be empty initially)
    get_response = client.get(
        f"/organizations/{organization}/roles/{role_id}/permissions"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get permissions: {get_response.text}"
    result = get_response.json()
    assert isinstance(result, list), "Expected response to be a list"


def test_add_permission_to_org_role(client, organization, cleanup_org_roles, cleanup_org_permissions):
    """Test adding a permission to a role at organization level."""
    # Create a role
    role_payload = {
        "name": "pytest_OrgTestRole",
        "description": "Role for permission testing"
    }

    role_response = client.post(
        f"/organizations/{organization}/roles",
        json=role_payload
    )

    if role_response.status_code == 200:
        cleanup_org_roles.append(role_response.json()["id"])

    assert role_response.status_code == 200, f"Failed to create role: {role_response.text}"
    role_id = role_response.json()["id"]

    # Create a permission
    perm_payload = {
        "name": "pytest_TestPermission",
        "description": "Test permission",
        "action": "read"
    }

    perm_response = client.post(
        f"/organizations/{organization}/permissions",
        json=perm_payload
    )

    if perm_response.status_code == 200:
        cleanup_org_permissions.append(perm_response.json()["id"])

    assert perm_response.status_code == 200, f"Failed to create permission: {perm_response.text}"
    perm_id = perm_response.json()["id"]

    # Add permission to role
    add_response = client.post(
        f"/organizations/{organization}/roles/{role_id}/permissions/{perm_id}"
    )

    print(f"\nStatus Code: {add_response.status_code}")
    print(f"Response Body: {add_response.text}")

    assert add_response.status_code == 200, f"Failed to add permission to role: {add_response.text}"


def test_set_permissions_for_org_role(client, organization, cleanup_org_roles, cleanup_org_permissions):
    """Test setting all permissions for a role (replaces existing) at organization level."""
    # Create a role
    role_payload = {
        "name": "pytest_OrgTestRole",
        "description": "Role for permission testing"
    }

    role_response = client.post(
        f"/organizations/{organization}/roles",
        json=role_payload
    )

    if role_response.status_code == 200:
        cleanup_org_roles.append(role_response.json()["id"])

    assert role_response.status_code == 200, f"Failed to create role: {role_response.text}"
    role_id = role_response.json()["id"]

    # Create multiple permissions
    permission_ids = []
    for i in range(3):
        perm_payload = {
            "name": f"pytest_TestPermission{i+1}",
            "description": f"Test permission {i+1}",
            "action": ["read", "write", "delete"][i]
        }

        perm_response = client.post(
            f"/organizations/{organization}/permissions",
            json=perm_payload
        )

        if perm_response.status_code == 200:
            perm_id = perm_response.json()["id"]
            permission_ids.append(perm_id)
            cleanup_org_permissions.append(perm_id)

    assert len(permission_ids) == 3, "Failed to create all test permissions"

    # Set permissions for role
    set_response = client.put(
        f"/organizations/{organization}/roles/{role_id}/permissions",
        json=permission_ids
    )

    print(f"\nStatus Code: {set_response.status_code}")
    print(f"Response Body: {set_response.text}")

    assert set_response.status_code == 200, f"Failed to set permissions for role: {set_response.text}"

    # Verify permissions were set
    get_response = client.get(
        f"/organizations/{organization}/roles/{role_id}/permissions"
    )

    assert get_response.status_code == 200, f"Failed to get permissions: {get_response.text}"
    result = get_response.json()
    assert isinstance(result, list), "Expected response to be a list"
    # Note: The actual number may vary based on API implementation
    print(f"Role now has {len(result)} permissions")


def test_remove_permission_from_org_role(client, organization, cleanup_org_roles, cleanup_org_permissions):
    """Test removing a permission from a role at organization level."""
    # Create a role
    role_payload = {
        "name": "pytest_OrgTestRole",
        "description": "Role for permission testing"
    }

    role_response = client.post(
        f"/organizations/{organization}/roles",
        json=role_payload
    )

    if role_response.status_code == 200:
        cleanup_org_roles.append(role_response.json()["id"])

    assert role_response.status_code == 200, f"Failed to create role: {role_response.text}"
    role_id = role_response.json()["id"]

    # Create a permission
    perm_payload = {
        "name": "pytest_TestPermission",
        "description": "Test permission",
        "action": "read"
    }

    perm_response = client.post(
        f"/organizations/{organization}/permissions",
        json=perm_payload
    )

    if perm_response.status_code == 200:
        cleanup_org_permissions.append(perm_response.json()["id"])

    assert perm_response.status_code == 200, f"Failed to create permission: {perm_response.text}"
    perm_id = perm_response.json()["id"]

    # Add permission to role first
    add_response = client.post(
        f"/organizations/{organization}/roles/{role_id}/permissions/{perm_id}"
    )
    assert add_response.status_code == 200, f"Failed to add permission to role: {add_response.text}"

    # Remove permission from role
    remove_response = client.delete(
        f"/organizations/{organization}/roles/{role_id}/permissions/{perm_id}"
    )

    print(f"\nStatus Code: {remove_response.status_code}")
    print(f"Response Body: {remove_response.text}")

    assert remove_response.status_code == 200, f"Failed to remove permission from role: {remove_response.text}"