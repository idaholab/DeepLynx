"""Tests for Records API endpoints."""

import pytest
import time

# ========================================================================
# RECORDS TESTS
# ========================================================================

def test_create_record(client, organization, project, origin_class, test_datasource_project, cleanup_records):
    """Test creating a single record."""
    timestamp = int(time.time() * 1000)
    
    payload = {
        "name": "pytest_CreateTestRecord",
        "description": "A test record for creation",
        "original_id": f"{timestamp}-create-001",
        "properties": {"key1": "value1", "key2": "value2"},
        "class_id": origin_class,
        "file_type": "pdf",
        "tags": ["test-tag"]
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    if response.status_code == 200:
        cleanup_records.append(response.json()["id"])
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    
    result = response.json()
    assert result["name"] == "pytest_CreateTestRecord"
    assert result["description"] == "A test record for creation"
    
    # Properties are returned as JSON string, need to parse
    import json
    properties = json.loads(result["properties"]) if isinstance(result["properties"], str) else result["properties"]
    assert properties["key1"] == "value1"
    assert result["classId"] == origin_class or result["class_id"] == origin_class


def test_bulk_create_records(client, organization, project, origin_class, test_datasource_project, cleanup_records):
    """Test bulk creating records."""
    timestamp = int(time.time() * 1000)
    
    payload = [
        {
            "name": "BulkTestRecord1",
            "description": "First bulk test record",
            "original_id": f"{timestamp}-bulk-001",
            "properties": {"bulk": "test1"},
            "class_id": origin_class,
            "file_type": "png"
        },
        {
            "name": "BulkTestRecord2",
            "description": "Second bulk test record",
            "original_id": f"{timestamp}-bulk-002",
            "properties": {"bulk": "test2"},
            "class_id": origin_class,
            "file_type": "jpg"
        },
        {
            "name": "BulkTestRecord3",
            "description": "Third bulk test record",
            "original_id": f"{timestamp}-bulk-003",
            "properties": {"bulk": "test3"},
            "class_id": origin_class
        }
    ]
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/records/bulk?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    if response.status_code == 200:
        results = response.json()
        cleanup_records.extend([r["id"] for r in results if "id" in r])
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    
    results = response.json()
    assert len(results) == 3
    
    import json
    
    # Check first record
    assert results[0]["name"] == "BulkTestRecord1"
    assert results[0]["description"] == "First bulk test record"
    properties_0 = json.loads(results[0]["properties"]) if isinstance(results[0]["properties"], str) else results[0]["properties"]
    assert properties_0["bulk"] == "test1"
    
    # Check second record
    assert results[1]["name"] == "BulkTestRecord2"
    assert results[1]["description"] == "Second bulk test record"
    properties_1 = json.loads(results[1]["properties"]) if isinstance(results[1]["properties"], str) else results[1]["properties"]
    assert properties_1["bulk"] == "test2"


def test_get_all_records(client, organization, project, origin_class, test_datasource_project, cleanup_records):
    """Test retrieving all records in a project."""
    timestamp = int(time.time() * 1000)
    
    # Create a test record
    payload = {
        "name": "pytest_GetAllTestRecord",
        "description": "Test record for get all",
        "original_id": f"{timestamp}-get-all-001",
        "properties": {"test": "value"},
        "class_id": origin_class
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    if create_response.status_code == 200:
        cleanup_records.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create record: {create_response.text}"
    created_id = create_response.json()["id"]
    
    # Get all records
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/records?hideArchived=true"
    )
    
    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")
    
    assert get_response.status_code == 200, f"Failed to get records: {get_response.text}"
    
    all_records = get_response.json()
    assert isinstance(all_records, list), "Expected response to be a list"
    
    record_ids = [rec["id"] for rec in all_records]
    assert created_id in record_ids, f"Created record {created_id} not found in list"


def test_get_all_records_with_filters(client, organization, project, origin_class, test_datasource_project, cleanup_records):
    """Test retrieving all records with filters (datasource and file type)."""
    timestamp = int(time.time() * 1000)
    
    # Create a record with specific file type
    payload = {
        "name": "pytest_FilterTestRecord",
        "description": "Test record for filter testing",
        "original_id": f"{timestamp}-filter-001",
        "properties": {},
        "class_id": origin_class,
        "file_type": "pdf"
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    if create_response.status_code == 200:
        cleanup_records.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create record: {create_response.text}"
    created_id = create_response.json()["id"]
    
    # Test filter by datasource
    get_response_ds = client.get(
        f"/organizations/{organization}/projects/{project}/records?hideArchived=true&dataSourceId={test_datasource_project}"
    )
    
    assert get_response_ds.status_code == 200, f"Failed to get records by datasource: {get_response_ds.text}"
    ds_records = get_response_ds.json()
    assert isinstance(ds_records, list), "Expected response to be a list"
    
    # Test filter by file type
    get_response_ft = client.get(
        f"/organizations/{organization}/projects/{project}/records?hideArchived=true&fileType=pdf"
    )
    
    print(f"\nStatus Code: {get_response_ft.status_code}")
    print(f"Response Body: {get_response_ft.text}")
    
    assert get_response_ft.status_code == 200, f"Failed to get records by file type: {get_response_ft.text}"
    ft_records = get_response_ft.json()
    
    # Our created record should be in the filtered results
    ft_record_ids = [rec["id"] for rec in ft_records]
    assert created_id in ft_record_ids, f"Created record {created_id} not found in filtered results"


def test_get_record(client, organization, project, origin_class, test_datasource_project, cleanup_records):
    """Test retrieving a single record by ID."""
    timestamp = int(time.time() * 1000)
    
    # Create a record
    payload = {
        "name": "pytest_GetTestRecord",
        "description": "Test record for retrieval",
        "original_id": f"{timestamp}-get-001",
        "properties": {"test": "value"},
        "class_id": origin_class,
        "file_type": "docx"
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    if create_response.status_code == 200:
        cleanup_records.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create record: {create_response.text}"
    created_id = create_response.json()["id"]
    
    # Get the specific record
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/{created_id}?hideArchived=true"
    )
    
    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")
    
    assert get_response.status_code == 200, f"Failed to get record: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_GetTestRecord"
    assert result["description"] == "Test record for retrieval"
    
    import json
    properties = json.loads(result["properties"]) if isinstance(result["properties"], str) else result["properties"]
    assert properties["test"] == "value"


def test_update_record(client, organization, project, origin_class, test_datasource_project, cleanup_records):
    """Test updating a record."""
    timestamp = int(time.time() * 1000)
    
    # Create a record
    create_payload = {
        "name": "pytest_UpdateTestRecord",
        "description": "Original description",
        "original_id": f"{timestamp}-update-001",
        "properties": {"version": "1"},
        "class_id": origin_class,
        "file_type": "txt"
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=create_payload
    )
    
    if create_response.status_code == 200:
        cleanup_records.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create record: {create_response.text}"
    created_id = create_response.json()["id"]
    
    # Update the record
    update_payload = {
        "name": "pytest_UpdateTestRecord_Updated",
        "description": "Updated description",
        "properties": {"version": "2", "updated": True},
        "file_type": "docx"
    }
    
    update_response = client.put(
        f"/organizations/{organization}/projects/{project}/records/{created_id}",
        json=update_payload
    )
    
    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")
    
    assert update_response.status_code == 200, f"Failed to update record: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["name"] == "pytest_UpdateTestRecord_Updated"
    assert result["description"] == "Updated description"
    
    import json
    properties = json.loads(result["properties"]) if isinstance(result["properties"], str) else result["properties"]
    assert properties["version"] == "2"


def test_archive_and_unarchive_record(client, organization, project, origin_class, test_datasource_project, cleanup_records):
    """Test archiving and unarchiving a record."""
    timestamp = int(time.time() * 1000)
    
    # Create a record
    payload = {
        "name": "pytest_ArchiveTestRecord",
        "description": "Test record for archive/unarchive",
        "original_id": f"{timestamp}-archive-001",
        "properties": {},
        "class_id": origin_class
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    if create_response.status_code == 200:
        cleanup_records.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create record: {create_response.text}"
    created_id = create_response.json()["id"]
    
    # Archive the record
    archive_response = client.patch(
        f"/organizations/{organization}/projects/{project}/records/{created_id}?archive=true"
    )
    
    print(f"\nArchive Status Code: {archive_response.status_code}")
    print(f"Archive Response Body: {archive_response.text}")
    
    assert archive_response.status_code == 200, f"Failed to archive record: {archive_response.text}"
    
    # Verify the record is archived (using hideArchived=false to see archived records)
    get_archived_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/{created_id}?hideArchived=false"
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived record: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    assert archived_result.get("isArchived") == True, "Record should be archived"
    
    # Unarchive the record
    unarchive_response = client.patch(
        f"/organizations/{organization}/projects/{project}/records/{created_id}?archive=false"
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive record: {unarchive_response.text}"
    
    # Verify the record is unarchived
    get_unarchived_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/{created_id}?hideArchived=true"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived record: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Record should not be archived"


def test_delete_record(client, organization, project, origin_class, test_datasource_project, cleanup_records):
    """Test permanently deleting a record."""
    timestamp = int(time.time() * 1000)
    
    # Create a record
    payload = {
        "name": "pytest_DeleteTestRecord",
        "description": "Test record for deletion",
        "original_id": f"{timestamp}-delete-001",
        "properties": {},
        "class_id": origin_class
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    # Still register for cleanup in case deletion fails
    if create_response.status_code == 200:
        cleanup_records.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create record: {create_response.text}"
    created_id = create_response.json()["id"]
    
    # Delete the record
    delete_response = client.delete(
        f"/organizations/{organization}/projects/{project}/records/{created_id}"
    )
    
    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")
    
    assert delete_response.status_code == 200, f"Failed to delete record: {delete_response.text}"
    
    # Verify the record is gone - get all records and confirm it's not in the list
    all_records_response = client.get(
        f"/organizations/{organization}/projects/{project}/records?hideArchived=true"
    )
    assert all_records_response.status_code == 200, f"Failed to get all records: {all_records_response.text}"
    
    all_records = all_records_response.json()
    record_ids = [rec["id"] for rec in all_records]
    
    assert created_id not in record_ids, f"Deleted record {created_id} should not appear in list"
    print(f"Confirmed: Record {created_id} not in list of {len(record_ids)} records")


def test_get_record_count_for_data_source(client, organization, project, origin_class, test_datasource_project, cleanup_records):
    """Test getting record count for a specific data source."""
    timestamp = int(time.time() * 1000)
    
    # Create a few records
    for i in range(3):
        payload = {
            "name": f"pytest_CountTestRecord{i}",
            "description": f"Test record {i} for count",
            "original_id": f"{timestamp}-count-{i:03d}",
            "properties": {},
            "class_id": origin_class
        }
        
        create_response = client.post(
            f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
            json=payload
        )
        
        if create_response.status_code == 200:
            cleanup_records.append(create_response.json()["id"])
        
        assert create_response.status_code == 200, f"Failed to create record {i}: {create_response.text}"
    
    # Get count for the data source
    count_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/count?dataSourceId={test_datasource_project}"
    )
    
    print(f"\nStatus Code: {count_response.status_code}")
    print(f"Response Body: {count_response.text}")
    
    assert count_response.status_code == 200, f"Failed to get record count: {count_response.text}"
    
    # The count should be at least 3 (the records we just created)
    result = count_response.json()
    if isinstance(result, dict) and "count" in result:
        assert result["count"] >= 3, f"Expected at least 3 records, got {result['count']}"
    elif isinstance(result, int):
        assert result >= 3, f"Expected at least 3 records, got {result}"


def test_get_records_by_tags(client, organization, project, origin_class, test_datasource_project, cleanup_records, cleanup_project_tags):
    """Test retrieving records by tags."""
    timestamp = int(time.time() * 1000)
    
    # Create tags first (using bulk endpoint)
    tag_payload = [
        {"name": "pytest-record-tag-1"},
        {"name": "pytest-record-tag-2"}
    ]
    
    tag_response = client.post(
        f"/projects/{project}/tags/bulk",
        json=tag_payload
    )
    
    if tag_response.status_code == 200:
        results = tag_response.json()
        cleanup_project_tags.extend([results[0]["id"], results[1]["id"]])
    
    assert tag_response.status_code == 200, f"Failed to create tags: {tag_response.text}"
    tag_results = tag_response.json()
    tag_ids = [tag["id"] for tag in tag_results]
    
    # Create records first, then attach tags (tags in payload might not work)
    record_ids = []
    for i in range(2):
        payload = {
            "name": f"pytest_TaggedRecord{i}",
            "description": f"Test record {i} with tag",
            "original_id": f"{timestamp}-tagged-{i:03d}",
            "properties": {},
            "class_id": origin_class
        }
        
        create_response = client.post(
            f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
            json=payload
        )
        
        if create_response.status_code == 200:
            record_id = create_response.json()["id"]
            cleanup_records.append(record_id)
            record_ids.append(record_id)
        
        assert create_response.status_code == 200, f"Failed to create tagged record {i}: {create_response.text}"
    
    # Now attach tags to the records
    for record_id in record_ids:
        attach_response = client.post(
            f"/organizations/{organization}/projects/{project}/records/{record_id}/tags?tagId={tag_ids[0]}"
        )
        assert attach_response.status_code == 200, f"Failed to attach tag: {attach_response.text}"
    
    # Get records by tag (using tag IDs)
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/by-tags?tagIds={tag_ids[0]}&hideArchived=true"
    )
    
    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")
    
    assert get_response.status_code == 200, f"Failed to get records by tags: {get_response.text}"
    
    results = get_response.json()
    assert isinstance(results, list), "Expected response to be a list"
    # Should have at least the 2 records we created
    tagged_records = [r for r in results if r["name"].startswith("pytest_TaggedRecord")]
    assert len(tagged_records) >= 2, "Should find at least 2 tagged records"


def test_attach_and_unattach_tag_to_record(client, organization, project, origin_class, test_datasource_project, cleanup_records, cleanup_project_tags):
    """Test attaching and unattaching a tag to a record."""
    timestamp = int(time.time() * 1000)
    
    # Create a tag
    tag_payload = {
        "name": "pytest-attach-tag"
    }
    
    tag_response = client.post(
        f"/projects/{project}/tags",
        json=tag_payload
    )
    
    if tag_response.status_code == 200:
        cleanup_project_tags.append(tag_response.json()["id"])
    
    assert tag_response.status_code == 200, f"Failed to create tag: {tag_response.text}"
    tag_id = tag_response.json()["id"]
    
    # Create a record without the tag
    record_payload = {
        "name": "pytest_AttachTagRecord",
        "description": "Test record for tag attachment",
        "original_id": f"{timestamp}-attach-001",
        "properties": {},
        "class_id": origin_class
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=record_payload
    )
    
    if create_response.status_code == 200:
        cleanup_records.append(create_response.json()["id"])
    
    assert create_response.status_code == 200, f"Failed to create record: {create_response.text}"
    record_id = create_response.json()["id"]
    
    # Attach the tag
    attach_response = client.post(
        f"/organizations/{organization}/projects/{project}/records/{record_id}/tags?tagId={tag_id}"
    )
    
    print(f"\nAttach Status Code: {attach_response.status_code}")
    print(f"Attach Response Body: {attach_response.text}")
    
    assert attach_response.status_code == 200, f"Failed to attach tag: {attach_response.text}"
    
    # Verify tag is attached
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/{record_id}?hideArchived=true"
    )
    assert get_response.status_code == 200, f"Failed to get record: {get_response.text}"
    record = get_response.json()
    
    # Check if tags field exists and contains our tag
    if "tags" in record and isinstance(record["tags"], list):
        tag_names = [tag.get("name") for tag in record["tags"]]
        assert "pytest-attach-tag" in tag_names, "Tag should be attached to record"
    
    # Unattach the tag
    unattach_response = client.delete(
        f"/organizations/{organization}/projects/{project}/records/{record_id}/tags?tagId={tag_id}"
    )
    
    print(f"\nUnattach Status Code: {unattach_response.status_code}")
    print(f"Unattach Response Body: {unattach_response.text}")
    
    assert unattach_response.status_code == 200, f"Failed to unattach tag: {unattach_response.text}"
    
    # Verify tag is unattached
    get_after_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/{record_id}?hideArchived=true"
    )
    assert get_after_response.status_code == 200, f"Failed to get record after unattach: {get_after_response.text}"
    record_after = get_after_response.json()
    
    # Check if tags field exists and doesn't contain our tag
    if "tags" in record_after and isinstance(record_after["tags"], list):
        tag_names = [tag.get("name") for tag in record_after["tags"]]
        assert "pytest-attach-tag" not in tag_names, "Tag should not be attached to record"

def test_bulk_attach_tags_to_records(client, organization, project, origin_class, test_datasource_project, cleanup_records, cleanup_project_tags):
    """Test bulk attaching multiple tags to multiple records."""
    timestamp = int(time.time() * 1000)

    # Create two tags
    tag_ids = []
    
    for i in range(2):
        tag_payload = {
            "name": f"pytest-bulk-attach-tag-{i}"
        }
            
        tag_response = client.post(
            f"/projects/{project}/tags",
            json=tag_payload
        )
        
        if tag_response.status_code == 200:
            cleanup_project_tags.append(tag_response.json()["id"])

        assert tag_response.status_code == 200, f"Failed to create tag: {tag_response.text}"
        tag_ids.append(tag_response.json()["id"])
        
    # Create two records without the tag
    record_ids = []
    
    for i in range(2):
        record_payload = {
            "name": f"pytest_BulkAttachTagRecord{1}",
            "description": f"Test record {i} for tag attachment",
            "original_id": f"{timestamp}-bulk-attach-{i}",
            "properties": {},
            "class_id": origin_class
        }
    
        create_response = client.post(
            f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
            json=record_payload
        )
    
        if create_response.status_code == 200:
            cleanup_records.append(create_response.json()["id"])
    
        assert create_response.status_code == 200, f"Failed to create record: {create_response.text}"
        record_ids.append(create_response.json()["id"])
    
    # Attach multiple record/tag pairs using one request
    pair_payload = [
        {"record_id": record_ids[0], "tag_id": tag_ids[0]},
        {"record_id": record_ids[0], "tag_id": tag_ids[1]},
        {"record_id": record_ids[1], "tag_id": tag_ids[0]},
        {"record_id": record_ids[1], "tag_id": tag_ids[1]}
    ]
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/records/bulk-attach-tags-to-records",
        json=pair_payload
    )
    
    print(f"\nStatusCode: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to bulk attach tags: {response.text}"
    
    # Verify both records have both tags
    for record_id in record_ids:
        get_response = client.get(
            f"/organizations/{organization}/projects/{project}/records/{record_id}?hideArchived=true"
        )
        assert get_response.status_code == 200, f"Failed to get record: {get_response.text}"
        
        record = get_response.json()
        tag_ids_on_record = [tag["id"] for tag in record["tags"]]
        
        assert tag_ids[0] in tag_ids_on_record, f"Tag {tag_ids[0]} should be attached to record {record_id}"
        assert tag_ids[1] in tag_ids_on_record, f"Tag {tag_ids[1]} should be attached to record {record_id}"

def test_attach_and_unattach_label_to_record(
    client, organization, project, origin_class, test_datasource_project, current_user_id,
    cleanup_records, cleanup_project_labels):
    """Test attaching and unattaching a sensitivity label to a record."""
    timestamp = int(time.time() * 1000)
    
    # Get the current user's role from their existing project membership
    members_response = client.get(
        f"/organizations/{organization}/projects/{project}/members"
    )
    assert members_response.status_code == 200, f"Failed to get members: {members_response.text}"
    
    members = members_response.json()
    print(f"\nMembers Response: {members}")
    
    # Find the current user's membership and get their role ID
    user_member = None
    for member in members:
        if member.get('memberId') == current_user_id or member.get('userId') == current_user_id:
            user_member = member
            break
    
    assert user_member is not None, f"Current user {current_user_id} not found in project members: {members}"
    role_id = user_member.get('roleId')
    assert role_id is not None, f"Role ID not found for user {current_user_id} in member data: {user_member}"
    
    print(f"\nFound user's role ID: {role_id}")
    
    # Create a sensitivity label
    label_payload = {
        "name": "pytest-attach-label",
        "description": "Test label for attachment"
    }
    
    label_response = client.post(
        f"/projects/{project}/labels",
        json=label_payload
    )
    
    assert label_response.status_code == 200, f"Failed to create label: {label_response.text}"
    label_id = label_response.json()["id"]
    cleanup_project_labels.append(label_id)
    
    # Get the permissions that were automatically created when the label was created
    permissions_response = client.get(
        f"/organizations/{organization}/projects/{project}/permissions"
    )
    assert permissions_response.status_code == 200, f"Failed to get permissions: {permissions_response.text}"
    
    permissions = permissions_response.json()
    write_permission = None
    read_permission = None
    
    # Find the read and write permissions for this specific label
    for permission in permissions:
        if permission.get('action') == 'write' and permission.get('labelId') == label_id:
            write_permission = permission
        if permission.get('action') == 'read' and permission.get('labelId') == label_id:
            read_permission = permission
        # Break only after finding both permissions
        if write_permission and read_permission:
            break
    
    assert write_permission is not None, f"Write permission not found for label {label_id}. Available permissions: {permissions}"
    assert read_permission is not None, f"Read permission not found for label {label_id}. Available permissions: {permissions}"
    
    write_permission_id = write_permission['id']
    read_permission_id = read_permission['id']
    
    print(f"\nFound write permission: {write_permission}")
    print(f"\nFound read permission: {read_permission}")
    
    # Add the read permission to the user's role (required to view records with this label)
    add_read_permission_response = client.post(
        f"/organizations/{organization}/projects/{project}/roles/{role_id}/permissions/{read_permission_id}"
    )
    
    print(f"\nAdd Read Permission Status Code: {add_read_permission_response.status_code}")
    print(f"Add Read Permission Response Body: {add_read_permission_response.text}")
    
    assert add_read_permission_response.status_code == 200, f"Failed to add read permission to role: {add_read_permission_response.text}"
    
    # Add the write permission to the user's role (required to attach/modify labels)
    add_write_permission_response = client.post(
        f"/organizations/{organization}/projects/{project}/roles/{role_id}/permissions/{write_permission_id}"
    )
    
    print(f"\nAdd Write Permission Status Code: {add_write_permission_response.status_code}")
    print(f"Add Write Permission Response Body: {add_write_permission_response.text}")
    
    assert add_write_permission_response.status_code == 200, f"Failed to add write permission to role: {add_write_permission_response.text}"
    
    # Verify the role has both permissions
    role_permissions_response = client.get(
        f"/organizations/{organization}/projects/{project}/roles/{role_id}/permissions"
    )
    print(f"\nRole Permissions Check Status Code: {role_permissions_response.status_code}")
    print(f"Role Permissions: {role_permissions_response.text}")
    
    assert role_permissions_response.status_code == 200, f"Failed to get role permissions: {role_permissions_response.text}"
    role_permissions = role_permissions_response.json()
    permission_ids_in_role = [p.get('id') for p in role_permissions]
    assert read_permission_id in permission_ids_in_role, f"Read permission {read_permission_id} not found in role {role_id}. Role has: {permission_ids_in_role}"
    assert write_permission_id in permission_ids_in_role, f"Write permission {write_permission_id} not found in role {role_id}. Role has: {permission_ids_in_role}"
    
    # Create a record without the label
    record_payload = {
        "name": "pytest_AttachLabelRecord",
        "description": "Test record for label attachment",
        "original_id": f"{timestamp}-attach-001",
        "properties": {},
        "class_id": origin_class
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=record_payload
    )
    
    assert create_response.status_code == 200, f"Failed to create record: {create_response.text}"
    record_id = create_response.json()["id"]
    cleanup_records.append(record_id)
    
    # Attach the label
    attach_response = client.post(
        f"/organizations/{organization}/projects/{project}/records/{record_id}/sensitivity-labels?labelId={label_id}"
    )
    
    print(f"\nAttach Status Code: {attach_response.status_code}")
    print(f"Attach Response Body: {attach_response.text}")
    
    assert attach_response.status_code == 200, f"Failed to attach label: {attach_response.text}"
    
    # Verify the response message
    attach_data = attach_response.json()
    assert "message" in attach_data, "Response should contain a message"
    assert str(label_id) in attach_data["message"], "Message should mention the label ID"
    assert str(record_id) in attach_data["message"], "Message should mention the record ID"
    
    # Verify label is attached by fetching the record
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/{record_id}?hideArchived=true"
    )
    assert get_response.status_code == 200, f"Failed to get record: {get_response.text}"
    record = get_response.json()
    
    # Check if labels field exists and contains our label
    assert "labels" in record, "Record should have labels field"
    assert record["labels"] is not None, "Labels should not be null after attaching a label"
    assert isinstance(record["labels"], list), "Labels should be a list"
    label_ids = [label.get("id") for label in record["labels"]]
    assert label_id in label_ids, f"Label {label_id} should be attached to record"
    
    # Unattach the label
    unattach_response = client.delete(
        f"/organizations/{organization}/projects/{project}/records/{record_id}/sensitivity-labels?labelId={label_id}"
    )
    
    print(f"\nUnattach Status Code: {unattach_response.status_code}")
    print(f"Unattach Response Body: {unattach_response.text}")
    
    assert unattach_response.status_code == 200, f"Failed to unattach label: {unattach_response.text}"
    
    # Verify the response message
    unattach_data = unattach_response.json()
    assert "message" in unattach_data, "Response should contain a message"
    assert str(label_id) in unattach_data["message"], "Message should mention the label ID"
    assert str(record_id) in unattach_data["message"], "Message should mention the record ID"
    
    # Verify label is unattached
    get_after_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/{record_id}?hideArchived=true"
    )
    assert get_after_response.status_code == 200, f"Failed to get record after unattach: {get_after_response.text}"
    record_after = get_after_response.json()
    
    # Check if labels field exists and doesn't contain our label
    # Labels can be null or an empty list when no labels are attached
    if "labels" in record_after:
        if record_after["labels"] is not None and isinstance(record_after["labels"], list):
            label_ids_after = [label.get("id") for label in record_after["labels"]]
            assert label_id not in label_ids_after, f"Label {label_id} should not be attached to record"
        # If labels is null, that's also valid - means no labels attached
        
