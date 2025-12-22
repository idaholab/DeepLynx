"""Tests for Edge API endpoints."""

# ========================================================================
# PROJECT-LEVEL TESTS (Edges only exist at project level)
# ========================================================================

def test_create_edge(client, organization, project, test_relationship_project, test_records, test_datasource_project, cleanup_edges):
    """Test creating a single edge."""
    origin_id, destination_id = test_records[0], test_records[1]
    
    payload = {
        "origin_id": origin_id,
        "destination_id": destination_id,
        "relationship_id": test_relationship_project
    }

    response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=payload
    )

    if response.status_code == 200:
        cleanup_edges.append(response.json()["id"])

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert result["originId"] == origin_id
    assert result["destinationId"] == destination_id
    assert result["relationshipId"] == test_relationship_project


def test_bulk_create_edges(client, organization, project, test_relationship_project, test_records, test_datasource_project ,cleanup_edges):
    """Test bulk creating edges."""
    payload = [
        {
            "origin_id": test_records[0],
            "destination_id": test_records[1],
            "relationship_id": test_relationship_project
        },
        {
            "origin_id": test_records[2],
            "destination_id": test_records[3],
            "relationship_id": test_relationship_project
        }
    ]
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/edges/bulk?dataSourceId={test_datasource_project}",
        json=payload
    )

    if response.status_code == 200:
        results = response.json()
        cleanup_edges.extend([results[0]["id"], results[1]["id"]])

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")    

    assert response.status_code == 200, f"Request failed: {response.text}"
    
    results = response.json()
    assert len(results) == 2
    
    result_1 = results[0]
    assert result_1["originId"] == test_records[0]
    assert result_1["destinationId"] == test_records[1]
    assert result_1["relationshipId"] == test_relationship_project

    result_2 = results[1]
    assert result_2["originId"] == test_records[2]
    assert result_2["destinationId"] == test_records[3]
    assert result_2["relationshipId"] == test_relationship_project


def test_get_all_edges(client, organization, project, test_relationship_project, test_records, test_datasource_project, cleanup_edges):
    """Test retrieving all edges."""

    payload = {
        "origin_id": test_records[0],
        "destination_id": test_records[1],
        "relationship_id": test_relationship_project
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?datasourceid={test_datasource_project}",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_edges.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create edge: {create_response.text}"
    created_edge = create_response.json()
    created_id = created_edge["id"]

    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/edges"
    )

    assert get_response.status_code == 200, f"Failed to get edges: {get_response.text}"
    
    all_edges = get_response.json()
    assert isinstance(all_edges, list), "Expected response to be a list"
    
    edge_ids = [edge["id"] for edge in all_edges]
    assert created_id in edge_ids, f"Created edge {created_id} not found in list of edges"
    
    our_edge = next((edge for edge in all_edges if edge["id"] == created_id), None)
    assert our_edge is not None, f"Could not find edge with id {created_id}"
    assert our_edge["originId"] == test_records[0]
    assert our_edge["destinationId"] == test_records[1]
    assert our_edge["relationshipId"] == test_relationship_project


