"""Tests for Class API endpoints."""

# ========================================================================
# PROJECT-LEVEL TESTS
# ========================================================================
    
def test_create_project_class(client, project, cleanup_project_classes):
    """Test creating a single class at an project level."""
    payload = {
        "name": "pytest_projectTestClass",
        "description": "A test class for project-level API testing"
    }

    response = client.post(
        f"/projects/{project}/classes",
        json=payload
    )

    # Register for cleanup IMMEDIATELY, before assertions
    if response.status_code == 200:
        cleanup_project_classes.append(response.json()["id"])

    # DEBUG: Print the actual response
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert result["name"] == "pytest_projectTestClass"
    assert result["description"] == payload["description"]

def test_bulk_create_classes_project(client, project, cleanup_project_classes):
    """Test bulk creating classes at an project level"""
    payload = [
            {
                "name": "projectBulkTestClass1",
                "description": "First project bulk test class",
                "uuid": "project-bulk-class-uuid-001"
            },
            {
                "name": "projectBulkTestClass2",
                "description": "Second project bulk test class",
                "uuid": "project-bulk-class-uuid-002"
            }
        ]
    
    response = client.post(
        f"/projects/{project}/classes/bulk",
        json=payload
    )

    if response.status_code == 200:
          results = response.json()
          cleanup_project_classes.extend([results[0]["id"], results[1]["id"]])

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
    assert result_1["name"] == "projectBulkTestClass1"
    assert result_1["description"] == "First project bulk test class"
    assert result_1["uuid"] == "project-bulk-class-uuid-001"

    # Check second class
    result_2 = results[1]
    assert result_2["name"] == "projectBulkTestClass2"
    assert result_2["description"] == "Second project bulk test class"
    assert result_2["uuid"] == "project-bulk-class-uuid-002"

def test_get_all_classes_project(client, project, cleanup_project_classes):
    """Test retrieving all classes at project level"""
    payload = {
        "name": "pytest_projectTestClass",
        "description": "A test class for project-level API testing"
    }

    # Create a class
    create_response = client.post(
        f"/projects/{project}/classes",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_classes.append(create_response.json()["id"])

    # Verify creation succeeded
    assert create_response.status_code == 200, f"Failed to create class: {create_response.text}"
    created_class = create_response.json()
    created_id = created_class["id"]

    # Get all classes
    get_response = client.get(
        f"/projects/{project}/classes"
    )

    # Verify GET succeeded
    assert get_response.status_code == 200, f"Failed to get classes: {get_response.text}"
    
    all_classes = get_response.json()
    assert isinstance(all_classes, list), "Expected response to be a list"
    
    class_ids = [cls["id"] for cls in all_classes]
    assert created_id in class_ids, f"Created class {created_id} not found in list of classes"
    
    our_class = next((cls for cls in all_classes if cls["id"] == created_id), None)
    assert our_class is not None, f"Could not find class with id {created_id}"
    assert our_class["name"] == "pytest_projectTestClass"
    assert our_class["description"] == "A test class for project-level API testing"

def test_get_class_project(client, project, cleanup_project_classes):
    """Test retrieving a single class by ID at project level"""
    payload = {
        "name": "pytest_projectTestClass",
        "description": "A test class for project-level API testing"
    }

    # Create a class
    create_response = client.post(
        f"/projects/{project}/classes",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_classes.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create class: {create_response.text}"
    created_id = create_response.json()["id"]

    # Get the specific class
    get_response = client.get(
        f"/projects/{project}/classes/{created_id}"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get class: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_projectTestClass"
    assert result["description"] == "A test class for project-level API testing"


def test_update_class_project(client, project, cleanup_project_classes):
    """Test updating a class at project level"""
    # Create a class
    create_payload = {
        "name": "pytest_projectTestClass",
        "description": "Original description"
    }

    create_response = client.post(
        f"/projects/{project}/classes",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_project_classes.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create class: {create_response.text}"
    created_id = create_response.json()["id"]

    # Update the class
    update_payload = {
        "name": "pytest_projectTestClass_Updated",
        "description": "Updated description"
    }

    update_response = client.put(
        f"/projects/{project}/classes/{created_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update class: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_projectTestClass_Updated"
    assert result["description"] == "Updated description"


def test_archive_and_unarchive_class_project(client, project, cleanup_project_classes):
    """Test archiving and unarchiving a class at project level"""
    # Create a class
    payload = {
        "name": "pytest_projectTestClass",
        "description": "A test class for archive/unarchive testing"
    }

    create_response = client.post(
        f"/projects/{project}/classes",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_project_classes.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create class: {create_response.text}"
    created_id = create_response.json()["id"]

    # Archive the class
    archive_response = client.patch(
        f"/projects/{project}/classes/{created_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive class: {archive_response.text}"

    # Verify the class is archived
    get_archived_response = client.get(
        f"/projects/{project}/classes/{created_id}",
        params={"hideArchived": "false"}
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived class: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(archived_result)
    assert archived_result.get("isArchived") == True, "Class should be archived"

    # Unarchive the class
    unarchive_response = client.patch(
        f"/projects/{project}/classes/{created_id}",
        params={"archive": "false"}
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive class: {unarchive_response.text}"

    # Verify the class is unarchived
    get_unarchived_response = client.get(
        f"/projects/{project}/classes/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived class: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Class should not be archived"


def test_delete_class_project(client, project, cleanup_project_classes):
    """Test permanently deleting a class at project level"""
    # Create a class
    payload = {
        "name": "pytest_projectTestClass",
        "description": "A test class for deletion"
    }

    create_response = client.post(
        f"/projects/{project}/classes",
        json=payload
    )

    # Note: We still register for cleanup in case deletion fails
    if create_response.status_code == 200:
        cleanup_project_classes.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create class: {create_response.text}"
    created_id = create_response.json()["id"]

    delete_response = client.delete(
        f"/projects/{project}/classes/{created_id}"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete class: {delete_response.text}"

    # Verify the class is gone
    get_response = client.get(
        f"/projects/{project}/classes/{created_id}"
    )
    
    # Get all classes and confirm deleted class is not in the list
    all_classes_response = client.get(f"/projects/{project}/classes")
    assert all_classes_response.status_code == 200, f"Failed to get all classes: {all_classes_response.text}"
    
    all_classes = all_classes_response.json()
    class_ids = [cls["id"] for cls in all_classes]
    
    assert created_id not in class_ids, f"Deleted class {created_id} should not appear in list of all classes"
    print(f"Confirmed: Class {created_id} not in list of {len(class_ids)} classes")
