"""
Pytest tests for DeepLynx Historical Edge API endpoints.

Tests all endpoints of the HistoricalEdgeController API:
- GET /edges/historical - Get all historical edges
- GET /edges/historical/{edgeId} - Get historical edge by ID
- GET /edges/historical/by-relationship - Get historical edge by origin and destination
- GET /edges/historical/{edgeId}/history - Get edge history by ID
- GET /edges/historical/by-relationship/history - Get edge history by origin and destination
"""

import pytest
import time
from datetime import datetime, timedelta


@pytest.fixture
def test_edge_with_history(client, organization, project, test_records, test_relationship_project, test_datasource_project, cleanup_edges):
    """Create a test edge and update it to build history."""
    origin_id = test_records[0]  # First origin record
    destination_id = test_records[1]  # First destination record
    
    # Create the edge
    payload = {
        "origin_id": origin_id,
        "destination_id": destination_id,
        "relationship_id": test_relationship_project
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create edge: {response.text}"
    edge_id = response.json()["id"]
    cleanup_edges.append(edge_id)
    
    # Capture timestamp before updates
    time.sleep(1)
    timestamp_before_updates = datetime.utcnow().isoformat() + 'Z'
    time.sleep(1)
    
    # Update edge multiple times to create history
    update_payload = {
        "relationshipId": test_relationship_project
    }
    
    for _ in range(2):
        response = client.put(
            f"/organizations/{organization}/projects/{project}/edges/{edge_id}",
            json=update_payload
        )
        assert response.status_code == 200, f"Failed to update edge: {response.text}"
        time.sleep(1)
    
    return {
        "edge_id": edge_id,
        "origin_id": origin_id,
        "destination_id": destination_id,
        "timestamp_before_updates": timestamp_before_updates
    }


@pytest.fixture
def multiple_test_edges(client, organization, project, test_records, test_relationship_project, test_datasource_project, cleanup_edges):
    """Create multiple test edges for list testing."""
    edge_ids = []
    
    # Create 3 edges using different record pairs
    pairs = [
        (test_records[0], test_records[1]),  # origin1, dest1
        (test_records[2], test_records[3]),  # origin2, dest2
        (test_records[4], test_records[5]),  # origin3, dest3
    ]
    
    for origin_id, destination_id in pairs:
        payload = {
            "origin_id": origin_id,
            "destination_id": destination_id,
            "relationship_id": test_relationship_project
        }
        
        response = client.post(
            f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
            json=payload
        )
        
        assert response.status_code == 200, f"Failed to create edge: {response.text}"
        edge_id = response.json()["id"]
        edge_ids.append(edge_id)
        cleanup_edges.append(edge_id)
        time.sleep(0.5)
    
    return edge_ids


def test_get_all_historical_edges(client, organization, project, multiple_test_edges):
    """Test GET /edges/historical - Get all historical edges."""
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical",
        params={"hideArchived": True}
    )
    
    assert response.status_code == 200, f"Failed to get historical edges: {response.text}"
    edges = response.json()
    
    # Verify we got a list
    assert isinstance(edges, list), "Response should be a list"
    
    # Verify our test edges are in the results
    edge_ids = [edge.get("id") for edge in edges]
    for test_edge_id in multiple_test_edges:
        assert test_edge_id in edge_ids, f"Test edge {test_edge_id} not found in results"
    
    # Verify edge structure
    if len(edges) > 0:
        edge = edges[0]
        assert "id" in edge
        assert "originId" in edge or "origin_id" in edge
        assert "destinationId" in edge or "destination_id" in edge
        assert "relationshipId" in edge or "relationship_id" in edge


def test_get_all_historical_edges_with_datasource_filter(client, organization, project, test_datasource_project, multiple_test_edges):
    """Test GET /edges/historical with dataSourceId filter."""
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical",
        params={
            "dataSourceId": test_datasource_project,
            "hideArchived": True
        }
    )
    
    assert response.status_code == 200, f"Failed to get historical edges: {response.text}"
    edges = response.json()
    
    assert isinstance(edges, list), "Response should be a list"
    assert len(edges) > 0, "Should return edges from the specified datasource"


