import pytest
import uuid

def test_create_project(client, organization, cleanup_created_projects):
    unique_name = f"project create test {uuid.uuid4()}"
    response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "TP"}
    )

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 200
    result = response.json()
    cleanup_created_projects.append(result["id"])
    assert result["name"] == unique_name
    assert result["abbreviation"] == "TP"


def test_get_all_projects(client, organization, project):
    response = client.get(f"/organizations/{organization}/projects")

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 200
    results = response.json()
    all_ids = [proj["id"] for proj in results]
    assert project in all_ids, "project not found"

def test_get_all_projects_hide_archived(client, organization, project):
    response = client.get(f"/organizations/{organization}/projects?hideArchived=true")

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 200
    results = response.json()
    all_ids = [proj["id"] for proj in results]
    assert project in all_ids, "project not found"

def test_get_project_by_id(client, organization, project):
    response = client.get(f"/organizations/{organization}/projects/{project}")
    assert response.status_code == 200

    result = response.json()
    assert result["id"] == project

def test_get_project_stats(client, organization, project):
    response = client.get(f"/organizations/{organization}/projects/{project}/stats")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200
    result = response.json()
    # Stats endpoint should return some statistical data
    assert isinstance(result, dict)

def test_update_project(client, organization, cleanup_created_projects):
    unique_name = f"project update test {uuid.uuid4()}"
    # Create project first
    create_response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "PU"}
    )

    assert create_response.status_code == 200, f"Failed to create project: {create_response.text}"
    
    create_result = create_response.json()
    created_project_id = create_result["id"]
    cleanup_created_projects.append(created_project_id)

    # Update the project
    updated_name = f"updated project name {uuid.uuid4()}"
    update_response = client.put(
        f"/organizations/{organization}/projects/{created_project_id}",
        json={
            "name": updated_name,
            "description": "updated description",
            "abbreviation": "UPD"
        }
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200
    result = update_response.json()
    assert result["name"] == updated_name
    assert result["description"] == "updated description"
    assert result["abbreviation"] == "UPD"

def test_delete_project(client, organization, cleanup_created_projects):
    unique_name = f"project delete test {uuid.uuid4()}"
    # Create project first
    create_response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "PD"}
    )

    assert create_response.status_code == 200, f"Failed to create project: {create_response.text}"
    
    create_result = create_response.json()
    created_project_id = create_result["id"]

    # Delete the project
    delete_response = client.delete(f"/organizations/{organization}/projects/{created_project_id}")

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200
    
    # Verify response contains deletion message
    if delete_response.status_code == 200 and delete_response.text:
        result = delete_response.json()
        assert "message" in result or result == {}, (
            f"Unexpected response format: {delete_response.text}"
        )

    # Verify it's deleted/archived (may return 500 or 404 depending on soft vs hard delete)
    get_response = client.get(f"/organizations/{organization}/projects/{created_project_id}")
    assert get_response.status_code in [404, 500], (
        f"Expected 404 or 500 for deleted project, got {get_response.status_code}: {get_response.text}"
    )

def test_patch_project_archive(client, organization, cleanup_created_projects):
    unique_name = f"project patch test {uuid.uuid4()}"
    # Create project first
    create_response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "PA"}
    )

    assert create_response.status_code == 200, f"Failed to create project: {create_response.text}"
    
    create_result = create_response.json()
    created_project_id = create_result["id"]
    cleanup_created_projects.append(created_project_id)

    # PATCH appears to be used for archiving/unarchiving
    # First archive the project
    patch_archive_response = client.patch(
        f"/organizations/{organization}/projects/{created_project_id}?archive=true"
    )

    print(f"\nArchive Status Code: {patch_archive_response.status_code}")
    print(f"Archive Response Body: {patch_archive_response.text}")

    assert patch_archive_response.status_code == 200, f"PATCH archive failed: {patch_archive_response.text}"

    # Then unarchive it
    patch_unarchive_response = client.patch(
        f"/organizations/{organization}/projects/{created_project_id}?archive=false"
    )

    print(f"\nUnarchive Status Code: {patch_unarchive_response.status_code}")
    print(f"Unarchive Response Body: {patch_unarchive_response.text}")

    assert patch_unarchive_response.status_code == 200, f"PATCH unarchive failed: {patch_unarchive_response.text}"

# ========== PROJECT MEMBER TESTS ==========

def test_get_project_members(client, organization, project):
    response = client.get(f"/organizations/{organization}/projects/{project}/members")
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200
    results = response.json()
    assert isinstance(results, list)