def test_attach_label_unauthorized(
    client, organization, project, origin_class, test_datasource_project, current_user_id,
    cleanup_records, cleanup_project_labels, cleanup_project_roles):
    """Test that attaching a label without write permission fails."""
    timestamp = int(time.time() * 1000)
    
    # Get the current user's role from their existing project membership
    members_response = client.get(
        f"/organizations/{organization}/projects/{project}/members"
    )
    assert members_response.status_code == 200, f"Failed to get members: {members_response.text}"
    
    members = members_response.json()
    print(f"\nMembers Response: {members}")
    
    # Find the current user's membership and get their role ID
    user_member = None
    for member in members:
        if member.get('memberId') == current_user_id or member.get('userId') == current_user_id:
            user_member = member
            break
    
    assert user_member is not None, f"Current user {current_user_id} not found in project members: {members}"
    role_id = user_member.get('roleId')
    assert role_id is not None, f"Role ID not found for user {current_user_id} in member data: {user_member}"
    
    print(f"\nFound user's role ID: {role_id}")
    
    # Create a sensitivity label (this will automatically create read/write permissions)
    label_payload = {
        "name": "pytest-attach-label-unauth",
        "description": "Test label for unauthorized attachment"
    }
    
    label_response = client.post(
        f"/projects/{project}/labels",
        json=label_payload
    )
    
    assert label_response.status_code == 200, f"Failed to create label: {label_response.text}"
    label_id = label_response.json()["id"]
    cleanup_project_labels.append(label_id)
    
    # Get the permissions that were automatically created when the label was created
    permissions_response = client.get(
        f"/organizations/{organization}/projects/{project}/permissions"
    )
    assert permissions_response.status_code == 200, f"Failed to get permissions: {permissions_response.text}"
    
    permissions = permissions_response.json()
    write_permission = None
    read_permission = None
    
    # Find the read and write permissions for this specific label
    for permission in permissions:
        if permission.get('action') == 'write' and permission.get('labelId') == label_id:
            write_permission = permission
        if permission.get('action') == 'read' and permission.get('labelId') == label_id:
            read_permission = permission
        # Break only after finding both permissions
        if write_permission and read_permission:
            break
    
    assert write_permission is not None, f"Write permission not found for label {label_id}"
    assert read_permission is not None, f"Read permission not found for label {label_id}"
    
    write_permission_id = write_permission['id']
    read_permission_id = read_permission['id']
    
    print(f"\nFound write permission: {write_permission}")
    print(f"\nFound read permission: {read_permission}")
    
    # DO NOT add write permission to the role - this is the key to testing unauthorized access
    # We only add read permission so the user can view the record, but not modify it with labels
    add_read_permission_response = client.post(
        f"/organizations/{organization}/projects/{project}/roles/{role_id}/permissions/{read_permission_id}"
    )
    
    print(f"\nAdd Read Permission Status Code: {add_read_permission_response.status_code}")
    print(f"Add Read Permission Response Body: {add_read_permission_response.text}")
    
    assert add_read_permission_response.status_code == 200, f"Failed to add read permission to role: {add_read_permission_response.text}"
    
    # Verify the role has ONLY the read permission (NOT the write permission)
    role_permissions_response = client.get(
        f"/organizations/{organization}/projects/{project}/roles/{role_id}/permissions"
    )
    print(f"\nRole Permissions Check Status Code: {role_permissions_response.status_code}")
    print(f"Role Permissions: {role_permissions_response.text}")
    
    assert role_permissions_response.status_code == 200, f"Failed to get role permissions: {role_permissions_response.text}"
    role_permissions = role_permissions_response.json()
    permission_ids_in_role = [p.get('id') for p in role_permissions]
    assert read_permission_id in permission_ids_in_role, f"Read permission {read_permission_id} should be in role {role_id}"
    assert write_permission_id not in permission_ids_in_role, f"Write permission {write_permission_id} should NOT be in role {role_id}"
    
    # Create a record without the label
    record_payload = {
        "name": "pytest_AttachLabelRecord_Unauth",
        "description": "Test record for unauthorized label attachment",
        "original_id": f"{timestamp}-attach-unauth-001",
        "properties": {},
        "class_id": origin_class
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=record_payload
    )
    
    assert create_response.status_code == 200, f"Failed to create record: {create_response.text}"
    record_id = create_response.json()["id"]
    cleanup_records.append(record_id)
    
    # Try to attach the label - should fail with 401, 403, or 500 (UnauthorizedAccessException)
    attach_response = client.post(
        f"/organizations/{organization}/projects/{project}/records/{record_id}/sensitivity-labels?labelId={label_id}"
    )
    
    print(f"\nUnauthorized Attach Status Code: {attach_response.status_code}")
    print(f"Unauthorized Attach Response Body: {attach_response.text}")
    
    # Should fail with Unauthorized (401), Forbidden (403), or 500 (for UnauthorizedAccessException)
    assert attach_response.status_code in [401, 403, 500], \
        f"Should fail with unauthorized error, got: {attach_response.status_code}"
    
    # Verify the error message mentions permission/authorization
    error_text = attach_response.text.lower()
    assert any(keyword in error_text for keyword in ['unauthorized', 'permission', 'access']), \
        f"Error message should mention authorization/permission issue: {attach_response.text}"
    
    # Verify label is NOT attached by fetching the record
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/{record_id}?hideArchived=true"
    )
    assert get_response.status_code == 200, f"Failed to get record: {get_response.text}"
    record = get_response.json()
    
    print(f"\nRecord after failed attach attempt: {record}")
    
    # Check that the label is NOT attached
    # Labels can be null or an empty list when no labels are attached
    if record.get("labels") is not None and isinstance(record["labels"], list):
        label_ids = [label.get("id") for label in record["labels"]]
        assert label_id not in label_ids, f"Label {label_id} should NOT be attached to record (no write permission)"
    # If labels is null, that's also valid - means no labels attached
    
    print(f"\nTest passed: User without write permission cannot attach label {label_id} to record {record_id}")
        