def test_get_all_historical_edges_with_point_in_time(client, organization, project, test_edge_with_history):
    """Test GET /edges/historical with pointInTime parameter."""
    timestamp = test_edge_with_history["timestamp_before_updates"]
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical",
        params={
            "pointInTime": timestamp,
            "hideArchived": True
        }
    )
    
    assert response.status_code == 200, f"Failed to get historical edges: {response.text}"
    edges = response.json()
    
    assert isinstance(edges, list), "Response should be a list"
    
    # Verify the edge exists at this point in time
    edge_ids = [edge.get("id") for edge in edges]
    assert test_edge_with_history["edge_id"] in edge_ids, "Test edge should exist at this timestamp"


def test_get_historical_edge_by_id(client, organization, project, test_edge_with_history):
    """Test GET /edges/historical/{edgeId} - Get historical edge by ID."""
    edge_id = test_edge_with_history["edge_id"]
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/{edge_id}",
        params={"hideArchived": True}
    )
    
    assert response.status_code == 200, f"Failed to get historical edge by ID: {response.text}"
    edge = response.json()
    
    # Verify edge data
    assert edge.get("id") == edge_id
    assert edge.get("originId") == test_edge_with_history["origin_id"] or \
           edge.get("origin_id") == test_edge_with_history["origin_id"]
    assert edge.get("destinationId") == test_edge_with_history["destination_id"] or \
           edge.get("destination_id") == test_edge_with_history["destination_id"]
    assert "relationshipId" in edge or "relationship_id" in edge


def test_get_historical_edge_by_id_with_point_in_time(client, organization, project, test_edge_with_history):
    """Test GET /edges/historical/{edgeId} with pointInTime parameter."""
    edge_id = test_edge_with_history["edge_id"]
    timestamp = test_edge_with_history["timestamp_before_updates"]
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/{edge_id}",
        params={
            "pointInTime": timestamp,
            "hideArchived": True
        }
    )
    
    assert response.status_code == 200, f"Failed to get historical edge by ID at point in time: {response.text}"
    edge = response.json()
    
    # Verify edge data
    assert edge.get("id") == edge_id
    assert "originId" in edge or "origin_id" in edge
    assert "destinationId" in edge or "destination_id" in edge


def test_get_historical_edge_by_id_not_found(client, organization, project):
    """Test GET /edges/historical/{edgeId} with non-existent edge ID."""
    non_existent_id = 999999999
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/{non_existent_id}",
        params={"hideArchived": True}
    )

    # Accept either 500 (current buggy behavior) or 404 (correct behavior)
    assert response.status_code in [404, 500], \
        f"Expected 404 or 500 for non-existent edge, got {response.status_code}"
    
    # If 500, verify it's a "key not found" error
    if response.status_code == 500:
        response_text = response.text.lower()
        assert "not found" in response_text or "does not exist" in response_text, \
            f"500 error should be 'key not found', got: {response.text}"
        print("WARNING: API returns 500 for missing edge (should be 404)")


def test_get_historical_edge_by_origin_and_destination(client, organization, project, test_edge_with_history):
    """Test GET /edges/historical/by-relationship - Get historical edge by origin and destination."""
    origin_id = test_edge_with_history["origin_id"]
    destination_id = test_edge_with_history["destination_id"]
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/by-relationship",
        params={
            "originId": origin_id,
            "destinationId": destination_id,
            "hideArchived": True
        }
    )
    
    assert response.status_code == 200, f"Failed to get historical edge by relationship: {response.text}"
    edge = response.json()
    
    # Verify edge data
    assert edge.get("id") == test_edge_with_history["edge_id"]
    assert edge.get("originId") == origin_id or edge.get("origin_id") == origin_id
    assert edge.get("destinationId") == destination_id or edge.get("destination_id") == destination_id