def test_add_user_to_project_with_role(client, organization, cleanup_created_projects, cleanup_project_members, cleanup_project_roles, current_user_id):
    unique_name = f"project member test {uuid.uuid4()}"
    # Create project
    create_response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "PM"}
    )

    assert create_response.status_code == 200, f"Failed to create project: {create_response.text}"
    
    create_result = create_response.json()
    created_project_id = create_result["id"]
    cleanup_created_projects.append(created_project_id)

    # Create a role for the project
    role_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/roles",
        json={"name": "Test Role", "description": "Test role for member"}
    )

    assert role_response.status_code == 200, f"Failed to create role: {role_response.text}"
    role_result = role_response.json()
    role_id = role_result["id"]
    cleanup_project_roles.append((created_project_id, role_id))

    # Add user to project with role
    add_user_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/members?userId={current_user_id}&roleId={role_id}"
    )

    print(f"\nStatus Code: {add_user_response.status_code}")
    print(f"Response Body: {add_user_response.text}")

    if add_user_response.status_code == 200:
        cleanup_project_members.append((created_project_id, current_user_id, None))

    assert add_user_response.status_code == 200, (
        f"Add user to project failed with status {add_user_response.status_code}. "
        f"Response: {add_user_response.text}"
    )

def test_add_user_to_project_without_role(client, organization, cleanup_created_projects, cleanup_project_members, current_user_id):
    unique_name = f"project member no role test {uuid.uuid4()}"
    # Create project
    create_response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "PN"}
    )

    assert create_response.status_code == 200, f"Failed to create project: {create_response.text}"
    
    create_result = create_response.json()
    created_project_id = create_result["id"]
    cleanup_created_projects.append(created_project_id)

    # Add user to project without role
    add_user_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/members?userId={current_user_id}"
    )

    print(f"\nStatus Code: {add_user_response.status_code}")
    print(f"Response Body: {add_user_response.text}")

    if add_user_response.status_code == 200:
        cleanup_project_members.append((created_project_id, current_user_id, None))

    assert add_user_response.status_code == 200, (
        f"Add user to project failed with status {add_user_response.status_code}. "
        f"Response: {add_user_response.text}"
    )

def test_update_user_role_in_project(client, organization, cleanup_created_projects, cleanup_project_members, cleanup_project_roles, current_user_id):
    unique_name = f"project update role test {uuid.uuid4()}"
    # Create project
    create_response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "PR"}
    )

    assert create_response.status_code == 200, f"Failed to create project: {create_response.text}"
    
    create_result = create_response.json()
    created_project_id = create_result["id"]
    cleanup_created_projects.append(created_project_id)

    # Create first role
    role_response_1 = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/roles",
        json={"name": "Test Role 1", "description": "First test role"}
    )
    assert role_response_1.status_code == 200
    role_id_1 = role_response_1.json()["id"]
    cleanup_project_roles.append((created_project_id, role_id_1))

    # Create second role
    role_response_2 = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/roles",
        json={"name": "Test Role 2", "description": "Second test role"}
    )
    assert role_response_2.status_code == 200
    role_id_2 = role_response_2.json()["id"]
    cleanup_project_roles.append((created_project_id, role_id_2))

    # Add user with first role
    add_user_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/members?userId={current_user_id}&roleId={role_id_1}"
    )
    assert add_user_response.status_code == 200
    cleanup_project_members.append((created_project_id, current_user_id, None))

    # Update user to second role
    update_role_response = client.put(
        f"/organizations/{organization}/projects/{created_project_id}/members?userId={current_user_id}&roleId={role_id_2}"
    )

    print(f"\nStatus Code: {update_role_response.status_code}")
    print(f"Response Body: {update_role_response.text}")

    assert update_role_response.status_code == 200, (
        f"Update user role failed with status {update_role_response.status_code}. "
        f"Response: {update_role_response.text}"
    )

def test_remove_user_from_project(client, organization, cleanup_created_projects, cleanup_project_roles, current_user_id):
    unique_name = f"project remove user test {uuid.uuid4()}"
    # Create project
    create_response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "RU"}
    )

    assert create_response.status_code == 200, f"Failed to create project: {create_response.text}"
    
    create_result = create_response.json()
    created_project_id = create_result["id"]
    cleanup_created_projects.append(created_project_id)

    # Create a role
    role_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/roles",
        json={"name": "Test Role", "description": "Test role"}
    )
    assert role_response.status_code == 200
    role_id = role_response.json()["id"]
    cleanup_project_roles.append((created_project_id, role_id))

    # Add user to project
    add_user_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/members?userId={current_user_id}&roleId={role_id}"
    )
    assert add_user_response.status_code == 200

    # Remove user from project
    remove_user_response = client.delete(
        f"/organizations/{organization}/projects/{created_project_id}/members?userId={current_user_id}"
    )

    print(f"\nStatus Code: {remove_user_response.status_code}")
    print(f"Response Body: {remove_user_response.text}")

    assert remove_user_response.status_code == 200

