"""Tests for Metrics API endpoints - System, Organization, and Project Level."""


# ========================================================================
# SYSTEM-LEVEL DATASOURCE COUNT TESTS
# ========================================================================

def test_get_datasource_count_system(client):
    """Test retrieving system-wide datasource count."""
    response = client.get("/metrics/datasources/count")

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 200, f"Failed to get datasource count: {response.text}"
    count = response.json()
    assert isinstance(count, int), "Expected response to be an integer"
    assert count >= 0, "Count should be non-negative"


def test_get_datasource_count_system_increases_after_create(client, organization, cleanup_org_datasources):
    """Test that system-wide datasource count increases after creating a new datasource."""
    # Get baseline count
    baseline_response = client.get("/metrics/datasources/count")
    assert baseline_response.status_code == 200, f"Failed to get baseline count: {baseline_response.text}"
    baseline_count = baseline_response.json()

    # Create a datasource
    payload = {
        "name": "pytest_metricsCountTestDatasource",
        "description": "A test datasource for metrics count testing"
    }
    create_response = client.post(
        f"/organizations/{organization}/datasources",
        json=payload
    )
    if create_response.status_code == 200:
        cleanup_org_datasources.append(create_response.json()["id"])
    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"

    # Count should have increased by 1
    new_response = client.get("/metrics/datasources/count")
    assert new_response.status_code == 200, f"Failed to get new count: {new_response.text}"
    new_count = new_response.json()
    assert new_count == baseline_count + 1, f"Expected count {baseline_count + 1}, got {new_count}"


