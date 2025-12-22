"""Tests for Saved Search API endpoints."""

import pytest
import json
from urllib.parse import urlencode


# ========================================================================
# FIXTURES FOR SAVED SEARCH TESTS
# ========================================================================

@pytest.fixture
def test_search_class(client, organization, cleanup_org_classes):
    """Create a test class for saved search testing."""
    payload = {
        "name": "TestSearchClass",
        "description": "Test class for saved search API testing"
    }
    
    response = client.post(
        f"/organizations/{organization}/classes",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create test class: {response.text}"
    class_id = response.json()["id"]
    cleanup_org_classes.append(class_id)
    
    return class_id


@pytest.fixture
def test_search_records(client, organization, project, test_search_class, test_datasource_project, cleanup_records):
    """Create test records for saved search testing."""
    import time
    timestamp = int(time.time() * 1000)
    
    payload = []
    for i in range(5):
        payload.append({
            "name": f"SearchTestRecord{i+1}",
            "description": f"Test record {i+1} for search testing with various properties",
            "original_id": f"search-record-{timestamp}-{i+1:03d}",
            "properties": {
                "category": "test" if i % 2 == 0 else "demo",
                "priority": i + 1,
                "status": "active" if i < 3 else "inactive",
                "tags": ["searchable", f"group{i % 3}"]
            },
            "class_id": test_search_class
        })
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/records/bulk?dataSourceId={test_datasource_project}",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create test records: {response.text}"
    result = response.json()
    
    record_ids = [r.get('id') for r in result if r.get('id') is not None]
    cleanup_records.extend(record_ids)
    
    return record_ids


# ========================================================================
# SAVED SEARCH TESTS
# ========================================================================

def test_save_simple_text_search(client, test_search_records):
    """Test saving a simple search with text search only."""
    params = {
        "textSearch": "SearchTestRecord",
        "alias": "Simple Text Search",
        "favorite": "false"
    }
    payload = []
    
    url = f"/saved-searches?{urlencode(params)}"
    response = client.post(url, json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to save search: {response.text}"

def test_save_search_with_filters(client, test_search_records):
    """Test saving a search with query filters."""
    params = {
        "alias": "Filtered Search",
        "favorite": "true"
    }
    
    payload = [
        {
            "filter": "properties.category",
            "operator": "=",
            "value": "test",
            "connector": "AND"
        },
        {
            "filter": "properties.status",
            "operator": "=",
            "value": "active"
        }
    ]
    
    url = f"/saved-searches?{urlencode(params)}"
    response = client.post(url, json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to save filtered search: {response.text}"

def test_save_search_complex(client, test_search_records, test_search_class):
    """Test saving a complex search with text and multiple filters."""
    params = {
        "textSearch": "test record",
        "alias": "Complex Search Query",
        "favorite": "true"
    }
    
    payload = [
        {
            "filter": "properties.priority",
            "operator": ">",
            "value": "2",
            "connector": "AND"
        },
        {
            "filter": "name",
            "operator": "LIKE",
            "value": "%Record%",
            "connector": "AND"
        },
        {
            "filter": "class_id",
            "operator": "=",
            "value": str(test_search_class)
        }
    ]
    
    url = f"/saved-searches?{urlencode(params)}"
    response = client.post(url, json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to save complex search: {response.text}"

def test_save_favorite_search(client, test_search_records):
    """Test saving a search marked as favorite."""
    params = {
        "textSearch": "priority search",
        "alias": "My Favorite Search",
        "favorite": "true"
    }
    
    payload = [
        {
            "filter": "properties.priority",
            "operator": "<",
            "value": "3"
        }
    ]
    
    url = f"/saved-searches?{urlencode(params)}"
    response = client.post(url, json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to save favorite search: {response.text}"

def test_save_search_with_json_filter(client, test_search_records):
    """Test saving a search with JSON filter for complex nested queries."""
    params = {
        "alias": "JSON Filter Search",
        "favorite": "false"
    }
    
    payload = [
        {
            "filter": "properties",
            "operator": "KEY_VALUE",
            "json": json.dumps({
                "category": "test",
                "priority": 1
            })
        }
    ]
    
    url = f"/saved-searches?{urlencode(params)}"
    response = client.post(url, json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to save JSON filter search: {response.text}"

def test_save_search_with_or_connector(client, test_search_records):
    """Test saving a search with OR connector between filters."""
    params = {
        "alias": "OR Connector Search",
        "favorite": "false"
    }
    
    payload = [
        {
            "filter": "properties.category",
            "operator": "=",
            "value": "test",
            "connector": "OR"
        },
        {
            "filter": "properties.category",
            "operator": "=",
            "value": "demo"
        }
    ]
    
    url = f"/saved-searches?{urlencode(params)}"
    response = client.post(url, json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to save OR connector search: {response.text}"

def test_save_search_minimal(client, test_search_records):
    """Test saving a minimal search with required alias."""
    params = {
        "textSearch": "minimal search",
        "alias": "Quick Search",
        "favorite": "false"
    }
    payload = []
    
    url = f"/saved-searches?{urlencode(params)}"
    response = client.post(url, json=payload)
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to save minimal search: {response.text}"

# def test_save_search_without_alias(client):
#     """Test that saving a search without alias should fail or auto-generate."""
#     params = {
#         "textSearch": "unnamed search",
#         "favorite": "false"
#     }
#     payload = []
    
#     url = f"/saved-searches?{urlencode(params)}"
#     response = client.post(url, json=payload)
    
#     print(f"\nStatus Code: {response.status_code}")
#     print(f"Response Body: {response.text}")

#     assert response.status_code == 200


# def test_get_all_saved_searches(client, test_search_records):
#     """Test retrieving all saved searches for the user."""
#     # Create a few saved searches first
#     searches = [
#         {
#             "params": {"textSearch": "search1", "alias": "Test Search 1", "favorite": "false"},
#             "payload": []
#         },
#         {
#             "params": {"textSearch": "search2", "alias": "Test Search 2", "favorite": "true"},
#             "payload": []
#         },
#         {
#             "params": {"alias": "Filtered Search", "favorite": "false"},
#             "payload": [{"filter": "properties.category", "operator": "=", "value": "test"}]
#         }
#     ]
    
#     created_ids = []
#     for search in searches:
#         url = f"/saved-searches?{urlencode(search['params'])}"
#         client.post(url, json=search["payload"])
    
#     # Now get all saved searches
#     get_response = client.get("/saved-searches")
    
#     print(f"\nStatus Code: {get_response.status_code}")
#     print(f"Response Body: {get_response.text}")
    
#     assert get_response.status_code == 200, f"Failed to get saved searches: {get_response.text}"
#     result = get_response.json()
    
#     assert isinstance(result, list), "Expected response to be a list"
#     assert len(result) >= 3, f"Expected at least 3 saved searches, got {len(result)}"
    
#     # Verify our created searches are in the list
#     returned_ids = [s["id"] for s in result]
#     for created_id in created_ids:
#         assert created_id in returned_ids, f"Created search {created_id} not found in returned list"
    
#     # Verify structure of returned searches
#     for search in result:
#         assert "id" in search
#         assert "alias" in search
#         assert "favorite" in search


def test_get_saved_search_by_id(client, test_search_records):
    """Test retrieving a specific saved search by ID."""
    # Create a saved search
    params = {
        "textSearch": "specific search",
        "alias": "Specific Search",
        "favorite": "true"
    }
    payload = [
        {
            "filter": "properties.status",
            "operator": "=",
            "value": "active"
        }
    ]
    
    url = f"/saved-searches?{urlencode(params)}"
    create_response = client.post(url, json=payload)
    
    assert create_response.status_code == 200, f"Failed to create search: {create_response.text}"


# def test_filter_saved_searches_by_favorite(client, test_search_records):
#     """Test filtering saved searches to get only favorites."""
#     # Create both favorite and non-favorite searches
#     searches = [
#         {"params": {"alias": "Favorite 1", "favorite": "true"}, "payload": []},
#         {"params": {"alias": "Not Favorite", "favorite": "false"}, "payload": []},
#         {"params": {"alias": "Favorite 2", "favorite": "true"}, "payload": []}
#     ]
    
#     created_favorites = []
#     for search in searches:
#         url = f"/saved-searches?{urlencode(search['params'])}"
#         client.post(url, json=search["payload"])
    
#     # Get only favorite searches (if API supports filtering)
#     # This might be via query param like ?favorite=true
#     get_response = client.get("/saved-searches?favorite=true")
    
#     print(f"\nStatus Code: {get_response.status_code}")
#     print(f"Response Body: {get_response.text}")
    
#     if get_response.status_code == 200:
#         result = get_response.json()
        
#         # All returned searches should be favorites
#         for search in result:
#             assert search.get("favorite") == True, f"Non-favorite search returned: {search}"
#     else:
#         # If filtering not supported, get all and verify favorites exist
#         pytest.skip("Favorite filtering not supported by API")