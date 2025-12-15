"""Tests for Relationship API endpoints at organization level."""

import pytest

# ========================================================================
# ORGANIZATION-LEVEL TESTS
# ========================================================================

def test_create_org_relationship(client, organization, origin_class, destination_class, test_relationship_org, cleanup_org_relationships):
    """Test creating a single relationship at organization level."""
    payload = {
        "name": "pytest_OrgTestRelationship",
        "description": "A test relationship for organization-level API testing",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    response = client.post(
        f"/organizations/{organization}/relationships",
        json=payload
    )

    # Register for cleanup IMMEDIATELY, before assertions
    if response.status_code == 200:
        cleanup_org_relationships.append(response.json()["id"])

    # DEBUG: Print the actual response
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert result["name"] == "pytest_OrgTestRelationship"
    assert result["description"] == payload["description"]
    assert result["originId"] == origin_class
    assert result["destinationId"] == destination_class


def test_bulk_create_relationships_org(client, organization, origin_class, destination_class, cleanup_org_relationships):
    """Test bulk creating relationships at organization level."""
    payload = [
        {
            "name": "OrgBulkTestRelationship1",
            "description": "First org bulk test relationship",
            "uuid": "org-bulk-rel-uuid-001",
            "origin_id": origin_class,
            "destination_id": destination_class
        },
        {
            "name": "OrgBulkTestRelationship2",
            "description": "Second org bulk test relationship",
            "uuid": "org-bulk-rel-uuid-002",
            "origin_id": origin_class,
            "destination_id": destination_class
        }
    ]
    
    response = client.post(
        f"/organizations/{organization}/relationships/bulk",
        json=payload
    )

    if response.status_code == 200:
        results = response.json()
        cleanup_org_relationships.extend([results[0]["id"], results[1]["id"]])

    # DEBUG: Print the actual response
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 200, f"Request failed: {response.text}"
    
    results = response.json()
    
    # Check first relationship
    result_1 = results[0]
    assert result_1["name"] == "OrgBulkTestRelationship1"
    assert result_1["description"] == "First org bulk test relationship"
    assert result_1["uuid"] == "org-bulk-rel-uuid-001"

    # Check second relationship
    result_2 = results[1]
    assert result_2["name"] == "OrgBulkTestRelationship2"
    assert result_2["description"] == "Second org bulk test relationship"
    assert result_2["uuid"] == "org-bulk-rel-uuid-002"


def test_get_all_relationships_org(client, organization, origin_class, destination_class, cleanup_org_relationships):
    """Test retrieving all relationships at organization level."""
    payload = {
        "name": "pytest_OrgTestRelationship",
        "description": "A test relationship for organization-level API testing",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    # Create a relationship
    create_response = client.post(
        f"/organizations/{organization}/relationships",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_relationships.append(create_response.json()["id"])

    # Verify creation succeeded
    assert create_response.status_code == 200, f"Failed to create relationship: {create_response.text}"
    created_relationship = create_response.json()
    created_id = created_relationship["id"]

    # Get all relationships
    get_response = client.get(
        f"/organizations/{organization}/relationships"
    )

    # Verify GET succeeded
    assert get_response.status_code == 200, f"Failed to get relationships: {get_response.text}"
    
    all_relationships = get_response.json()
    assert isinstance(all_relationships, list), "Expected response to be a list"
    
    relationship_ids = [rel["id"] for rel in all_relationships]
    assert created_id in relationship_ids, f"Created relationship {created_id} not found in list of relationships"
    
    our_relationship = next((rel for rel in all_relationships if rel["id"] == created_id), None)
    assert our_relationship is not None, f"Could not find relationship with id {created_id}"
    assert our_relationship["name"] == "pytest_OrgTestRelationship"
    assert our_relationship["description"] == "A test relationship for organization-level API testing"
    assert our_relationship["originId"] == origin_class
    assert our_relationship["destinationId"] == destination_class


def test_get_relationship_org(client, organization, origin_class, destination_class, cleanup_org_relationships):
    """Test retrieving a single relationship by ID at organization level."""
    payload = {
        "name": "pytest_OrgTestRelationship",
        "description": "A test relationship for organization-level API testing",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    # Create a relationship
    create_response = client.post(
        f"/organizations/{organization}/relationships",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_relationships.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create relationship: {create_response.text}"
    created_id = create_response.json()["id"]

    # Get the specific relationship
    get_response = client.get(
        f"/organizations/{organization}/relationships/{created_id}"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get relationship: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_OrgTestRelationship"
    assert result["description"] == "A test relationship for organization-level API testing"
    assert result["originId"] == origin_class
    assert result["destinationId"] == destination_class


def test_update_relationship_org(client, organization, origin_class, destination_class, cleanup_org_relationships):
    """Test updating a relationship at organization level."""
    # Create a relationship
    create_payload = {
        "name": "pytest_OrgTestRelationship",
        "description": "Original description",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    create_response = client.post(
        f"/organizations/{organization}/relationships",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_org_relationships.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create relationship: {create_response.text}"
    created_id = create_response.json()["id"]

    # Update the relationship
    update_payload = {
        "name": "pytest_OrgTestRelationship_Updated",
        "description": "Updated description"
    }

    update_response = client.put(
        f"/organizations/{organization}/relationships/{created_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update relationship: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_OrgTestRelationship_Updated"
    assert result["description"] == "Updated description"


def test_archive_and_unarchive_relationship_org(client, organization, origin_class, destination_class, cleanup_org_relationships):
    """Test archiving and unarchiving a relationship at organization level."""
    # Create a relationship
    payload = {
        "name": "pytest_OrgTestRelationship",
        "description": "A test relationship for archive/unarchive testing",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    create_response = client.post(
        f"/organizations/{organization}/relationships",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_relationships.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create relationship: {create_response.text}"
    created_id = create_response.json()["id"]

    # Archive the relationship
    archive_response = client.patch(
        f"/organizations/{organization}/relationships/{created_id}?archive=true"
    )
    assert archive_response.status_code == 200, f"Failed to archive relationship: {archive_response.text}"

    # Verify the relationship is archived
    get_archived_response = client.get(
        f"/organizations/{organization}/relationships/{created_id}?hideArchived=false"
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived relationship: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(archived_result)
    assert archived_result.get("isArchived") == True, "Relationship should be archived"

    # Unarchive the relationship
    unarchive_response = client.patch(
        f"/organizations/{organization}/relationships/{created_id}?archive=false"
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive relationship: {unarchive_response.text}"

    # Verify the relationship is unarchived
    get_unarchived_response = client.get(
        f"/organizations/{organization}/relationships/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived relationship: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Relationship should not be archived"


def test_delete_relationship_org(client, organization, origin_class, destination_class, cleanup_org_relationships):
    """Test permanently deleting a relationship at organization level."""
    # Create a relationship
    payload = {
        "name": "pytest_OrgTestRelationship",
        "description": "A test relationship for deletion",
        "origin_id": origin_class,
        "destination_id": destination_class
    }

    create_response = client.post(
        f"/organizations/{organization}/relationships",
        json=payload
    )

    # Note: We still register for cleanup in case deletion fails
    if create_response.status_code == 200:
        cleanup_org_relationships.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create relationship: {create_response.text}"
    created_id = create_response.json()["id"]

    delete_response = client.delete(
        f"/organizations/{organization}/relationships/{created_id}"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete relationship: {delete_response.text}"

    # Verify the relationship is gone
    # Get all relationships and confirm deleted relationship is not in the list
    all_relationships_response = client.get(f"/organizations/{organization}/relationships")
    assert all_relationships_response.status_code == 200, f"Failed to get all relationships: {all_relationships_response.text}"
    
    all_relationships = all_relationships_response.json()
    relationship_ids = [rel["id"] for rel in all_relationships]
    
    assert created_id not in relationship_ids, f"Deleted relationship {created_id} should not appear in list of all relationships"
    print(f"Confirmed: Relationship {created_id} not in list of {len(relationship_ids)} relationships")