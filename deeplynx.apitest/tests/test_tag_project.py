"""Tests for Project-level Tag API endpoints."""

import pytest

# ========================================================================
# PROJECT-LEVEL TESTS
# ========================================================================

def test_create_project_tag(client, project, cleanup_project_tags):
    """Test creating a single tag at project level."""
    payload = {
        "name": "pytest_ProjectTestTag"
    }
    
    response = client.post(
        f"/projects/{project}/tags",
        json=payload
    )
    
    if response.status_code == 200:
        cleanup_project_tags.append(response.json()["id"])
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert result["name"] == "pytest_ProjectTestTag"
    assert result["projectId"] == project
    assert result.get("isArchived") == False or "isArchived" not in result


def test_bulk_create_tags_project(client, project, cleanup_project_tags):
    """Test bulk creating tags at project level."""
    payload = [
        {"name": "ProjectBulkTestTag1"},
        {"name": "ProjectBulkTestTag2"},
        {"name": "ProjectBulkTestTag3"}
    ]
    
    response = client.post(
        f"/projects/{project}/tags/bulk",
        json=payload
    )
    
    if response.status_code == 200:
        results = response.json()
        cleanup_project_tags.extend([results[0]["id"], results[1]["id"], results[2]["id"]])
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    
    results = response.json()
    assert len(results) == 3, "Should create 3 tags"
    
    result_1 = results[0]
    assert result_1["name"] == "ProjectBulkTestTag1"
    assert result_1["projectId"] == project
    
    result_2 = results[1]
    assert result_2["name"] == "ProjectBulkTestTag2"
    assert result_2["projectId"] == project
    
    result_3 = results[2]
    assert result_3["name"] == "ProjectBulkTestTag3"
    assert result_3["projectId"] == project


def test_get_all_tags_project(client, project, cleanup_project_tags):
    """Test retrieving all tags at project level."""
    payload = {
        "name": "pytest_ProjectTestTag"
    }
    
    create_response = client.post(
        f"/projects/{project}/tags",
        json=payload
    )
    
    if create_response.status_code == 200:
        cleanup_project_tags.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create tag: {create_response.text}"
    created_tag = create_response.json()
    created_id = created_tag["id"]
    
    get_response = client.get(
        f"/projects/{project}/tags"
    )
    
    assert get_response.status_code == 200, f"Failed to get tags: {get_response.text}"
    
    all_tags = get_response.json()
    assert isinstance(all_tags, list), "Expected response to be a list"
    
    tag_ids = [tag["id"] for tag in all_tags]
    assert created_id in tag_ids, f"Created tag {created_id} not found in list of tags"
    
    our_tag = next((tag for tag in all_tags if tag["id"] == created_id), None)
    assert our_tag is not None, f"Could not find tag with id {created_id}"
    assert our_tag["name"] == "pytest_ProjectTestTag"


def test_get_tag_project(client, project, cleanup_project_tags):
    """Test retrieving a single tag by ID at project level."""
    payload = {
        "name": "pytest_ProjectTestTag"
    }
    
    create_response = client.post(
        f"/projects/{project}/tags",
        json=payload
    )
    
    if create_response.status_code == 200:
        cleanup_project_tags.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create tag: {create_response.text}"
    created_id = create_response.json()["id"]
    
    get_response = client.get(
        f"/projects/{project}/tags/{created_id}"
    )
    
    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")
    
    assert get_response.status_code == 200, f"Failed to get tag: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_ProjectTestTag"


def test_update_tag_project(client, project, cleanup_project_tags):
    """Test updating a tag at project level."""
    create_payload = {
        "name": "pytest_ProjectTestTag"
    }
    
    create_response = client.post(
        f"/projects/{project}/tags",
        json=create_payload
    )
    
    if create_response.status_code == 200:
        cleanup_project_tags.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create tag: {create_response.text}"
    created_id = create_response.json()["id"]
    
    update_payload = {
        "name": "pytest_ProjectTestTag_Updated"
    }
    
    update_response = client.put(
        f"/projects/{project}/tags/{created_id}",
        json=update_payload
    )
    
    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")
    
    assert update_response.status_code == 200, f"Failed to update tag: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_ProjectTestTag_Updated"