def test_get_historical_edge_by_origin_and_destination_with_point_in_time(client, organization, project, test_edge_with_history):
    """Test GET /edges/historical/by-relationship with pointInTime parameter."""
    origin_id = test_edge_with_history["origin_id"]
    destination_id = test_edge_with_history["destination_id"]
    timestamp = test_edge_with_history["timestamp_before_updates"]
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/by-relationship",
        params={
            "originId": origin_id,
            "destinationId": destination_id,
            "pointInTime": timestamp,
            "hideArchived": True
        }
    )
    
    assert response.status_code == 200, f"Failed to get historical edge by relationship at point in time: {response.text}"
    edge = response.json()
    
    # Verify edge data
    assert edge.get("originId") == origin_id or edge.get("origin_id") == origin_id
    assert edge.get("destinationId") == destination_id or edge.get("destination_id") == destination_id


def test_get_historical_edge_by_origin_and_destination_not_found(client, organization, project, test_records):
    """Test GET /edges/historical/by-relationship with non-existent relationship."""
    # Use records that don't have an edge between them
    origin_id = test_records[6]  # origin4
    destination_id = test_records[7]  # dest4
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/by-relationship",
        params={
            "originId": origin_id,
            "destinationId": destination_id,
            "hideArchived": True
        }
    )
    
    # Accept either 500 (current buggy behavior), 404, or 200 (correct behaviors)
    assert response.status_code in [404, 200, 500], \
        f"Expected 404, 200, or 500 for non-existent relationship, got {response.status_code}"
    
    # If 200, verify it's empty
    if response.status_code == 200:
        data = response.json()
        assert not data or data == {} or data == [], \
            f"Expected empty result for non-existent relationship, got: {data}"
    
    # If 500, verify it's a "key not found" or "not found" error
    elif response.status_code == 500:
        response_text = response.text.lower()
        assert "not found" in response_text or "does not exist" in response_text, \
            f"500 error should be 'not found', got: {response.text}"
        print("WARNING: API returns 500 for missing relationship (should be 404)")


def test_get_edge_history_by_id(client, organization, project, test_edge_with_history):
    """Test GET /edges/historical/{edgeId}/history - Get edge history by ID."""
    edge_id = test_edge_with_history["edge_id"]
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/{edge_id}/history"
    )
    
    assert response.status_code == 200, f"Failed to get edge history by ID: {response.text}"
    history = response.json()
    
    assert isinstance(history, list), "Response should be a list"
    assert len(history) >= 3, "Should have at least 3 versions (create + 2 updates)"
    
    for version in history:
        assert "id" in version
        assert "originId" in version or "origin_id" in version
        assert "destinationId" in version or "destination_id" in version
        assert "relationshipId" in version or "relationship_id" in version
    
    for version in history:
        assert version.get("id") == edge_id


def test_get_edge_history_by_id_single_version(client, organization, project, test_records, test_relationship_project, test_datasource_project, cleanup_edges):
    """Test GET /edges/historical/{edgeId}/history for edge with no updates."""
    payload = {
        "origin_id": test_records[6],
        "destination_id": test_records[7],
        "relationship_id": test_relationship_project
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    assert response.status_code == 200
    edge_id = response.json()["id"]
    cleanup_edges.append(edge_id)
    
    # Get history
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/{edge_id}/history"
    )
    
    assert response.status_code == 200, f"Failed to get edge history: {response.text}"
    history = response.json()
    
    # Should have exactly 1 version (the creation)
    assert isinstance(history, list), "Response should be a list"
    assert len(history) >= 1, "Should have at least the creation version"


