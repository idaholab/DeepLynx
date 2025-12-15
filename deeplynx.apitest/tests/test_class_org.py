"""Tests for Class API endpoints."""

# ========================================================================
# ORGANIZATION-LEVEL TESTS
# ========================================================================
    
def test_create_org_class(client, organization, cleanup_org_classes):
    """Test creating a single class at an organization level."""
    payload = {
        "name": "pytest_OrgTestClass",
        "description": "A test class for organization-level API testing"
    }

    response = client.post(
        f"/organizations/{organization}/classes",
        json=payload
    )

    # Register for cleanup IMMEDIATELY, before assertions
    if response.status_code == 200:
        cleanup_org_classes.append(response.json()["id"])

    # DEBUG: Print the actual response
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert result["name"] == "pytest_OrgTestClass"
    assert result["description"] == payload["description"]

def test_bulk_create_classes_org(client, organization, cleanup_org_classes):
    """Test bulk creating classes at an organization level"""
    payload = [
            {
                "name": "OrgBulkTestClass1",
                "description": "First org bulk test class",
                "uuid": "org-bulk-class-uuid-001"
            },
            {
                "name": "OrgBulkTestClass2",
                "description": "Second org bulk test class",
                "uuid": "org-bulk-class-uuid-002"
            }
        ]
    
    response = client.post(
        f"/organizations/{organization}/classes/bulk",
        json=payload
    )

    if response.status_code == 200:
          results = response.json()
          cleanup_org_classes.extend([results[0]["id"], results[1]["id"]])

    # DEBUG: Print the actual response
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")    

    assert response.status_code == 200, f"Request failed: {response.text}"
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")    

    assert response.status_code == 200, f"Request failed: {response.text}"
    
    results = response.json()
    
    # Check first class
    result_1 = results[0]
    assert result_1["name"] == "OrgBulkTestClass1"
    assert result_1["description"] == "First org bulk test class"
    assert result_1["uuid"] == "org-bulk-class-uuid-001"

    # Check second class
    result_2 = results[1]
    assert result_2["name"] == "OrgBulkTestClass2"
    assert result_2["description"] == "Second org bulk test class"
    assert result_2["uuid"] == "org-bulk-class-uuid-002"

def test_get_all_classes_org(client, organization, cleanup_org_classes):
    """Test retrieving all classes at organization level"""
    payload = {
        "name": "pytest_OrgTestClass",
        "description": "A test class for organization-level API testing"
    }

    # Create a class
    create_response = client.post(
        f"/organizations/{organization}/classes",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_classes.append(create_response.json()["id"])

    # Verify creation succeeded
    assert create_response.status_code == 200, f"Failed to create class: {create_response.text}"
    created_class = create_response.json()
    created_id = created_class["id"]

    # Get all classes
    get_response = client.get(
        f"/organizations/{organization}/classes"
    )

    # Verify GET succeeded
    assert get_response.status_code == 200, f"Failed to get classes: {get_response.text}"
    
    all_classes = get_response.json()
    assert isinstance(all_classes, list), "Expected response to be a list"
    
    class_ids = [cls["id"] for cls in all_classes]
    assert created_id in class_ids, f"Created class {created_id} not found in list of classes"
    
    our_class = next((cls for cls in all_classes if cls["id"] == created_id), None)
    assert our_class is not None, f"Could not find class with id {created_id}"
    assert our_class["name"] == "pytest_OrgTestClass"
    assert our_class["description"] == "A test class for organization-level API testing"

def test_get_class_org(client, organization, cleanup_org_classes):
    """Test retrieving a single class by ID at organization level"""
    payload = {
        "name": "pytest_OrgTestClass",
        "description": "A test class for organization-level API testing"
    }

    # Create a class
    create_response = client.post(
        f"/organizations/{organization}/classes",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_classes.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create class: {create_response.text}"
    created_id = create_response.json()["id"]

    # Get the specific class
    get_response = client.get(
        f"/organizations/{organization}/classes/{created_id}"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get class: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_OrgTestClass"
    assert result["description"] == "A test class for organization-level API testing"


def test_update_class_org(client, organization, cleanup_org_classes):
    """Test updating a class at organization level"""
    # Create a class
    create_payload = {
        "name": "pytest_OrgTestClass",
        "description": "Original description"
    }

    create_response = client.post(
        f"/organizations/{organization}/classes",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_org_classes.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create class: {create_response.text}"
    created_id = create_response.json()["id"]

    # Update the class
    update_payload = {
        "name": "pytest_OrgTestClass_Updated",
        "description": "Updated description"
    }

    update_response = client.put(
        f"/organizations/{organization}/classes/{created_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update class: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_OrgTestClass_Updated"
    assert result["description"] == "Updated description"


def test_archive_and_unarchive_class_org(client, organization, cleanup_org_classes):
    """Test archiving and unarchiving a class at organization level"""
    # Create a class
    payload = {
        "name": "pytest_OrgTestClass",
        "description": "A test class for archive/unarchive testing"
    }

    create_response = client.post(
        f"/organizations/{organization}/classes",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_classes.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create class: {create_response.text}"
    created_id = create_response.json()["id"]

    # Archive the class
    archive_response = client.patch(
        f"/organizations/{organization}/classes/{created_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive class: {archive_response.text}"

    # Verify the class is archived
    get_archived_response = client.get(
        f"/organizations/{organization}/classes/{created_id}",
        params={"hideArchived": "false"}
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived class: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(archived_result)
    assert archived_result.get("isArchived") == True, "Class should be archived"

    # Unarchive the class
    unarchive_response = client.patch(
        f"/organizations/{organization}/classes/{created_id}",
        params={"archive": "false"}
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive class: {unarchive_response.text}"

    # Verify the class is unarchived
    get_unarchived_response = client.get(
        f"/organizations/{organization}/classes/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived class: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Class should not be archived"


def test_delete_class_org(client, organization, cleanup_org_classes):
    """Test permanently deleting a class at organization level"""
    # Create a class
    payload = {
        "name": "pytest_OrgTestClass",
        "description": "A test class for deletion"
    }

    create_response = client.post(
        f"/organizations/{organization}/classes",
        json=payload
    )

    # Note: We still register for cleanup in case deletion fails
    if create_response.status_code == 200:
        cleanup_org_classes.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create class: {create_response.text}"
    created_id = create_response.json()["id"]

    delete_response = client.delete(
        f"/organizations/{organization}/classes/{created_id}"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete class: {delete_response.text}"

    # Verify the class is gone
    get_response = client.get(
        f"/organizations/{organization}/classes/{created_id}"
    )
    
    # Get all classes and confirm deleted class is not in the list
    all_classes_response = client.get(f"/organizations/{organization}/classes")
    assert all_classes_response.status_code == 200, f"Failed to get all classes: {all_classes_response.text}"
    
    all_classes = all_classes_response.json()
    class_ids = [cls["id"] for cls in all_classes]
    
    assert created_id not in class_ids, f"Deleted class {created_id} should not appear in list of all classes"
    print(f"Confirmed: Class {created_id} not in list of {len(class_ids)} classes")
