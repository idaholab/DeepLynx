def test_get_all_users(client, current_user_id):
    response = client.get("/users")

    assert response.status_code == 200
    results = response.json()
    user_ids = []
    for user in results:
        user_ids.append(user["id"])
    assert current_user_id in user_ids  

def test_create_user(client, cleanup_created_users):
    user_payload = {
        "name": "pytest user",
        "email": "testpytestuser@inl.gov",
        "username": "testuser",
        "isSysAdmin": "false"
    }
    create_response = client.post("/users",
        json=user_payload)
    
    if create_response.status_code == 200:
        new_user_id = create_response.json()["id"]
        cleanup_created_users.append(new_user_id)

    assert create_response.status_code == 200

    response = client.get("/users")

    assert response.status_code == 200
    results = response.json()
    user_ids = []
    for user in results:
        user_ids.append(user["id"])
    assert new_user_id in user_ids  

def test_get_user(client, current_user_id):
    response = client.get(f"/users/{current_user_id}")

    assert response.status_code == 200
    result = response.json()
    assert result["id"] == current_user_id

def test_update_user(client, cleanup_created_users):
    user_payload = {
        "name": "pytest user",
        "email": "testpytestuser@inl.gov",
        "username": "testuser",
        "isSysAdmin": "false"
    }
    create_response = client.post("/users",
        json=user_payload)
    
    if create_response.status_code == 200:
        new_user_id = create_response.json()["id"]
        cleanup_created_users.append(new_user_id)

    assert create_response.status_code == 200

    updated_payload = {
        "name": "pytest updated user",
        "username": "pytest user"
    }

    updated_response = client.put(f"/users/{new_user_id}",
                          json=updated_payload)
    
    assert updated_response.status_code == 200
    updated_result = updated_response.json()
    assert updated_result["name"] == "pytest updated user"

def test_delete_user(client, cleanup_created_users):
    user_payload = {
        "name": "pytest user",
        "email": "testpytestuser@inl.gov",
        "username": "testuser",
        "isSysAdmin": "false"
    }
    create_response = client.post("/users",
        json=user_payload)
    
    if create_response.status_code == 200:
        new_user_id = create_response.json()["id"]
        cleanup_created_users.append(new_user_id)

    assert create_response.status_code == 200

    delete_response = client.delete(f"/users/{new_user_id}")

    assert delete_response.status_code == 200
    get_response = client.get(f"/users/{new_user_id}")

    assert get_response.status_code == 500

def test_archive_and_unarchive_user(client, cleanup_created_users):
    user_payload = {
        "name": "pytest user",
        "email": "testpytestuser@inl.gov",
        "username": "testuser",
        "isSysAdmin": "false"
    }
    create_response = client.post("/users",
        json=user_payload)
    
    if create_response.status_code == 200:
        new_user_id = create_response.json()["id"]
        cleanup_created_users.append(new_user_id)

    assert create_response.status_code == 200

    archive_response = client.patch(f"/users/{new_user_id}?archive=true")

    assert archive_response.status_code == 200

    get_response = client.get(f"/users/{new_user_id}")

    assert get_response.status_code == 500

    unarchive_response = client.patch(f"/users/{new_user_id}?archive=false")

    assert unarchive_response.status_code == 200

    new_get_response = client.get(f"/users/{new_user_id}")

    assert new_get_response.status_code == 200

def test_grant_system_admin_rights(client, cleanup_created_users):
    user_payload = {
        "name": "pytest user",
        "email": "testpytestuser@inl.gov",
        "username": "testuser",
        "isSysAdmin": "false"
    }
    create_response = client.post("/users",
        json=user_payload)
    
    if create_response.status_code == 200:
        new_user_id = create_response.json()["id"]
        cleanup_created_users.append(new_user_id)

    assert create_response.status_code == 200

    admin_response = client.patch(f"/users/{new_user_id}/admin")

    assert admin_response.status_code == 200

    get_response = client.get(f"/users/{new_user_id}")

    assert get_response.status_code == 200
    get_result = get_response.json()

    assert get_result["isSysAdmin"] == True

def test_get_data_overview_for_user(client, current_user_id):
    overview_response = client.get(f"/users/{current_user_id}/overview")

    assert overview_response.status_code == 200

    overview_result = overview_response.json()
    assert overview_result["projects"] != None
    assert overview_result["records"] != None
    assert overview_result["tags"] != None
    assert overview_result["connections"] != None

def test_get_current_user(client, current_user_id):
    current_user_response = client.get(f"/users/current")

    assert current_user_response.status_code == 200
    current_user_result = current_user_response.json()
    assert current_user_result["id"] == current_user_id