"""Tests for Datasource API endpoints - project Level."""

# ========================================================================
# PROJECT-LEVEL TESTS
# ========================================================================

def test_create_datasource_project(client, project, cleanup_project_datasources):
    """Test creating a single datasource at an project level."""
    payload = {
        "name": "pytest_projectTestDatasource",
        "description": "A test datasource for project-level API testing"
    }

    response = client.post(
        f"/projects/{project}/datasources",
        json=payload
    )

    if response.status_code == 200:
        cleanup_project_datasources.append(response.json()["id"])

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert result["name"] == "pytest_projectTestDatasource"
    assert result["description"] == payload["description"]


def test_get_all_datasources_project(client, project, cleanup_project_datasources):
    """Test retrieving all datasources at project level"""
    payload = {
        "name": "pytest_projectTestDatasource",
        "description": "A test datasource for project-level API testing"
    }

    create_response = client.post(
        f"/projects/{project}/datasources",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_datasources.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_datasource = create_response.json()
    created_id = created_datasource["id"]

    get_response = client.get(
        f"/projects/{project}/datasources"
    )

    assert get_response.status_code == 200, f"Failed to get datasources: {get_response.text}"
    
    all_datasources = get_response.json()
    assert isinstance(all_datasources, list), "Expected response to be a list"
    
    datasource_ids = [ds["id"] for ds in all_datasources]
    assert created_id in datasource_ids, f"Created datasource {created_id} not found in list of datasources"
    
    our_datasource = next((ds for ds in all_datasources if ds["id"] == created_id), None)
    assert our_datasource is not None, f"Could not find datasource with id {created_id}"
    assert our_datasource["name"] == "pytest_projectTestDatasource"
    assert our_datasource["description"] == "A test datasource for project-level API testing"


def test_get_datasource_project(client, project, cleanup_project_datasources):
    """Test retrieving a single datasource by ID at project level"""
    payload = {
        "name": "pytest_projectTestDatasource",
        "description": "A test datasource for project-level API testing"
    }

    create_response = client.post(
        f"/projects/{project}/datasources",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_datasources.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    get_response = client.get(
        f"/projects/{project}/datasources/{created_id}"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get datasource: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_projectTestDatasource"
    assert result["description"] == "A test datasource for project-level API testing"


def test_update_datasource_project(client, project, cleanup_project_datasources):
    """Test updating a datasource at project level"""

    create_payload = {
        "name": "pytest_projectTestDatasource",
        "description": "Original description"
    }

    create_response = client.post(
        f"/projects/{project}/datasources",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_project_datasources.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    update_payload = {
        "name": "pytest_projectTestDatasource_Updated",
        "description": "Updated description"
    }

    update_response = client.put(
        f"/projects/{project}/datasources/{created_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update datasource: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_projectTestDatasource_Updated"
    assert result["description"] == "Updated description"


def test_archive_and_unarchive_datasource_project(client, project, cleanup_project_datasources):
    """Test archiving and unarchiving a datasource at project level"""

    payload = {
        "name": "pytest_projectTestDatasource",
        "description": "A test datasource for archive/unarchive testing"
    }

    create_response = client.post(
        f"/projects/{project}/datasources",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_datasources.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    archive_response = client.patch(
        f"/projects/{project}/datasources/{created_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive datasource: {archive_response.text}"

    get_archived_response = client.get(
        f"/projects/{project}/datasources/{created_id}",
        params={"hideArchived": "false"}
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived datasource: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(archived_result)
    assert archived_result.get("isArchived") == True, "Datasource should be archived"

    unarchive_response = client.patch(
        f"/projects/{project}/datasources/{created_id}",
        params={"archive": "false"}
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive datasource: {unarchive_response.text}"

    get_unarchived_response = client.get(
        f"/projects/{project}/datasources/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived datasource: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Datasource should not be archived"


def test_delete_datasource_project(client, project, cleanup_project_datasources):
    """Test permanently deleting a datasource at project level"""

    payload = {
        "name": "pytest_projectTestDatasource",
        "description": "A test datasource for deletion"
    }

    create_response = client.post(
        f"/projects/{project}/datasources",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_datasources.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    delete_response = client.delete(
        f"/projects/{project}/datasources/{created_id}"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete datasource: {delete_response.text}"

    get_response = client.get(
        f"/projects/{project}/datasources/{created_id}"
    )
    
    all_datasources_response = client.get(f"/projects/{project}/datasources")
    assert all_datasources_response.status_code == 200, f"Failed to get all datasources: {all_datasources_response.text}"
    
    all_datasources = all_datasources_response.json()
    datasource_ids = [ds["id"] for ds in all_datasources]
    
    assert created_id not in datasource_ids, f"Deleted datasource {created_id} should not appear in list of all datasources"
    print(f"Confirmed: Datasource {created_id} not in list of {len(datasource_ids)} datasources")

# def test_get_default_datasource_project(client, project, cleanup_project_classes):
#     """Test getting default Data Source project Level"""

#     get_response = client.get(
#         f"/projects/{project}/datasources/default"
#     )

#     assert get_response.status_code == 200, f"Failed to get datasources: {get_response.text}"
    
#     all_datasources = get_response.json()
#     assert isinstance(all_datasources, list), "Expected response to be a list"
    