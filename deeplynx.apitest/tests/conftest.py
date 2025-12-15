"""
Shared pytest fixtures for DeepLynx API tests.

Session-scoped fixtures (auth, org, project) are created once and shared.
Function-scoped fixtures handle per-test cleanup.
"""
from dotenv import load_dotenv
load_dotenv() 

import pytest
import requests
import os
from typing import Dict, List
import time


class DeepLynxClient:
    """Simple client wrapper for DeepLynx API interactions."""
    
    def __init__(self, base_url: str, token: str):
        self.base_url = base_url.rstrip('/')
        self.token = token
        self.headers = {
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json"
        }
    
    def get(self, endpoint: str, params: Dict = None):
        url = f"{self.base_url}{endpoint}"
        return requests.get(url, params=params, headers=self.headers)
    
    def post(self, endpoint: str, json: Dict = None):
        url = f"{self.base_url}{endpoint}"
        return requests.post(url, json=json, headers=self.headers)
    
    def put(self, endpoint: str, json: Dict = None):
        url = f"{self.base_url}{endpoint}"
        return requests.put(url, json=json, headers=self.headers)
    
    def patch(self, endpoint: str, params: Dict = None, json: Dict = None):
        url = f"{self.base_url}{endpoint}"
        return requests.patch(url, params=params, json=json, headers=self.headers)
    
    def delete(self, endpoint: str, params: Dict = None):
        url = f"{self.base_url}{endpoint}"
        return requests.delete(url, params=params, headers=self.headers)


# ============================================================================
# SESSION FIXTURES (Created once, shared across all tests)
# ============================================================================

@pytest.fixture(scope="session")
def base_url():
    """Get base URL from environment."""
    url = os.getenv("DEEPLYNX_URL", "http://localhost:5095/api/v1")
    return url.rstrip('/')


@pytest.fixture(scope="session")
def auth_token(base_url):
    """Create authentication token."""
    api_key = os.getenv("DEEPLYNX_API_KEY")
    api_secret = os.getenv("DEEPLYNX_API_SECRET")
    
    if not api_key or not api_secret:
        pytest.fail("Missing DEEPLYNX_API_KEY or DEEPLYNX_API_SECRET environment variables")
    
    response = requests.post(
        f"{base_url}/oauth/tokens",
        json={
            "apiKey": api_key,
            "apiSecret": api_secret,
            "expirationMinutes": 180
        }
    )
    
    assert response.status_code == 200, f"Authentication failed: {response.text}"
    return response.text.strip('"')


@pytest.fixture(scope="session")
def client(base_url, auth_token):
    """Create authenticated API client."""
    return DeepLynxClient(base_url, auth_token)

@pytest.fixture(scope="session")
def current_user_id(client):
    """Get the current user's ID for user management tests."""
    response = client.get("/users/current")
    
    if response.status_code == 200:
        return response.json().get("id")
    
    # If /users/current doesn't exist, try to extract from token or skip
    pytest.skip("Cannot get current user ID - /users/current endpoint not available")
    

@pytest.fixture(scope="session")
def organization(client, current_user_id):
    """Create or reuse test organization."""
    org_name = "DeepLynx API Test Organization"
    
    # Check if exists
    response = client.get("/organizations")
    for org in response.json():
        if org.get("name") == org_name:
            return org.get("id")
    
    # Create new
    response = client.post(
        "/organizations",
        json={"name": org_name, "description": "Test organization"}
    )
    assert response.status_code == 200

    add_user_url = f"/organizations/{organization}/user?userId={current_user_id}&isAdmin=true"
    add_user_response = client.post(add_user_url)

    assert add_user_response.status_code == 200

    return response.json().get("id")


@pytest.fixture(scope="session")
def project(client, organization):
    """Create or reuse test project."""
    project_name = "DeepLynx API Test Project"
    
    # Check if exists
    response = client.get(f"/organizations/{organization}/projects")
    for proj in response.json():
        if proj.get("name") == project_name:
            return proj.get("id")
    
    # Create new
    response = client.post(
        f"/organizations/{organization}/projects",
        json={
            "organizationId": organization,
            "name": project_name,
            "description": "Test project"
        }
    )
    assert response.status_code == 200
    return response.json().get("id")


# ============================================================================
# SESSION FIXTURES FOR EDGE TESTING
# ============================================================================