def test_get_edge(client, organization, project, test_relationship_project, test_records, test_datasource_project, cleanup_edges):
    """Test retrieving a single edge by ID."""

    payload = {
        "origin_id": test_records[0],
        "destination_id": test_records[1],
        "relationship_id": test_relationship_project
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_edges.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create edge: {create_response.text}"
    created_id = create_response.json()["id"]

    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/{created_id}"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get edge: {get_response.text}"
    
    result = get_response.json()
    assert result["id"] == created_id
    assert result["originId"] == test_records[0]
    assert result["destinationId"] == test_records[1]
    assert result["relationshipId"] == test_relationship_project


def test_get_edge_by_origin_and_destination(client, organization, project, test_relationship_project, test_records, test_datasource_project, cleanup_edges):
    """Test retrieving an edge by origin and destination IDs."""

    origin_id = test_records[0]
    destination_id = test_records[1]
    
    payload = {
        "origin_id": origin_id,
        "destination_id": destination_id,
        "relationship_id": test_relationship_project
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_edges.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create edge: {create_response.text}"

    get_response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/by-relationship",
        params={
            "originId": origin_id,
            "destinationId": destination_id
        }
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get edge: {get_response.text}"
    
    result = get_response.json()
    assert result["originId"] == origin_id
    assert result["destinationId"] == destination_id
    assert result["relationshipId"] == test_relationship_project


def test_update_edge(client, organization, project, test_relationship_project, test_records, test_datasource_project, cleanup_edges):
    """Test updating an edge by ID."""

    create_payload = {
        "origin_id": test_records[0],
        "destination_id": test_records[1],
        "relationship_id": test_relationship_project
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_edges.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create edge: {create_response.text}"
    created_id = create_response.json()["id"]

    update_payload = {
        "relationship_id": test_relationship_project
    }

    update_response = client.put(
        f"/organizations/{organization}/projects/{project}/edges/{created_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update edge: {update_response.text}"
    
    result = update_response.json()
    assert result["id"] == created_id
    assert result["relationshipId"] == test_relationship_project


def test_update_edge_by_origin_and_destination(client, organization, project, test_relationship_project, test_records, test_datasource_project, cleanup_edges):
    """Test updating an edge by origin and destination IDs."""

    origin_id = test_records[0]
    destination_id = test_records[1]
    
    create_payload = {
        "origin_id": origin_id,
        "destination_id": destination_id,
        "relationship_id": test_relationship_project
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_edges.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create edge: {create_response.text}"

    update_payload = {
        "relationshipId": test_relationship_project
    }

    # Add originId and destinationId as query parameters in the URL
    update_response = client.put(
        f"/organizations/{organization}/projects/{project}/edges/by-relationship?originId={origin_id}&destinationId={destination_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update edge: {update_response.text}"
    
    result = update_response.json()
    assert result["originId"] == origin_id
    assert result["destinationId"] == destination_id
    assert result["relationshipId"] == test_relationship_project


def test_archive_and_unarchive_edge(client, organization, project, test_relationship_project, test_records, test_datasource_project, cleanup_edges):
    """Test archiving and unarchiving an edge by ID."""

    payload = {
        "origin_id": test_records[0],
        "destination_id": test_records[1],
        "relationship_id": test_relationship_project
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_edges.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create edge: {create_response.text}"
    created_id = create_response.json()["id"]

    archive_response = client.patch(
        f"/organizations/{organization}/projects/{project}/edges/{created_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive edge: {archive_response.text}"

    get_archived_response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/{created_id}",
        params={"hideArchived": "false"}
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived edge: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(f"\nArchived result: {archived_result}")
    assert archived_result.get("isArchived") == True, "Edge should be archived"

    unarchive_response = client.patch(
        f"/organizations/{organization}/projects/{project}/edges/{created_id}",
        params={"archive": "false"}
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive edge: {unarchive_response.text}"

    get_unarchived_response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived edge: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Edge should not be archived"


def test_archive_and_unarchive_by_origin_and_destination(client, organization, project, test_relationship_project, test_records, test_datasource_project, cleanup_edges):
    """Test archiving and unarchiving an edge by origin and destination IDs."""

    origin_id = test_records[0]
    destination_id = test_records[1]
    
    payload = {
        "origin_id": origin_id,
        "destination_id": destination_id,
        "relationship_id": test_relationship_project
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_edges.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create edge: {create_response.text}"

    archive_response = client.patch(
        f"/organizations/{organization}/projects/{project}/edges/by-relationship",
        params={
            "originId": origin_id,
            "destinationId": destination_id,
            "archive": "true"
        }
    )
    assert archive_response.status_code == 200, f"Failed to archive edge: {archive_response.text}"

    get_archived_response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/by-relationship",
        params={
            "originId": origin_id,
            "destinationId": destination_id,
            "hideArchived": "false"
        }
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived edge: {get_archived_response.text}"
    archived_result = get_archived_response.json()
    print(f"\nArchived result: {archived_result}")
    assert archived_result.get("isArchived") == True, "Edge should be archived"

    unarchive_response = client.patch(
        f"/organizations/{organization}/projects/{project}/edges/by-relationship",
        params={
            "originId": origin_id,
            "destinationId": destination_id,
            "archive": "false"
        }
    )
    
    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")
    
    assert unarchive_response.status_code == 200, f"Failed to unarchive edge: {unarchive_response.text}"

    get_unarchived_response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/by-relationship",
        params={
            "originId": origin_id,
            "destinationId": destination_id
        }
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived edge: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, "Edge should not be archived"


def test_delete_edge(client, organization, project, test_relationship_project, test_records, test_datasource_project, cleanup_edges):
    """Test permanently deleting an edge by ID."""

    payload = {
        "origin_id": test_records[0],
        "destination_id": test_records[1],
        "relationship_id": test_relationship_project
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_edges.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create edge: {create_response.text}"
    created_id = create_response.json()["id"]

    delete_response = client.delete(
        f"/organizations/{organization}/projects/{project}/edges/{created_id}"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete edge: {delete_response.text}"

    all_edges_response = client.get(
        f"/organizations/{organization}/projects/{project}/edges"
    )
    assert all_edges_response.status_code == 200, f"Failed to get all edges: {all_edges_response.text}"
    
    all_edges = all_edges_response.json()
    edge_ids = [edge["id"] for edge in all_edges]
    
    assert created_id not in edge_ids, f"Deleted edge {created_id} should not appear in list of all edges"
    print(f"Confirmed: Edge {created_id} not in list of {len(edge_ids)} edges")


def test_delete_edge_by_origin_and_destination(client, organization, project, test_relationship_project, test_records, test_datasource_project, cleanup_edges):
    """Test permanently deleting an edge by origin and destination IDs."""

    origin_id = test_records[0]
    destination_id = test_records[1]
    
    payload = {
        "origin_id": origin_id,
        "destination_id": destination_id,
        "relationship_id": test_relationship_project
    }

    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_edges.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create edge: {create_response.text}"

    delete_response = client.delete(
        f"/organizations/{organization}/projects/{project}/edges/by-relationship",
        params={
            "originId": origin_id,
            "destinationId": destination_id
        }
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete edge: {delete_response.text}"

    all_edges_response = client.get(
        f"/organizations/{organization}/projects/{project}/edges"
    )
    assert all_edges_response.status_code == 200, f"Failed to get all edges: {all_edges_response.text}"
    
    all_edges = all_edges_response.json()
    
    matching_edges = [
        edge for edge in all_edges 
        if edge.get("originId") == origin_id and edge.get("destination_id") == destination_id
    ]
    
    assert len(matching_edges) == 0, f"Deleted edge should not appear in list of all edges"
    print(f"Confirmed: Edge with origin={origin_id}, destination={destination_id} not in list")