def test_get_edges_by_record(client, organization, project, test_records, test_relationship_project, test_datasource_project, cleanup_edges):
    """Test retrieving edges connected to a specific record."""
    
    # Use test_records fixture - indices 0 and 1 are origin and destination
    origin_record_id = test_records[0]
    destination_record_id = test_records[1]
    
    # Create an edge between the records
    edge_payload = {
        "origin_id": origin_record_id,
        "destination_id": destination_record_id,
        "relationship_id": test_relationship_project
    }
    
    edge_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=edge_payload
    )
    
    if edge_response.status_code == 200:
        cleanup_edges.append(edge_response.json()["id"])
    
    assert edge_response.status_code == 200, f"Failed to create edge: {edge_response.text}"
    
    # Get edges for the origin record (as origin)
    # Note: page parameter must be >= 1
    get_response_origin = client.get(
        f"/organizations/{organization}/projects/{project}/records/{origin_record_id}/edges?isOrigin=true&page=1&pageSize=20&hideArchived=true"
    )
    
    print(f"\nStatus Code (as origin): {get_response_origin.status_code}")
    print(f"Response Body (as origin): {get_response_origin.text}")
    
    assert get_response_origin.status_code == 200, f"Failed to get edges (as origin): {get_response_origin.text}"
    
    results_origin = get_response_origin.json()
    assert isinstance(results_origin, list), "Expected response to be a list"
    
    # Get edges for the destination record (as destination)
    get_response_dest = client.get(
        f"/organizations/{organization}/projects/{project}/records/{destination_record_id}/edges?isOrigin=false&page=1&pageSize=20&hideArchived=true"
    )
    
    print(f"\nStatus Code (as destination): {get_response_dest.status_code}")
    print(f"Response Body (as destination): {get_response_dest.text}")
    
    assert get_response_dest.status_code == 200, f"Failed to get edges (as destination): {get_response_dest.text}"