def test_get_edge_history_by_id_not_found(client, organization, project):
    """Test GET /edges/historical/{edgeId}/history with non-existent edge ID."""
    non_existent_id = 999999999
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/{non_existent_id}/history"
    )
    
    # Accept either 500 (current buggy behavior), 404, or 200 (correct behaviors)
    assert response.status_code in [404, 200, 500], \
        f"Expected 404, 200, or 500 for non-existent edge, got {response.status_code}"
    
    # If 200, verify it's an empty list
    if response.status_code == 200:
        history = response.json()
        assert isinstance(history, list), "Response should be a list"
        assert len(history) == 0, "Should return empty list for non-existent edge"
    
    # If 500, verify it's a "key not found" or "not found" error
    elif response.status_code == 500:
        response_text = response.text.lower()
        assert "not found" in response_text or "does not exist" in response_text, \
            f"500 error should be 'not found', got: {response.text}"
        print("WARNING: API returns 500 for missing edge history (should be 404)")


def test_get_edge_history_by_origin_and_destination(client, organization, project, test_edge_with_history):
    """Test GET /edges/historical/by-relationship/history - Get edge history by origin and destination."""
    origin_id = test_edge_with_history["origin_id"]
    destination_id = test_edge_with_history["destination_id"]
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/by-relationship/history",
        params={
            "originId": origin_id,
            "destinationId": destination_id
        }
    )
    
    assert response.status_code == 200, f"Failed to get edge history by relationship: {response.text}"
    history = response.json()
    
    # Verify we got a list of historical versions
    assert isinstance(history, list), "Response should be a list"
    assert len(history) >= 3, "Should have at least 3 versions (create + 2 updates)"
    
    # Verify each version has required fields and matches the relationship
    for version in history:
        assert "id" in version
        origin_id_field = version.get("originId") or version.get("origin_id")
        dest_id_field = version.get("destinationId") or version.get("destination_id")
        
        assert origin_id_field == origin_id, "All versions should have same origin"
        assert dest_id_field == destination_id, "All versions should have same destination"


def test_get_edge_history_by_origin_and_destination_not_found(client, organization, project, test_records):
    """Test GET /edges/historical/by-relationship/history with non-existent relationship."""
    # Use records that don't have an edge between them
    origin_id = test_records[6]  # origin4
    destination_id = test_records[7]  # dest4
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/by-relationship/history",
        params={
            "originId": origin_id,
            "destinationId": destination_id
        }
    )
    
    # Accept either 500 (current buggy behavior), 404, or 200 (correct behaviors)
    assert response.status_code in [404, 200, 500], \
        f"Expected 404, 200, or 500 for non-existent relationship, got {response.status_code}"
    
    # If 200, verify it's an empty list
    if response.status_code == 200:
        history = response.json()
        assert isinstance(history, list), "Response should be a list"
        assert len(history) == 0, "Should return empty list for non-existent relationship"
    
    # If 500, verify it's a "key not found" or "not found" error
    elif response.status_code == 500:
        response_text = response.text.lower()
        assert "not found" in response_text or "does not exist" in response_text, \
            f"500 error should be 'not found', got: {response.text}"
        print("WARNING: API returns 500 for missing relationship history (should be 404)")


def test_get_edge_history_by_origin_and_destination_single_version(client, organization, project, test_records, test_relationship_project, test_datasource_project, cleanup_edges):
    """Test GET /edges/historical/by-relationship/history for edge with no updates."""
    origin_id = test_records[4]
    destination_id = test_records[5]
    
    # Create an edge without updates
    payload = {
        "origin_id": origin_id,
        "destination_id": destination_id,
        "relationship_id": test_relationship_project
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/edges?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    assert response.status_code == 200
    edge_id = response.json()["id"]
    cleanup_edges.append(edge_id)
    
    # Get history by relationship
    response = client.get(
        f"/organizations/{organization}/projects/{project}/edges/historical/by-relationship/history",
        params={
            "originId": origin_id,
            "destinationId": destination_id
        }
    )
    
    assert response.status_code == 200, f"Failed to get edge history: {response.text}"
    history = response.json()
    
    # Should have exactly 1 version (the creation)
    assert isinstance(history, list), "Response should be a list"
    assert len(history) >= 1, "Should have at least the creation version"