def test_archive_and_unarchive_tag_project(client, project, cleanup_project_tags):
    """Test archiving and unarchiving a tag at project level."""
    payload = {
        "name": "pytest_ProjectTestTag"
    }
    
    create_response = client.post(
        f"/projects/{project}/tags",
        json=payload
    )
    
    if create_response.status_code == 200:
        cleanup_project_tags.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create tag: {create_response.text}"
    created_id = create_response.json()["id"]
    
    archive_response = client.patch(
        f"/projects/{project}/tags/{created_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive tag: {archive_response.text}"
    
    get_archived_response = client.get(
        f"/projects/{project}/tags/{created_id}",
        params={"hideArchived": "false"}
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived tag: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(archived_result)
    assert archived_result.get("isArchived") == True, "Tag should be archived"
    
    unarchive_response = client.patch(
        f"/projects/{project}/tags/{created_id}",
        params={"archive": "false"}
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive tag: {unarchive_response.text}"
    
    get_unarchived_response = client.get(
        f"/projects/{project}/tags/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived tag: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Tag should not be archived"


def test_delete_tag_project(client, project, cleanup_project_tags):
    """Test permanently deleting a tag at project level."""
    payload = {
        "name": "pytest_ProjectTestTag"
    }
    
    create_response = client.post(
        f"/projects/{project}/tags",
        json=payload
    )
    
    if create_response.status_code == 200:
        cleanup_project_tags.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create tag: {create_response.text}"
    created_id = create_response.json()["id"]
    
    delete_response = client.delete(
        f"/projects/{project}/tags/{created_id}"
    )
    
    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")
    
    assert delete_response.status_code == 200, f"Failed to delete tag: {delete_response.text}"
    
    get_response = client.get(
        f"/projects/{project}/tags/{created_id}"
    )
    
    all_tags_response = client.get(f"/projects/{project}/tags")
    assert all_tags_response.status_code == 200, f"Failed to get all tags: {all_tags_response.text}"
    
    all_tags = all_tags_response.json()
    tag_ids = [tag["id"] for tag in all_tags]
    
    assert created_id not in tag_ids, f"Deleted tag {created_id} should not appear in list of all tags"
    print(f"Confirmed: Tag {created_id} not in list of {len(tag_ids)} tags")


def test_get_all_tags_with_archived_project(client, project, cleanup_project_tags):
    """Test retrieving all tags including archived ones at project level."""
    tag1_response = client.post(
        f"/projects/{project}/tags",
        json={"name": "pytest_VisibleTag"}
    )
    assert tag1_response.status_code == 200, f"Failed to create first tag: {tag1_response.text}"
    tag1_id = tag1_response.json()["id"]
    cleanup_project_tags.append(tag1_id)
    
    tag2_response = client.post(
        f"/projects/{project}/tags",
        json={"name": "pytest_ArchivedTag"}
    )
    assert tag2_response.status_code == 200, f"Failed to create second tag: {tag2_response.text}"
    tag2_id = tag2_response.json()["id"]
    
    archive_response = client.patch(
        f"/projects/{project}/tags/{tag2_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive tag: {archive_response.text}"
    
    response = client.get(
        f"/projects/{project}/tags",
        params={"hideArchived": "false"}
    )
    
    assert response.status_code == 200, f"Failed to get all tags: {response.text}"
    result = response.json()
    
    tag_ids = [tag["id"] for tag in result]
    assert tag1_id in tag_ids, "Visible tag should be in results"
    assert tag2_id in tag_ids, "Archived tag should be in results when hideArchived=false"
    
    response_hidden = client.get(
        f"/projects/{project}/tags",
        params={"hideArchived": "true"}
    )
    
    assert response_hidden.status_code == 200, f"Failed to get tags with hideArchived: {response_hidden.text}"
    result_hidden = response_hidden.json()
    
    hidden_tag_ids = [tag["id"] for tag in result_hidden]
    assert tag1_id in hidden_tag_ids, "Visible tag should be in results"
    assert tag2_id not in hidden_tag_ids, "Archived tag should not be in results when hideArchived=true"

    # unarchive tag so it can be deleted
    client.patch(
        f"/projects/{project}/tags/{tag2_id}",
        params={"archive": "false"}
    )
    cleanup_project_tags.append(tag2_id)

    print(f"Confirmed: Archived tag properly hidden/shown based on hideArchived parameter")


def test_get_all_tags_multiproject(client, organization, project, cleanup_project_tags):
    """Test retrieving all tags from multiple projects."""
    # Create tags in the current project
    tag1_response = client.post(
        f"/projects/{project}/tags",
        json={"name": "pytest_MultiProjectTag1"}
    )
    assert tag1_response.status_code == 200, f"Failed to create first tag: {tag1_response.text}"
    tag1_id = tag1_response.json()["id"]
    cleanup_project_tags.append(tag1_id)
    
    tag2_response = client.post(
        f"/projects/{project}/tags",
        json={"name": "pytest_MultiProjectTag2"}
    )
    assert tag2_response.status_code == 200, f"Failed to create second tag: {tag2_response.text}"
    tag2_id = tag2_response.json()["id"]
    cleanup_project_tags.append(tag2_id)
    
    # Get tags from multiple projects (just testing with one project for now)
    response = client.get(
        f"/projects/{project}/tags/",
        params={"projectIds": [project], "hideArchived": "true"}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to get multiproject tags: {response.text}"
    
    result = response.json()
    assert isinstance(result, list), "Expected response to be a list"
    
    tag_ids = [tag["id"] for tag in result]
    assert tag1_id in tag_ids, "First tag should be in multiproject results"
    assert tag2_id in tag_ids, "Second tag should be in multiproject results"
    
    print(f"Confirmed: Retrieved {len(result)} tags from multiproject endpoint")