def test_get_graph_data_for_record(client, organization, project, test_records, test_relationship_project, test_datasource_project, cleanup_edges):
    """Test retrieving graph data for a specific record."""
    
    # Use test_records fixture to create a simple graph
    origin_record_id = test_records[0]
    middle_record_id = test_records[1]
    destination_record_id = test_records[2]
    
    # Create edges to form a simple path: record0 -> record1 -> record2
    edge1_payload = {
        "origin_id": origin_record_id,
        "destination_id": middle_record_id,
        "relationship_id": test_relationship_project
    }
    
    edge1_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=edge1_payload
    )
    
    if edge1_response.status_code == 200:
        cleanup_edges.append(edge1_response.json()["id"])
    
    assert edge1_response.status_code == 200, f"Failed to create edge 1: {edge1_response.text}"
    
    edge2_payload = {
        "origin_id": middle_record_id,
        "destination_id": destination_record_id,
        "relationship_id": test_relationship_project
    }
    
    edge2_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=edge2_payload
    )
    
    if edge2_response.status_code == 200:
        cleanup_edges.append(edge2_response.json()["id"])
    
    assert edge2_response.status_code == 200, f"Failed to create edge 2: {edge2_response.text}"
    
    # Get graph data for the origin record with depth=2
    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/records/{origin_record_id}/graph?depth=2"
    )
    
    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")
    
    assert get_response.status_code == 200, f"Failed to get graph data: {get_response.text}"
    
    result = get_response.json()
    
    # Graph data should contain nodes and edges
    if isinstance(result, dict):
        assert "nodes" in result or "edges" in result, "Graph data should contain nodes or edges"
        
        if "nodes" in result:
            print(f"  Number of nodes: {len(result['nodes'])}")
        if "edges" in result:
            print(f"  Number of edges: {len(result['edges'])}")