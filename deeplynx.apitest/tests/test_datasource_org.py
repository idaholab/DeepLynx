"""Tests for Datasource API endpoints - Organization Level."""

# ========================================================================
# ORGANIZATION-LEVEL TESTS
# ========================================================================

def test_create_datasource_org(client, organization, cleanup_org_datasources):
    """Test creating a single datasource at an organization level."""
    payload = {
        "name": "pytest_OrgTestDatasource",
        "description": "A test datasource for organization-level API testing"
    }

    response = client.post(
        f"/organizations/{organization}/datasources",
        json=payload
    )

    # Register for cleanup IMMEDIATELY, before assertions
    if response.status_code == 200:
        cleanup_org_datasources.append(response.json()["id"])

    # DEBUG: Print the actual response
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert result["name"] == "pytest_OrgTestDatasource"
    assert result["description"] == payload["description"]


def test_get_all_datasources_org(client, organization, cleanup_org_datasources):
    """Test retrieving all datasources at organization level"""
    payload = {
        "name": "pytest_OrgTestDatasource",
        "description": "A test datasource for organization-level API testing"
    }

    # Create a datasource
    create_response = client.post(
        f"/organizations/{organization}/datasources",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_datasources.append(create_response.json()["id"])

    # Verify creation succeeded
    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_datasource = create_response.json()
    created_id = created_datasource["id"]

    # Get all datasources
    get_response = client.get(
        f"/organizations/{organization}/datasources"
    )

    # Verify GET succeeded
    assert get_response.status_code == 200, f"Failed to get datasources: {get_response.text}"
    
    all_datasources = get_response.json()
    assert isinstance(all_datasources, list), "Expected response to be a list"
    
    datasource_ids = [ds["id"] for ds in all_datasources]
    assert created_id in datasource_ids, f"Created datasource {created_id} not found in list of datasources"
    
    our_datasource = next((ds for ds in all_datasources if ds["id"] == created_id), None)
    assert our_datasource is not None, f"Could not find datasource with id {created_id}"
    assert our_datasource["name"] == "pytest_OrgTestDatasource"
    assert our_datasource["description"] == "A test datasource for organization-level API testing"


def test_get_datasource_org(client, organization, cleanup_org_datasources):
    """Test retrieving a single datasource by ID at organization level"""
    payload = {
        "name": "pytest_OrgTestDatasource",
        "description": "A test datasource for organization-level API testing"
    }

    # Create a datasource
    create_response = client.post(
        f"/organizations/{organization}/datasources",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_datasources.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    # Get the specific datasource
    get_response = client.get(
        f"/organizations/{organization}/datasources/{created_id}"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get datasource: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_OrgTestDatasource"
    assert result["description"] == "A test datasource for organization-level API testing"


def test_update_datasource_org(client, organization, cleanup_org_datasources):
    """Test updating a datasource at organization level"""
    # Create a datasource
    create_payload = {
        "name": "pytest_OrgTestDatasource",
        "description": "Original description"
    }

    create_response = client.post(
        f"/organizations/{organization}/datasources",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_org_datasources.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    # Update the datasource
    update_payload = {
        "name": "pytest_OrgTestDatasource_Updated",
        "description": "Updated description"
    }

    update_response = client.put(
        f"/organizations/{organization}/datasources/{created_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update datasource: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_OrgTestDatasource_Updated"
    assert result["description"] == "Updated description"


def test_archive_and_unarchive_datasource_org(client, organization, cleanup_org_datasources):
    """Test archiving and unarchiving a datasource at organization level"""
    # Create a datasource
    payload = {
        "name": "pytest_OrgTestDatasource",
        "description": "A test datasource for archive/unarchive testing"
    }

    create_response = client.post(
        f"/organizations/{organization}/datasources",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_datasources.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    # Archive the datasource
    archive_response = client.patch(
        f"/organizations/{organization}/datasources/{created_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive datasource: {archive_response.text}"

    # Verify the datasource is archived
    get_archived_response = client.get(
        f"/organizations/{organization}/datasources/{created_id}",
        params={"hideArchived": "false"}
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived datasource: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(archived_result)
    assert archived_result.get("isArchived") == True, "Datasource should be archived"

    # Unarchive the datasource
    unarchive_response = client.patch(
        f"/organizations/{organization}/datasources/{created_id}",
        params={"archive": "false"}
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive datasource: {unarchive_response.text}"

    # Verify the datasource is unarchived
    get_unarchived_response = client.get(
        f"/organizations/{organization}/datasources/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived datasource: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Datasource should not be archived"


def test_delete_datasource_org(client, organization, cleanup_org_datasources):
    """Test permanently deleting a datasource at organization level"""
    # Create a datasource
    payload = {
        "name": "pytest_OrgTestDatasource",
        "description": "A test datasource for deletion"
    }

    create_response = client.post(
        f"/organizations/{organization}/datasources",
        json=payload
    )

    # Note: We still register for cleanup in case deletion fails
    if create_response.status_code == 200:
        cleanup_org_datasources.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    delete_response = client.delete(
        f"/organizations/{organization}/datasources/{created_id}"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete datasource: {delete_response.text}"

    # Verify the datasource is gone
    get_response = client.get(
        f"/organizations/{organization}/datasources/{created_id}"
    )
    
    # Get all datasources and confirm deleted datasource is not in the list
    all_datasources_response = client.get(f"/organizations/{organization}/datasources")
    assert all_datasources_response.status_code == 200, f"Failed to get all datasources: {all_datasources_response.text}"
    
    all_datasources = all_datasources_response.json()
    datasource_ids = [ds["id"] for ds in all_datasources]
    
    assert created_id not in datasource_ids, f"Deleted datasource {created_id} should not appear in list of all datasources"
    print(f"Confirmed: Datasource {created_id} not in list of {len(datasource_ids)} datasources")

# def test_get_default_datasource_org(client, organization, cleanup_org_classes):
#     """Test getting default Data Source Organization Level"""

#     # Get all datasources
#     get_response = client.get(
#         f"/organizations/{organization}/datasources/default"
#     )

#     # Verify GET succeeded
#     assert get_response.status_code == 200, f"Failed to get datasources: {get_response.text}"
    
#     all_datasources = get_response.json()
#     assert isinstance(all_datasources, list), "Expected response to be a list"
    