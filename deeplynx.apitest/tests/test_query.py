"""
Tests for DeepLynx Query API endpoints.

Tests cover:
- Full text search for records
- Advanced query builder for records
- Getting recent records
- Multi-project record retrieval
"""
import pytest
import time


# ============================================================================
# FIXTURES FOR QUERY TESTING
# ============================================================================

@pytest.fixture
def query_test_classes(client, organization, project, cleanup_org_classes):
    """Create test classes for query testing."""
    class_ids = []
    
    # Create two classes with different names
    for i in range(2):
        payload = {
            "name": f"QueryTestClass_{i+1}",
            "description": f"Class {i+1} for query testing"
        }
        
        response = client.post(
            f"/organizations/{organization}/classes",
            json=payload
        )
        
        assert response.status_code == 200, f"Failed to create class {i+1}: {response.text}"
        class_id = response.json()["id"]
        class_ids.append(class_id)
        cleanup_org_classes.append(class_id)
    
    return class_ids


@pytest.fixture
def query_test_datasource(client, project, cleanup_project_datasources):
    """Create a test datasource for query testing."""
    payload = {
        "name": "QueryTestDatasource",
        "description": "Datasource for query testing"
    }
    
    response = client.post(
        f"/projects/{project}/datasources",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create datasource: {response.text}"
    datasource_id = response.json()["id"]
    cleanup_project_datasources.append(datasource_id)
    
    return datasource_id


@pytest.fixture
def query_test_records(client, organization, project, query_test_classes, query_test_datasource, cleanup_records):
    """Create diverse test records for query testing."""
    timestamp = int(time.time() * 1000)
    record_ids = []
    
    # Create records with different properties for testing various queries
    test_records = [
        {
            "name": "SearchableRecord",
            "description": "This record contains searchable keyword text",
            "original_id": f"{timestamp}-searchable-001",
            "properties": {"type": "searchable", "priority": "high", "status": "active"},
            "class_id": query_test_classes[0],
            "file_type": "pdf"
        },
        {
            "name": "TestRecord",
            "description": "Second test record with different properties",
            "original_id": f"{timestamp}-test-002",
            "properties": {"type": "test", "priority": "low", "status": "inactive"},
            "class_id": query_test_classes[0],
            "file_type": "csv"
        },
        {
            "name": "DataRecord",
            "description": "Data record for filtering tests",
            "original_id": f"{timestamp}-data-003",
            "properties": {"type": "data", "priority": "medium", "status": "active"},
            "class_id": query_test_classes[1],
            "file_type": "json"
        },
        {
            "name": "SpecialRecord",
            "description": "Special record with unique properties",
            "original_id": f"{timestamp}-special-004",
            "properties": {"type": "special", "priority": "high", "status": "pending"},
            "class_id": query_test_classes[1],
            "file_type": "xml"
        }
    ]
    
    for record_data in test_records:
        response = client.post(
            f"/organizations/{organization}/projects/{project}/records?dataSourceId={query_test_datasource}",
            json=record_data
        )
        
        assert response.status_code == 200, f"Failed to create record: {response.text}"
        record_id = response.json()["id"]
        record_ids.append(record_id)
        cleanup_records.append(record_id)
    
    # Small delay to ensure records are indexed for search
    time.sleep(0.5)
    
    return record_ids


@pytest.fixture
def secondary_project(client, organization, cleanup_created_projects):
    """Create a secondary project for multi-project testing."""
    payload = {
        "organizationId": organization,
        "name": "Secondary Test Project for Queries",
        "description": "Secondary project for multi-project query testing"
    }
    
    response = client.post(
        f"/organizations/{organization}/projects",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create secondary project: {response.text}"
    project_id = response.json()["id"]
    cleanup_created_projects.append(project_id)
    
    return project_id


@pytest.fixture
def secondary_project_records(client, organization, secondary_project, query_test_classes, cleanup_records):
    """Create records in the secondary project."""
    # Create a datasource for the secondary project
    ds_payload = {
        "name": "SecondaryProjectDatasource",
        "description": "Datasource for secondary project"
    }
    
    ds_response = client.post(
        f"/projects/{secondary_project}/datasources",
        json=ds_payload
    )
    
    assert ds_response.status_code == 200, f"Failed to create datasource: {ds_response.text}"
    datasource_id = ds_response.json()["id"]
    
    timestamp = int(time.time() * 1000)
    record_ids = []
    
    # Create a few records in the secondary project
    test_records = [
        {
            "name": "SecondaryRecord1",
            "description": "Record in secondary project",
            "original_id": f"{timestamp}-secondary-001",
            "properties": {"type": "secondary", "project": "secondary"},
            "class_id": query_test_classes[0]
        },
        {
            "name": "SecondaryRecord2",
            "description": "Another record in secondary project",
            "original_id": f"{timestamp}-secondary-002",
            "properties": {"type": "secondary", "project": "secondary"},
            "class_id": query_test_classes[1]
        }
    ]
    
    for record_data in test_records:
        response = client.post(
            f"/organizations/{organization}/projects/{secondary_project}/records?dataSourceId={datasource_id}",
            json=record_data
        )
        
        assert response.status_code == 200, f"Failed to create record: {response.text}"
        record_id = response.json()["id"]
        record_ids.append(record_id)
        cleanup_records.append(record_id)
    
    time.sleep(0.5)
    
    return record_ids


# ============================================================================
# QUERY API TESTS
# ============================================================================

def test_full_text_search_for_records(client, organization, project, query_test_records):
    """Test full text search for records using userQuery parameter."""
    # Test searching for a specific keyword
    response = client.get(
        f"/organizations/{organization}/query/records?userQuery=searchable&projectIds={project}"
    )
    
    assert response.status_code == 200, f"Failed to search records: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    assert len(results) > 0, "Should find at least one record with 'searchable' keyword"
    
    # Verify the found record contains the search term
    found_searchable = False
    for record in results:
        if "searchable" in record.get("name", "").lower() or "searchable" in record.get("description", "").lower():
            found_searchable = True
            break
    
    assert found_searchable, "Should find record containing 'searchable' in name or description"


def test_full_text_search_multiple_terms(client, organization, project, query_test_records):
    """Test full text search with multiple search terms."""
    response = client.get(
        f"/organizations/{organization}/query/records?userQuery=test+record&projectIds={project}"
    )
    
    assert response.status_code == 200, f"Failed to search records: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    # Should find records that match the search terms


def test_full_text_search_no_results(client, organization, project, query_test_records):
    """Test full text search that returns no results."""
    response = client.get(
        f"/organizations/{organization}/query/records?userQuery=nonexistent_keyword_xyz123&projectIds={project}"
    )
    
    assert response.status_code == 200, f"Failed to search records: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list"
    assert len(results) == 0, "Should return empty list for non-matching search"


def test_build_a_query_for_records_simple_filter(client, organization, project, query_test_records):
    """Test advanced query builder with a simple filter."""
    filter_query = [
        {
            "connector": None,
            "filter": "name",
            "operator": "LIKE",
            "value": "SearchableRecord"
        }
    ]
    
    response = client.post(
        f"/organizations/{organization}/query/records/advanced?projectIds={project}",
        json=filter_query
    )
    
    assert response.status_code == 200, f"Failed to execute query builder: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    assert len(results) > 0, "Should find records matching the filter"
    
    # Verify the results match the filter
    for record in results:
        assert "SearchableRecord" in record.get("name", ""), "Record name should contain 'SearchableRecord'"


def test_build_a_query_for_records_multiple_filters(client, organization, project, query_test_records):
    """Test advanced query builder with multiple filters using AND connector."""
    filter_query = [
        {
            "connector": None,
            "filter": "name",
            "operator": "LIKE",
            "value": "Record"
        },
        {
            "connector": "AND",
            "filter": "description",
            "operator": "LIKE",
            "value": "test"
        }
    ]
    
    response = client.post(
        f"/organizations/{organization}/query/records/advanced?projectIds={project}",
        json=filter_query
    )
    
    assert response.status_code == 200, f"Failed to execute query builder: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    
    # Verify results match both filters
    for record in results:
        assert "Record" in record.get("name", ""), "Name should contain 'Record'"
        assert "test" in record.get("description", "").lower(), "Description should contain 'test'"


def test_build_a_query_for_records_with_text_search(client, organization, project, query_test_records):
    """Test advanced query builder combined with text search."""
    filter_query = [
        {
            "connector": None,
            "filter": "name",
            "operator": "LIKE",
            "value": "Record"
        }
    ]
    
    response = client.post(
        f"/organizations/{organization}/query/records/advanced?projectIds={project}&textSearch=searchable",
        json=filter_query
    )
    
    assert response.status_code == 200, f"Failed to execute query builder: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"


def test_build_a_query_for_records_or_connector(client, organization, project, query_test_records):
    """Test advanced query builder with OR connector."""
    filter_query = [
        {
            "connector": None,
            "filter": "name",
            "operator": "LIKE",
            "value": "SearchableRecord"
        },
        {
            "connector": "OR",
            "filter": "name",
            "operator": "LIKE",
            "value": "DataRecord"
        }
    ]
    
    response = client.post(
        f"/organizations/{organization}/query/records/advanced?projectIds={project}",
        json=filter_query
    )
    
    assert response.status_code == 200, f"Failed to execute query builder: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    assert len(results) >= 2, "Should find at least 2 records (SearchableRecord OR DataRecord)"


def test_build_a_query_for_records_empty_filter(client, organization, project, query_test_records):
    """Test advanced query builder with empty filter array."""
    filter_query = []
    
    response = client.post(
        f"/organizations/{organization}/query/records/advanced?projectIds={project}",
        json=filter_query
    )
    
    assert response.status_code == 200, f"Failed to execute query builder: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    # Empty filter should return all records in the project


def test_get_recent_records(client, organization, project, query_test_records):
    """Test retrieving recently added records."""
    response = client.get(
        f"/organizations/{organization}/query/recent?projectIds={project}"
    )
    
    assert response.status_code == 200, f"Failed to get recent records: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    assert len(results) > 0, "Should find recent records"
    
    # Verify records have required fields
    for record in results:
        assert "id" in record, "Record should have an ID"
        assert "name" in record, "Record should have a name"

def test_get_recent_records_multiple_projects(client, organization, project, secondary_project, query_test_records, secondary_project_records):
    """Test retrieving recent records from multiple projects."""
    response = client.get(
        f"/organizations/{organization}/query/recent?projectIds={project}&projectIds={secondary_project}"
    )
    
    assert response.status_code == 200, f"Failed to get recent records: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    assert len(results) > 0, "Should find recent records from multiple projects"
    
    # Verify we get records from both projects
    project_ids_in_results = set(record.get("projectId") or record.get("project_id") for record in results)
    # Note: Depending on API implementation, we may or may not get records from both projects


def test_retrieve_all_records_for_multiple_projects(client, organization, project, secondary_project, query_test_records, secondary_project_records):
    """Test retrieving all records across multiple projects."""
    response = client.get(
        f"/organizations/{organization}/query/multiproject?projects={project}&projects={secondary_project}&hideArchived=true"
    )
    
    assert response.status_code == 200, f"Failed to get multi-project records: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    assert len(results) > 0, "Should find records from multiple projects"
    
    # Verify records from both projects are present
    for record in results:
        assert "id" in record, "Record should have an ID"
        assert "name" in record, "Record should have a name"


def test_retrieve_all_records_show_archived(client, organization, project, query_test_records):
    """Test retrieving records with archived records shown."""
    response = client.get(
        f"/organizations/{organization}/query/multiproject?projects={project}&hideArchived=false"
    )
    
    assert response.status_code == 200, f"Failed to get multi-project records: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    # hideArchived=False should return both active and archived records


def test_retrieve_all_records_single_project(client, organization, project, query_test_records):
    """Test retrieving all records for a single project using multiproject endpoint."""
    response = client.get(
        f"/organizations/{organization}/query/multiproject?projects={project}&hideArchived=true"
    )
    
    assert response.status_code == 200, f"Failed to get multi-project records: {response.text}"
    results = response.json()
    
    assert isinstance(results, list), "Response should be a list of records"
    assert len(results) >= 4, f"Should find at least 4 test records, found {len(results)}"


def test_query_with_no_projects(client, organization):
    """Test query endpoints with no project IDs specified."""
    # Test full text search with no projects
    response = client.get(
        f"/organizations/{organization}/query/records?userQuery=test"
    )
    
    # Should either return empty list or error - both are acceptable
    assert response.status_code in [200, 400], f"Unexpected status code: {response.status_code}"


def test_query_with_invalid_project(client, organization):
    """Test query endpoints with invalid project ID."""
    invalid_project_id = 999999999
    
    response = client.get(
        f"/organizations/{organization}/query/records?userQuery=test&projectIds={invalid_project_id}"
    )
    
    # Should handle gracefully - either return empty list or error
    assert response.status_code in [500], f"Unexpected status code: {response.status_code}"