def test_get_datasource_count_system_excludes_archived(client, organization, cleanup_org_datasources):
    """Test that system-wide datasource count excludes archived datasources by default."""
    # Create a datasource
    payload = {
        "name": "pytest_metricsArchiveTestDatasource",
        "description": "A test datasource for metrics archive count testing"
    }
    create_response = client.post(
        f"/organizations/{organization}/datasources",
        json=payload
    )
    if create_response.status_code == 200:
        cleanup_org_datasources.append(create_response.json()["id"])
    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    # Get count before archiving
    count_before = client.get("/metrics/datasources/count")
    assert count_before.status_code == 200
    count_before_val = count_before.json()

    # Archive the datasource
    archive_response = client.patch(
        f"/organizations/{organization}/datasources/{created_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive datasource: {archive_response.text}"

    # Default count should drop by 1
    count_hidden = client.get("/metrics/datasources/count")
    assert count_hidden.status_code == 200
    assert count_hidden.json() == count_before_val - 1

    # Count with hideArchived=false should include the archived one
    count_all = client.get(
        "/metrics/datasources/count",
        params={"hideArchived": "false"}
    )
    assert count_all.status_code == 200
    assert count_all.json() == count_before_val


# ========================================================================
# ORGANIZATION-LEVEL DATASOURCE COUNT TESTS
# ========================================================================

def test_get_datasource_count_org(client, organization, cleanup_org_datasources):
    """Test counting datasources at organization level."""
    # Get baseline count
    baseline_response = client.get(
        f"/organization/{organization}/metrics/count"
    )
    assert baseline_response.status_code == 200, f"Failed to get count: {baseline_response.text}"
    baseline_count = baseline_response.json()
    assert isinstance(baseline_count, int), "Expected response to be an integer"

    # Create a datasource
    payload = {
        "name": "pytest_orgCountTestDatasource",
        "description": "A test datasource for count endpoint testing"
    }
    create_response = client.post(
        f"/organizations/{organization}/datasources",
        json=payload
    )
    if create_response.status_code == 200:
        cleanup_org_datasources.append(create_response.json()["id"])
    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"

    # Count should have increased by 1
    new_response = client.get(
        f"/organization/{organization}/metrics/count"
    )
    assert new_response.status_code == 200, f"Failed to get count: {new_response.text}"
    new_count = new_response.json()
    assert new_count == baseline_count + 1, f"Expected count {baseline_count + 1}, got {new_count}"


def test_get_datasource_count_org_excludes_archived(client, organization, cleanup_org_datasources):
    """Test that org-level count endpoint excludes archived datasources by default."""
    # Create a datasource
    payload = {
        "name": "pytest_orgCountArchiveTest",
        "description": "A test datasource for archive count testing"
    }
    create_response = client.post(
        f"/organizations/{organization}/datasources",
        json=payload
    )
    if create_response.status_code == 200:
        cleanup_org_datasources.append(create_response.json()["id"])
    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    # Get count before archiving
    count_before = client.get(f"/organization/{organization}/metrics/count")
    assert count_before.status_code == 200
    count_before_val = count_before.json()

    # Archive the datasource
    archive_response = client.patch(
        f"/organizations/{organization}/datasources/{created_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive datasource: {archive_response.text}"

    # Count with hideArchived=true (default) should be one less
    count_hidden = client.get(f"/organization/{organization}/metrics/count")
    assert count_hidden.status_code == 200
    assert count_hidden.json() == count_before_val - 1

    # Count with hideArchived=false should include the archived one
    count_all = client.get(
        f"/organization/{organization}/metrics/count",
        params={"hideArchived": "false"}
    )
    assert count_all.status_code == 200
    assert count_all.json() == count_before_val


# ========================================================================
# PROJECT-LEVEL DATASOURCE COUNT TESTS
# ========================================================================

def test_get_datasource_count_project(client, organization, project, cleanup_project_datasources):
    """Test counting datasources at project level."""
    # Get baseline count
    baseline_response = client.get(
        f"/organizations/{organization}/projects/{project}/metrics/count"
    )
    assert baseline_response.status_code == 200, f"Failed to get count: {baseline_response.text}"
    baseline_count = baseline_response.json()
    assert isinstance(baseline_count, int), "Expected response to be an integer"

    # Create a datasource
    payload = {
        "name": "pytest_projectCountTestDatasource",
        "description": "A test datasource for count endpoint testing"
    }
    create_response = client.post(
        f"/projects/{project}/datasources",
        json=payload
    )
    if create_response.status_code == 200:
        cleanup_project_datasources.append(create_response.json()["id"])
    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"

    # Count should have increased by 1
    new_response = client.get(
        f"/organizations/{organization}/projects/{project}/metrics/count"
    )
    assert new_response.status_code == 200, f"Failed to get count: {new_response.text}"
    new_count = new_response.json()
    assert new_count == baseline_count + 1, f"Expected count {baseline_count + 1}, got {new_count}"


def test_get_datasource_count_project_excludes_archived(client, organization, project, cleanup_project_datasources):
    """Test that project-level count endpoint excludes archived datasources by default."""
    # Create a datasource
    payload = {
        "name": "pytest_projectCountArchiveTest",
        "description": "A test datasource for archive count testing"
    }
    create_response = client.post(
        f"/projects/{project}/datasources",
        json=payload
    )
    if create_response.status_code == 200:
        cleanup_project_datasources.append(create_response.json()["id"])
    assert create_response.status_code == 200, f"Failed to create datasource: {create_response.text}"
    created_id = create_response.json()["id"]

    # Get count before archiving
    count_before = client.get(f"/organizations/{organization}/projects/{project}/metrics/count")
    assert count_before.status_code == 200
    count_before_val = count_before.json()

    # Archive the datasource
    archive_response = client.patch(
        f"/projects/{project}/datasources/{created_id}",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive datasource: {archive_response.text}"

    # Count with hideArchived=true (default) should be one less
    count_hidden = client.get(f"/organizations/{organization}/projects/{project}/metrics/count")
    assert count_hidden.status_code == 200
    assert count_hidden.json() == count_before_val - 1

    # Count with hideArchived=false should include the archived one
    count_all = client.get(
        f"/organizations/{organization}/projects/{project}/metrics/count",
        params={"hideArchived": "false"}
    )
    assert count_all.status_code == 200
    assert count_all.json() == count_before_val
