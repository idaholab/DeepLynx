import pytest
import uuid

def test_create_organization(client, cleanup_created_organizations):
    unique_name = f"organization create test {uuid.uuid4()}"
    response = client.post(
        "/organizations",
        json={"name": unique_name, "description": "Test organization"}
    )

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 200
    result = response.json()
    cleanup_created_organizations.append(result["id"])
    assert result["name"] == unique_name

def test_get_all_organizations(client, organization):
    response = client.get("/organizations")

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 200
    results = response.json()
    all_ids = [org["id"] for org in results]
    assert organization in all_ids, "organization not found"

def test_get_organization_by_id(client, organization):
    response = client.get(f"/organizations/{organization}")
    assert response.status_code == 200

    result = response.json()
    assert result["id"] == organization

def test_add_user_to_organization(client, cleanup_created_organizations, cleanup_current_user_added_to_organizations, current_user_id):
    unique_name = f"organization add user test {uuid.uuid4()}"
    create_response = client.post(
        "/organizations",
        json={"name": unique_name, "description": "Test organization"}
    )

    assert create_response.status_code == 200, f"Failed to create org: {create_response.text}"
    
    create_result = create_response.json()
    created_org_id = create_result["id"]
    cleanup_created_organizations.append(created_org_id)

    add_user_url = f"/organizations/{created_org_id}/user?userId={current_user_id}&isAdmin=true"
    add_user_response = client.post(add_user_url)

    if add_user_response.status_code == 200:
        cleanup_current_user_added_to_organizations.append(created_org_id)

    assert add_user_response.status_code == 200, (
        f"Add user to org failed with status {add_user_response.status_code}. "
        f"Response: {add_user_response.text}"
    )

def test_get_organizations_by_user(client, cleanup_created_organizations, cleanup_current_user_added_to_organizations, current_user_id):
    unique_name = f"organization user test {uuid.uuid4()}"
    create_response = client.post(
        "/organizations",
        json={"name": unique_name, "description": "Test organization"}
    )

    assert create_response.status_code == 200, f"Failed to create org: {create_response.text}"
    
    create_result = create_response.json()
    created_org_id = create_result["id"]
    cleanup_created_organizations.append(created_org_id)

    add_user_url = f"/organizations/{created_org_id}/user?userId={current_user_id}&isAdmin=true"
    add_user_response = client.post(add_user_url)

    if add_user_response.status_code == 200:
        cleanup_current_user_added_to_organizations.append(created_org_id)

    assert add_user_response.status_code == 200, (
        f"Add user to org failed with status {add_user_response.status_code}. "
        f"Response: {add_user_response.text}"
    )

    response = client.get(f"/organizations/user?userId={current_user_id}")
    results = response.json()

    assert response.status_code == 200
    organization_ids = [org["id"] for org in results]
    
    assert created_org_id in organization_ids, (
        f"Created organization {created_org_id} not found in user's organizations. "
        f"Found: {organization_ids}"
    )

