"""Tests for Relationship API endpoints at project level."""

import pytest

# ========================================================================
# PROJECT-LEVEL TESTS
# ========================================================================

def test_create_project_relationship(client, organization, project, origin_class, destination_class, test_relationship_project, cleanup_project_relationships):
    """Test creating a single relationship at project level."""
    payload = {
        "name": "pytest_ProjectTestRelationship",
        "description": "A test relationship for project-level API testing",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    response = client.post(
        f"/projects/{project}/relationships",
        json=payload
    )

    # Register for cleanup IMMEDIATELY, before assertions
    if response.status_code == 200:
        cleanup_project_relationships.append(response.json()["id"])

    # DEBUG: Print the actual response
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert result["name"] == "pytest_ProjectTestRelationship"
    assert result["description"] == payload["description"]
    assert result["originId"] == origin_class
    assert result["destinationId"] == destination_class


def test_bulk_create_relationships_project(client, organization, project, origin_class, destination_class, cleanup_project_relationships):
    """Test bulk creating relationships at project level."""
    payload = [
        {
            "name": "ProjectBulkTestRelationship1",
            "description": "First project bulk test relationship",
            "uuid": "project-bulk-rel-uuid-001",
            "origin_id": origin_class,
            "destination_id": destination_class
        },
        {
            "name": "ProjectBulkTestRelationship2",
            "description": "Second project bulk test relationship",
            "uuid": "project-bulk-rel-uuid-002",
            "origin_id": origin_class,
            "destination_id": destination_class
        }
    ]
    
    response = client.post(
        f"/projects/{project}/relationships/bulk",
        json=payload
    )

    if response.status_code == 200:
        results = response.json()
        cleanup_project_relationships.extend([results[0]["id"], results[1]["id"]])

    # DEBUG: Print the actual response
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 200, f"Request failed: {response.text}"
    
    results = response.json()
    
    # Check first relationship
    result_1 = results[0]
    assert result_1["name"] == "ProjectBulkTestRelationship1"
    assert result_1["description"] == "First project bulk test relationship"
    assert result_1["uuid"] == "project-bulk-rel-uuid-001"

    # Check second relationship
    result_2 = results[1]
    assert result_2["name"] == "ProjectBulkTestRelationship2"
    assert result_2["description"] == "Second project bulk test relationship"
    assert result_2["uuid"] == "project-bulk-rel-uuid-002"


def test_get_all_relationships_project(client, organization, project, origin_class, destination_class, cleanup_project_relationships):
    """Test retrieving all relationships at project level."""
    payload = {
        "name": "pytest_ProjectTestRelationship",
        "description": "A test relationship for project-level API testing",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    # Create a relationship
    create_response = client.post(
        f"/projects/{project}/relationships",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_relationships.append(create_response.json()["id"])

    # Verify creation succeeded
    assert create_response.status_code == 200, f"Failed to create relationship: {create_response.text}"
    created_relationship = create_response.json()
    created_id = created_relationship["id"]

    # Get all relationships
    get_response = client.get(
        f"/projects/{project}/relationships"
    )

    # Verify GET succeeded
    assert get_response.status_code == 200, f"Failed to get relationships: {get_response.text}"
    
    all_relationships = get_response.json()
    assert isinstance(all_relationships, list), "Expected response to be a list"
    
    relationship_ids = [rel["id"] for rel in all_relationships]
    assert created_id in relationship_ids, f"Created relationship {created_id} not found in list of relationships"
    
    our_relationship = next((rel for rel in all_relationships if rel["id"] == created_id), None)
    assert our_relationship is not None, f"Could not find relationship with id {created_id}"
    assert our_relationship["name"] == "pytest_ProjectTestRelationship"
    assert our_relationship["description"] == "A test relationship for project-level API testing"
    assert our_relationship["originId"] == origin_class
    assert our_relationship["destinationId"] == destination_class


def test_get_relationship_project(client, organization, project, origin_class, destination_class, cleanup_project_relationships):
    """Test retrieving a single relationship by ID at project level."""
    payload = {
        "name": "pytest_ProjectTestRelationship",
        "description": "A test relationship for project-level API testing",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    # Create a relationship
    create_response = client.post(
        f"/projects/{project}/relationships",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_relationships.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create relationship: {create_response.text}"
    created_id = create_response.json()["id"]

    # Get the specific relationship
    get_response = client.get(
        f"/projects/{project}/relationships/{created_id}"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get relationship: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_ProjectTestRelationship"
    assert result["description"] == "A test relationship for project-level API testing"
    assert result["originId"] == origin_class
    assert result["destinationId"] == destination_class


def test_update_relationship_project(client, organization, project, origin_class, destination_class, cleanup_project_relationships):
    """Test updating a relationship at project level."""
    # Create a relationship
    create_payload = {
        "name": "pytest_ProjectTestRelationship",
        "description": "Original description",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    create_response = client.post(
        f"/projects/{project}/relationships",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_project_relationships.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create relationship: {create_response.text}"
    created_id = create_response.json()["id"]

    # Update the relationship
    update_payload = {
        "name": "pytest_ProjectTestRelationship_Updated",
        "description": "Updated description"
    }

    update_response = client.put(
        f"/projects/{project}/relationships/{created_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update relationship: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_ProjectTestRelationship_Updated"
    assert result["description"] == "Updated description"


def test_archive_and_unarchive_relationship_project(client, organization, project, origin_class, destination_class, cleanup_project_relationships):
    """Test archiving and unarchiving a relationship at project level."""
    # Create a relationship
    payload = {
        "name": "pytest_ProjectTestRelationship",
        "description": "A test relationship for archive/unarchive testing",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    create_response = client.post(
        f"/projects/{project}/relationships",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_relationships.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create relationship: {create_response.text}"
    created_id = create_response.json()["id"]

    # Archive the relationship
    archive_response = client.patch(
        f"/projects/{project}/relationships/{created_id}?archive=true"
    )
    assert archive_response.status_code == 200, f"Failed to archive relationship: {archive_response.text}"

    # Verify the relationship is archived
    get_archived_response = client.get(
        f"/projects/{project}/relationships/{created_id}?hideArchived=false"
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived relationship: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(archived_result)
    assert archived_result.get("isArchived") == True, "Relationship should be archived"

    # Unarchive the relationship
    unarchive_response = client.patch(
        f"/projects/{project}/relationships/{created_id}?archive=false"
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive relationship: {unarchive_response.text}"

    # Verify the relationship is unarchived
    get_unarchived_response = client.get(
        f"/projects/{project}/relationships/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived relationship: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Relationship should not be archived"


def test_delete_relationship_project(client, organization, project, origin_class, destination_class, cleanup_project_relationships):
    """Test permanently deleting a relationship at project level."""
    # Create a relationship
    payload = {
        "name": "pytest_ProjectTestRelationship",
        "description": "A test relationship for deletion",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    create_response = client.post(
        f"/projects/{project}/relationships",
        json=payload
    )

    # Note: We still register for cleanup in case deletion fails
    if create_response.status_code == 200:
        cleanup_project_relationships.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create relationship: {create_response.text}"
    created_id = create_response.json()["id"]

    delete_response = client.delete(
        f"/projects/{project}/relationships/{created_id}"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete relationship: {delete_response.text}"

    # Verify the relationship is gone
    # Get all relationships and confirm deleted relationship is not in the list
    all_relationships_response = client.get(f"/projects/{project}/relationships")
    assert all_relationships_response.status_code == 200, f"Failed to get all relationships: {all_relationships_response.text}"
    
    all_relationships = all_relationships_response.json()
    relationship_ids = [rel["id"] for rel in all_relationships]
    
    assert created_id not in relationship_ids, f"Deleted relationship {created_id} should not appear in list of all relationships"
    print(f"Confirmed: Relationship {created_id} not in list of {len(relationship_ids)} relationships")