@pytest.fixture(scope="session")
def origin_class(client, project):
    """Create a test origin class for edges (session-scoped)."""
    payload = {
        "name": "pytest_EdgeOriginClass",
        "description": "Origin class for edge testing"
    }
    
    response = client.post(
        f"/projects/{project}/classes",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create origin class: {response.text}"
    class_id = response.json()["id"]
    
    yield class_id
    
    # Cleanup after all tests
    try:
        client.delete(f"/projects/{project}/classes/{class_id}")
    except:
        pass


@pytest.fixture(scope="session")
def destination_class(client, organization):
    """Create a test destination class for edges (session-scoped)."""
    payload = {
        "name": "pytest_EdgeDestinationClass",
        "description": "Destination class for edge testing"
    }
    
    response = client.post(
        f"/organizations/{organization}/classes",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create destination class: {response.text}"
    class_id = response.json()["id"]
    
    yield class_id
    
    # Cleanup after all tests
    try:
        client.delete(f"/organizations/{organization}/classes/{class_id}")
    except:
        pass

@pytest.fixture(scope="session")
def test_relationship_org(client, organization, origin_class, destination_class):
    """Create a test relationship for edges (session-scoped)."""
    payload = {
        "name": "pytest_TestRelationship_org",
        "description": "Test relationship for edge testing",
        "originClassId": origin_class,
        "destinationClassId": destination_class
    }
    
    response = client.post(
        f"/organizations/{organization}/relationships",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create relationship: {response.text}"
    relationship_id = response.json()["id"]
    
    yield relationship_id
    
    # Cleanup after all tests
    try:
        client.delete(f"/organizations/{organization}/relationships/{relationship_id}")
    except:
        pass

@pytest.fixture(scope="session")
def test_relationship_project(client, project, origin_class, destination_class):
    """Create a test relationship for edges (session-scoped)."""
    payload = {
        "name": "pytest_TestRelationship",
        "description": "Test relationship for edge testing",
        "originClassId": origin_class,
        "destinationClassId": destination_class
    }
    
    response = client.post(
        f"/projects/{project}/relationships",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create relationship: {response.text}"
    relationship_id = response.json()["id"]
    
    yield relationship_id
    
    # Cleanup after all tests
    try:
        client.delete(f"/projects/{project}/relationships/{relationship_id}")
    except:
        pass


@pytest.fixture(scope="session")
def test_datasource_org(client, organization):
    """Create a test datasource for records (session-scoped)."""
    payload = {
        "name": "pytest_TestDatasource",
        "description": "Test datasource for edge testing"
    }
    
    response = client.post(
        f"/organizations/{organization}/datasources",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create datasource: {response.text}"
    datasource_id = response.json()["id"]
    
    yield datasource_id
    
    # Cleanup after all tests
    try:
        client.delete(f"/organizations/{organization}/datasources/{datasource_id}")
    except:
        pass

@pytest.fixture(scope="session")
def test_datasource_project(client, project):
    """Create a test datasource for records (session-scoped)."""
    payload = {
        "name": "pytest_TestDatasource",
        "description": "Test datasource for edge testing"
    }
    
    response = client.post(
        f"/projects/{project}/datasources",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create datasource: {response.text}"
    datasource_id = response.json()["id"]
    
    yield datasource_id
    
    # Cleanup after all tests
    try:
        client.delete(f"/projects/{project}/datasources/{datasource_id}")
    except:
        pass


# ============================================================================
# CLEANUP FIXTURES (Function-scoped, one per resource type)
# ============================================================================

@pytest.fixture
def cleanup_org_classes(client, organization):
    """Track and cleanup organization-level classes."""
    created_ids = []
    yield created_ids
    for class_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/classes/{class_id}")
        except:
            pass


@pytest.fixture
def cleanup_project_classes(client, project):
    """Track and cleanup project-level classes."""
    created_ids = []
    yield created_ids
    for class_id in created_ids:
        try:
            client.delete(f"/projects/{project}/classes/{class_id}")
        except:
            pass


@pytest.fixture
def cleanup_org_relationships(client, organization):
    """Track and cleanup organization-level relationships."""
    created_ids = []
    yield created_ids
    for rel_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/relationships/{rel_id}")
        except:
            pass


@pytest.fixture
def cleanup_project_relationships(client, project):
    """Track and cleanup project-level relationships."""
    created_ids = []
    yield created_ids
    for rel_id in created_ids:
        try:
            client.delete(f"/projects/{project}/relationships/{rel_id}")
        except:
            pass


@pytest.fixture
def cleanup_org_datasources(client, organization):
    """Track and cleanup data sources."""
    created_ids = []
    yield created_ids
    for ds_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/datasources/{ds_id}")
        except:
            pass


@pytest.fixture
def cleanup_project_datasources(client, project):
    """Track and cleanup data sources."""
    created_ids = []
    yield created_ids
    for ds_id in created_ids:
        try:
            client.delete(f"/projects/{project}/datasources/{ds_id}")
        except:
            pass


@pytest.fixture
def cleanup_edges(client, organization, project):
    """Track and cleanup edges."""
    created_ids = []
    yield created_ids
    for edge_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/projects/{project}/edges/{edge_id}")
        except:
            pass


@pytest.fixture
def cleanup_records(client, organization, project):
    """Track and cleanup records."""
    created_ids = []
    yield created_ids
    for record_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/projects/{project}/records/{record_id}")
        except:
            pass

@pytest.fixture
def cleanup_groups(client, organization):
    """Track and cleanup groups created during tests."""
    created_ids = []
    yield created_ids
    for group_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/groups/{group_id}")
        except:
            pass


@pytest.fixture
def test_group(client, organization, cleanup_groups):
    """Create a test group for operations (function-scoped)."""
    payload = {
        "name": "pytest_TestGroup",
        "description": "Test group for pytest operations"
    }
    
    response = client.post(
        f"/organizations/{organization}/groups",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create test group: {response.text}"
    group_id = response.json()["id"]
    cleanup_groups.append(group_id)
    
    return group_id

@pytest.fixture
def historical_test_record(client, organization, project, origin_class, test_datasource_project, cleanup_records):
    """Create a record and update it to generate historical data."""
    timestamp = int(time.time() * 1000)
    
    # Create initial record
    record_payload = {
        "name": "HistoricalTestRecord",
        "description": "Original description",
        "original_id": f"{timestamp}-hist-record-001",
        "properties": {"version": "1"},
        "class_id": origin_class
    }
    
    create_response = client.post(
        f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
        json=record_payload
    )
    
    assert create_response.status_code == 200, f"Failed to create historical test record: {create_response.text}"
    record_id = create_response.json()["id"]
    cleanup_records.append(record_id)
    
    # Update the record multiple times to create history
    for i in range(3):
        update_payload = {
            "name": "HistoricalTestRecord",
            "description": f"Updated description version {i+2}",
            "original_id": f"{timestamp}-hist-record-001",
            "properties": {"version": str(i+2)},
            "class_id": origin_class
        }
        
        update_response = client.put(
            f"/organizations/{organization}/projects/{project}/records/{record_id}?dataSourceId={test_datasource_project}",
            json=update_payload
        )
        
        assert update_response.status_code == 200, f"Failed to update record (iteration {i+1}): {update_response.text}"
    
    return record_id

@pytest.fixture
def cleanup_oauth_applications(client):
    """Track and cleanup OAuth applications."""
    created_ids = []
    yield created_ids
    for app_id in created_ids:
        try:
            client.delete(f"/oauth/applications/{app_id}")
        except:
            pass

@pytest.fixture
def cleanup_org_storages(client, organization):
    """Track and cleanup organization-level storages."""
    created_ids = []
    yield created_ids
    for storage_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/storages/{storage_id}")
        except:
            pass

@pytest.fixture
def cleanup_project_storages(client, organization, project):
    """Track and cleanup project-level storages."""
    created_ids = []
    yield created_ids
    for storage_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/projects/{project}/storages/{storage_id}")
        except:
            pass

@pytest.fixture
def cleanup_created_organizations(client):
    """Track and cleanup created organizations"""
    created_ids = []
    yield created_ids
    for organization_id in created_ids:
        try:
            client.delete(f"/organizations/{organization_id}")
        except:
            pass

@pytest.fixture
def cleanup_created_projects(client):
    """Track and cleanup created projects"""
    created_ids = []
    yield created_ids
    for project_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/projects/{project_id}")
        except:
            pass

@pytest.fixture
def cleanup_current_user_added_to_organizations(client, current_user_id):
    organization_ids = []
    yield organization_ids
    for org_id in organization_ids:
        try:
            client.delete(f"/organizations/{org_id}/user?userId={current_user_id}")
        except:
            pass

@pytest.fixture
def cleanup_current_user_added_to_projects(client, organization, current_user_id):
    project_ids = []
    yield project_ids
    for project_id in project_ids:
        try:
            client.delete(f"/organizations/{organization}/projects/{project_id}/user?userId={current_user_id}")
        except:
            pass
    
@pytest.fixture
def cleanup_org_permissions(client, organization):
    """Track and cleanup organization-level permissions."""
    created_ids = []
    yield created_ids
    for permission_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/permissions/{permission_id}")
        except:
            pass

@pytest.fixture
def cleanup_project_permissions(client, organization):
    """Track and cleanup project-level permissions."""
    created_ids = []
    yield created_ids
    for permission_id in created_ids:
        try:
            client.delete(f"/projectss/{project}/permissions/{permission_id}")
        except:
            pass

@pytest.fixture
def cleanup_project_members(client, organization):
    members = []
    yield members
    for project_id, user_id, group_id in members:
        try:
            if user_id:
                client.delete(f"/organizations/{organization}/projects/{project_id}/members?userId={user_id}")
            elif group_id:
                client.delete(f"/organizations/{organization}/projects/{project_id}/members?groupId={group_id}")
        except:
            pass

@pytest.fixture
def cleanup_project_roles(client, organization):
    roles = []
    yield roles
    for project_id, role_id in roles:
        try:
            client.delete(f"/organizations/{organization}/projects/{project_id}/roles/{role_id}")
        except:
            pass

@pytest.fixture
def cleanup_created_groups(client, organization):
    groups = []
    yield groups
    for group_id in groups:
        try:
            client.delete(f"/organizations/{organization}/groups/{group_id}")
        except:
            pass

@pytest.fixture
def cleanup_org_tags(client, organization):
    """Track and cleanup organization-level tags."""
    created_ids = []
    yield created_ids
    for tag_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/tags/{tag_id}")
        except:
            pass


@pytest.fixture
def cleanup_project_tags(client, project):
    """Track and cleanup project-level tags."""
    created_ids = []
    yield created_ids
    for tag_id in created_ids:
        try:
            client.delete(f"/projects/{project}/tags/{tag_id}")
        except:
            pass

@pytest.fixture
def cleanup_org_roles(client, organization):
    """Track and cleanup organization-level roles."""
    created_ids = []
    yield created_ids
    for role_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/roles/{role_id}")
        except:
            pass

@pytest.fixture
def cleanup_project_roles(client, organization, project):
    """Track and cleanup organization-level roles."""
    created_ids = []
    yield created_ids
    for role_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/projects/{project}roles/{role_id}")
        except:
            pass

@pytest.fixture
def cleanup_created_users(client):
    """Track and cleanup created users"""
    created_ids = []
    yield created_ids
    for user_id in created_ids:
        try:
            client.delete(f"/users/{user_id}")
        except:
            pass

# ============================================================================
# FUNCTION-SCOPED FIXTURES FOR EDGE TESTING
# ============================================================================

@pytest.fixture
def test_records(client, organization, project, origin_class, destination_class, test_datasource_project, cleanup_records):
    """Create test records for edge testing (function-scoped, cleaned up after each test)."""
    import time
    
    record_ids = []
    
    # Create 4 origin records and 4 destination records
    for i in range(4):
        # Make original_id unique by adding timestamp (like in the old working code)
        timestamp = int(time.time() * 1000)
        
        # Origin record - using snake_case field names as in the old working code
        origin_payload = {
            "name": f"OriginRecord{i+1}",
            "description": f"Test origin record {i+1}",
            "original_id": f"{timestamp}-origin-{i+1:03d}",  # Number-like string with timestamp
            "properties": {},  # Required field
            "class_id": origin_class
        }
        
        # Need to pass dataSourceId as query parameter
        origin_response = client.post(
            f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
            json=origin_payload
        )
        
        assert origin_response.status_code == 200, f"Failed to create origin record: {origin_response.text}"
        origin_id = origin_response.json()["id"]
        record_ids.append(origin_id)
        cleanup_records.append(origin_id)
        
        # Destination record - using snake_case field names as in the old working code
        dest_payload = {
            "name": f"DestinationRecord{i+1}",
            "description": f"Test destination record {i+1}",
            "original_id": f"{timestamp}-dest-{i+1:03d}",  # Number-like string with timestamp
            "properties": {},  # Required field
            "class_id": destination_class
        }
        
        # Need to pass dataSourceId as query parameter
        dest_response = client.post(
            f"/organizations/{organization}/projects/{project}/records?dataSourceId={test_datasource_project}",
            json=dest_payload
        )
        
        assert dest_response.status_code == 200, f"Failed to create destination record: {dest_response.text}"
        dest_id = dest_response.json()["id"]
        record_ids.append(dest_id)
        cleanup_records.append(dest_id)
    
    # Return list of record IDs: [origin1, dest1, origin2, dest2, origin3, dest3, origin4, dest4]
    # For convenience, tests can use:
    # - test_records[0], test_records[2], test_records[4], test_records[6] for origin records
    # - test_records[1], test_records[3], test_records[5], test_records[7] for destination records
    yield record_ids