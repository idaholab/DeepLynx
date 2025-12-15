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
        "tags": ["test-tag"],
        "sensitivity_labels": ["public"]
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