def test_add_group_to_project(client, organization, cleanup_created_projects, cleanup_project_members, cleanup_project_roles, cleanup_created_groups):
    unique_name = f"project group test {uuid.uuid4()}"
    # Create project
    create_response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "PG"}
    )

    assert create_response.status_code == 200, f"Failed to create project: {create_response.text}"
    
    create_result = create_response.json()
    created_project_id = create_result["id"]
    cleanup_created_projects.append(created_project_id)

    # Create a group
    group_response = client.post(
        f"/organizations/{organization}/groups",
        json={"name": f"Test Group {uuid.uuid4()}", "description": "Test group"}
    )
    assert group_response.status_code == 200, f"Failed to create group: {group_response.text}"
    group_result = group_response.json()
    group_id = group_result["id"]
    cleanup_created_groups.append(group_id)

    # Create a role
    role_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/roles",
        json={"name": "Test Role", "description": "Test role"}
    )
    assert role_response.status_code == 200
    role_id = role_response.json()["id"]
    cleanup_project_roles.append((created_project_id, role_id))

    # Add group to project
    add_group_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/members?groupId={group_id}&roleId={role_id}"
    )

    print(f"\nStatus Code: {add_group_response.status_code}")
    print(f"Response Body: {add_group_response.text}")

    if add_group_response.status_code == 200:
        cleanup_project_members.append((created_project_id, None, group_id))

    assert add_group_response.status_code == 200, (
        f"Add group to project failed with status {add_group_response.status_code}. "
        f"Response: {add_group_response.text}"
    )

def test_update_group_role_in_project(client, organization, cleanup_created_projects, cleanup_project_members, cleanup_project_roles, cleanup_created_groups):
    unique_name = f"project update group role test {uuid.uuid4()}"
    # Create project
    create_response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "UG"}
    )

    assert create_response.status_code == 200, f"Failed to create project: {create_response.text}"
    
    create_result = create_response.json()
    created_project_id = create_result["id"]
    cleanup_created_projects.append(created_project_id)

    # Create a group
    group_response = client.post(
        f"/organizations/{organization}/groups",
        json={"name": f"Test Group {uuid.uuid4()}", "description": "Test group"}
    )
    assert group_response.status_code == 200
    group_id = group_response.json()["id"]
    cleanup_created_groups.append(group_id)

    # Create two roles
    role_response_1 = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/roles",
        json={"name": "Test Role 1", "description": "First test role"}
    )
    assert role_response_1.status_code == 200
    role_id_1 = role_response_1.json()["id"]
    cleanup_project_roles.append((created_project_id, role_id_1))

    role_response_2 = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/roles",
        json={"name": "Test Role 2", "description": "Second test role"}
    )
    assert role_response_2.status_code == 200
    role_id_2 = role_response_2.json()["id"]
    cleanup_project_roles.append((created_project_id, role_id_2))

    # Add group with first role
    add_group_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/members?groupId={group_id}&roleId={role_id_1}"
    )
    assert add_group_response.status_code == 200
    cleanup_project_members.append((created_project_id, None, group_id))

    # Update group to second role
    update_role_response = client.put(
        f"/organizations/{organization}/projects/{created_project_id}/members?groupId={group_id}&roleId={role_id_2}"
    )

    print(f"\nStatus Code: {update_role_response.status_code}")
    print(f"Response Body: {update_role_response.text}")

    assert update_role_response.status_code == 200, (
        f"Update group role failed with status {update_role_response.status_code}. "
        f"Response: {update_role_response.text}"
    )

def test_remove_group_from_project(client, organization, cleanup_created_projects, cleanup_project_roles, cleanup_created_groups):
    unique_name = f"project remove group test {uuid.uuid4()}"
    # Create project
    create_response = client.post(
        f"/organizations/{organization}/projects",
        json={"name": unique_name, "description": "Test project", "abbreviation": "RG"}
    )

    assert create_response.status_code == 200, f"Failed to create project: {create_response.text}"
    
    create_result = create_response.json()
    created_project_id = create_result["id"]
    cleanup_created_projects.append(created_project_id)

    # Create a group
    group_response = client.post(
        f"/organizations/{organization}/groups",
        json={"name": f"Test Group {uuid.uuid4()}", "description": "Test group"}
    )
    assert group_response.status_code == 200
    group_id = group_response.json()["id"]
    cleanup_created_groups.append(group_id)

    # Create a role
    role_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/roles",
        json={"name": "Test Role", "description": "Test role"}
    )
    assert role_response.status_code == 200
    role_id = role_response.json()["id"]
    cleanup_project_roles.append((created_project_id, role_id))

    # Add group to project
    add_group_response = client.post(
        f"/organizations/{organization}/projects/{created_project_id}/members?groupId={group_id}&roleId={role_id}"
    )
    assert add_group_response.status_code == 200

    # Remove group from project
    remove_group_response = client.delete(
        f"/organizations/{organization}/projects/{created_project_id}/members?groupId={group_id}"
    )

    print(f"\nStatus Code: {remove_group_response.status_code}")
    print(f"Response Body: {remove_group_response.text}")

    assert remove_group_response.status_code == 200