def test_update_organization(client, cleanup_created_organizations):
    unique_name = f"organization update test {uuid.uuid4()}"
    # Create organization first
    create_response = client.post(
        "/organizations",
        json={"name": unique_name, "description": "Test organization"}
    )

    assert create_response.status_code == 200, f"Failed to create org: {create_response.text}"
    
    create_result = create_response.json()
    created_org_id = create_result["id"]
    cleanup_created_organizations.append(created_org_id)

    # Update the organization
    updated_name = f"updated organization name {uuid.uuid4()}"
    update_response = client.put(
        f"/organizations/{created_org_id}",
        json={
            "name": updated_name,
            "description": "updated description",
            "defaultOrg": False
        }
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200
    result = update_response.json()
    assert result["name"] == updated_name
    assert result["description"] == "updated description"

def test_delete_organization(client, cleanup_created_organizations):
    unique_name = f"organization delete test {uuid.uuid4()}"
    # Create organization first
    create_response = client.post(
        "/organizations",
        json={"name": unique_name, "description": "Test organization"}
    )

    assert create_response.status_code == 200, f"Failed to create org: {create_response.text}"
    
    create_result = create_response.json()
    created_org_id = create_result["id"]

    # Delete the organization
    delete_response = client.delete(f"/organizations/{created_org_id}")

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200
    
    # Verify response contains deletion message
    if delete_response.status_code == 200 and delete_response.text:
        result = delete_response.json()
        assert "message" in result or result == {}, (
            f"Unexpected response format: {delete_response.text}"
        )

    # Verify it's deleted/archived (may return 500 or 404 depending on soft vs hard delete)
    get_response = client.get(f"/organizations/{created_org_id}")
    assert get_response.status_code in [404, 500], (
        f"Expected 404 or 500 for deleted org, got {get_response.status_code}: {get_response.text}"
    )

def test_patch_organization(client, cleanup_created_organizations):
    unique_name = f"organization patch test {uuid.uuid4()}"
    # Create organization first
    create_response = client.post(
        "/organizations",
        json={"name": unique_name, "description": "Test organization"}
    )

    assert create_response.status_code == 200, f"Failed to create org: {create_response.text}"
    
    create_result = create_response.json()
    created_org_id = create_result["id"]
    cleanup_created_organizations.append(created_org_id)

    # PATCH appears to be used for archiving/unarchiving
    # First archive the organization
    patch_archive_response = client.patch(
        f"/organizations/{created_org_id}?archive=true"
    )

    print(f"\nArchive Status Code: {patch_archive_response.status_code}")
    print(f"Archive Response Body: {patch_archive_response.text}")

    assert patch_archive_response.status_code == 200, f"PATCH archive failed: {patch_archive_response.text}"

    # Then unarchive it
    patch_unarchive_response = client.patch(
        f"/organizations/{created_org_id}?archive=false"
    )

    print(f"\nUnarchive Status Code: {patch_unarchive_response.status_code}")
    print(f"Unarchive Response Body: {patch_unarchive_response.text}")

    assert patch_unarchive_response.status_code == 200, f"PATCH unarchive failed: {patch_unarchive_response.text}"

def test_remove_user_from_organization(client, cleanup_created_organizations, cleanup_current_user_added_to_organizations, current_user_id):
    unique_name = f"organization remove user test {uuid.uuid4()}"
    # Create organization
    create_response = client.post(
        "/organizations",
        json={"name": unique_name, "description": "Test organization"}
    )

    assert create_response.status_code == 200, f"Failed to create org: {create_response.text}"
    
    create_result = create_response.json()
    created_org_id = create_result["id"]
    cleanup_created_organizations.append(created_org_id)

    # Add user to organization
    add_user_url = f"/organizations/{created_org_id}/user?userId={current_user_id}&isAdmin=true"
    add_user_response = client.post(add_user_url)

    assert add_user_response.status_code == 200

    # Remove user from organization
    remove_user_url = f"/organizations/{created_org_id}/user?userId={current_user_id}"
    remove_user_response = client.delete(remove_user_url)

    print(f"\nStatus Code: {remove_user_response.status_code}")
    print(f"Response Body: {remove_user_response.text}")

    assert remove_user_response.status_code == 200

def test_update_organization_admin(client, cleanup_created_organizations, cleanup_current_user_added_to_organizations, current_user_id):
    unique_name = f"organization admin test {uuid.uuid4()}"
    # Create organization
    create_response = client.post(
        "/organizations",
        json={"name": unique_name, "description": "Test organization"}
    )

    assert create_response.status_code == 200, f"Failed to create org: {create_response.text}"
    
    create_result = create_response.json()
    created_org_id = create_result["id"]
    cleanup_created_organizations.append(created_org_id)

    # Add user to organization as non-admin
    add_user_url = f"/organizations/{created_org_id}/user?userId={current_user_id}&isAdmin=false"
    add_user_response = client.post(add_user_url)

    if add_user_response.status_code == 200:
        cleanup_current_user_added_to_organizations.append(created_org_id)

    assert add_user_response.status_code == 200

    # Update user to admin
    update_admin_url = f"/organizations/{created_org_id}/admin?userId={current_user_id}&isAdmin=true"
    update_admin_response = client.put(update_admin_url)

    print(f"\nStatus Code: {update_admin_response.status_code}")
    print(f"Response Body: {update_admin_response.text}")

    assert update_admin_response.status_code == 200