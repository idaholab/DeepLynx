# ============================================================================
# GROUP CRUD TESTS
# ============================================================================

def test_get_all_groups_in_org(client, organization, cleanup_groups):
    """Test retrieving all groups in an organization."""
    # Create a few test groups
    test_groups = []
    for i in range(3):
        payload = {
            "name": f"pytest_Group_{i}",
            "description": f"Test group {i}"
        }
        response = client.post(
            f"/organizations/{organization}/groups",
            json=payload
        )
        assert response.status_code == 200
        group_id = response.json()["id"]
        test_groups.append(group_id)
        cleanup_groups.append(group_id)
    
    # Get all groups
    response = client.get(
        f"/organizations/{organization}/groups",
        params={"hideArchived": True}
    )
    
    assert response.status_code == 200, f"Failed to get groups: {response.text}"
    groups = response.json()
    
    # Verify our test groups are in the response
    group_ids = [g["id"] for g in groups]
    for test_id in test_groups:
        assert test_id in group_ids


def test_create_group(client, organization, cleanup_groups):
    """Test creating a new group."""
    payload = {
        "name": "pytest_NewGroup",
        "description": "A brand new test group"
    }
    
    response = client.post(
        f"/organizations/{organization}/groups",
        json=payload
    )
    
    assert response.status_code == 200, f"Failed to create group: {response.text}"
    
    group = response.json()
    cleanup_groups.append(group["id"])
    
    assert group["name"] == payload["name"]
    assert group["description"] == payload["description"]


def test_get_group(client, organization, test_group):
    """Test retrieving a specific group by ID."""
    response = client.get(
        f"/organizations/{organization}/groups/{test_group}",
        params={"hideArchived": True}
    )
    
    assert response.status_code == 200, f"Failed to get group: {response.text}"
    
    group = response.json()
    assert group["id"] == test_group


def test_update_group(client, organization, test_group):
    """Test updating a group's name and description."""
    updated_payload = {
        "name": "pytest_UpdatedGroup",
        "description": "This group has been updated"
    }
    
    response = client.put(
        f"/organizations/{organization}/groups/{test_group}",
        json=updated_payload
    )
    
    assert response.status_code == 200, f"Failed to update group: {response.text}"
    
    updated_group = response.json()
    assert updated_group["name"] == updated_payload["name"]
    assert updated_group["description"] == updated_payload["description"]


def test_delete_group(client, organization, cleanup_groups):
    """Test deleting a group."""
    # Create a group to delete
    payload = {
        "name": "pytest_GroupToDelete",
        "description": "This group will be deleted"
    }
    
    create_response = client.post(
        f"/organizations/{organization}/groups",
        json=payload
    )
    assert create_response.status_code == 200
    group_id = create_response.json()["id"]
    
    # Delete the group
    delete_response = client.delete(
        f"/organizations/{organization}/groups/{group_id}"
    )
    
    assert delete_response.status_code == 200, f"Failed to delete group: {delete_response.text}"


def test_archive_and_unarchive_group(client, organization, cleanup_groups):
    """Test archiving and unarchiving a group."""
    # Create a group to archive
    payload = {
        "name": "pytest_GroupToArchive",
        "description": "This group will be archived and unarchived"
    }
    
    create_response = client.post(
        f"/organizations/{organization}/groups",
        json=payload
    )
    assert create_response.status_code == 200
    group_id = create_response.json()["id"]
    cleanup_groups.append(group_id)
    
    # Archive the group
    archive_response = client.patch(
        f"/organizations/{organization}/groups/{group_id}",
        params={"archive": True}
    )
    assert archive_response.status_code == 200, f"Failed to archive group: {archive_response.text}"
    
    # Verify group is archived (should not appear in hideArchived=True query)
    list_response = client.get(
        f"/organizations/{organization}/groups",
        params={"hideArchived": True}
    )
    assert list_response.status_code == 200
    visible_ids = [g["id"] for g in list_response.json()]
    assert group_id not in visible_ids
    
    # Unarchive the group
    unarchive_response = client.patch(
        f"/organizations/{organization}/groups/{group_id}",
        params={"archive": False}
    )
    assert unarchive_response.status_code == 200, f"Failed to unarchive group: {unarchive_response.text}"
    
    # Verify group is visible again
    visible_response = client.get(
        f"/organizations/{organization}/groups",
        params={"hideArchived": True}
    )
    assert visible_response.status_code == 200
    visible_ids = [g["id"] for g in visible_response.json()]
    assert group_id in visible_ids


# ============================================================================
# GROUP USER MANAGEMENT TESTS
# ============================================================================

def test_get_all_group_members(client, organization, test_group):
    """Test retrieving all members of a group."""
    response = client.get(
        f"/organizations/{organization}/groups/{test_group}/users"
    )
    
    assert response.status_code == 200, f"Failed to get group members: {response.text}"


def test_add_group_user(client, organization, test_group, current_user_id):
    """Test adding a user to a group."""
    response = client.post(
        f"/organizations/{organization}/groups/{test_group}/users?userId={current_user_id}",
    )
    
    assert response.status_code == 200, f"Failed to add user to group: {response.text}"
    
    # Verify user was added
    members_response = client.get(
        f"/organizations/{organization}/groups/{test_group}/users"
    )
    assert members_response.status_code == 200
    member_ids = [m.get("id") or m.get("userId") for m in members_response.json()]
    assert current_user_id in member_ids


def test_remove_group_user(client, organization, test_group, current_user_id):
    """Test removing a user from a group."""
    # First add the user
    add_response = client.post(
        f"/organizations/{organization}/groups/{test_group}/users?userId={current_user_id}",
    )
    assert add_response.status_code == 200
    
    # Now remove the user
    remove_response = client.delete(
        f"/organizations/{organization}/groups/{test_group}/users/{current_user_id}"
    )
    
    assert remove_response.status_code == 200, f"Failed to remove user from group: {remove_response.text}"
    
    # Verify user was removed
    members_response = client.get(
        f"/organizations/{organization}/groups/{test_group}/users"
    )
    assert members_response.status_code == 200
    member_ids = [m.get("id") or m.get("userId") for m in members_response.json()]
    assert current_user_id not in member_ids