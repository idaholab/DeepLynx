"""Tests for AI Model Config API endpoints (organization-level)."""

import pytest

# ========================================================================
# SHARED PAYLOAD HELPERS
# ========================================================================

def make_config_payload(
    server_url="https://api.example.com",
    model_type="llm",
    model_provider="openai",
    model_name="gpt-4o",
    requires_token=True,
    default=False
):
    """Return a valid CreateAiModelConfigDto payload."""
    return {
        "server_url": server_url,
        "model_type": model_type,
        "model_provider": model_provider,
        "model_name": model_name,
        "requires_token": requires_token,
        "default": default,
    }
    

def assert_config_fields(result, payload):
    assert result["serverUrl"] == payload["server_url"]
    assert result["modelType"] == payload["model_type"]
    assert result["modelProvider"] == payload["model_provider"]
    assert result["modelName"] == payload["model_name"]
    assert result["requiresToken"] == payload["requires_token"]
    assert result["default"] == payload["default"]


# ========================================================================
# FIXTURES
# ========================================================================

@pytest.fixture
def cleanup_org_ai_model_configs(client, organization):
    """Track and cleanup organization-level AI Model Configurations."""
    created_ids = []
    yield created_ids
    for config_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/ai-model-configs/{config_id}")
        except:
            pass


@pytest.fixture
def test_ai_model_config(client, organization, cleanup_org_ai_model_configs):
    """Create a single AI Model Config for use in tests that need an existing config."""
    payload = make_config_payload(server_url="https://fixture.example.com", default=False)

    response = client.post(
        f"/organizations/{organization}/ai-model-configs",
        json=payload
    )

    assert response.status_code == 200, f"Failed to create AI Model Config fixture: {response.text}"
    config_id = response.json()["id"]
    cleanup_org_ai_model_configs.append(config_id)

    return config_id


# ========================================================================
# ORGANIZATION-LEVEL TESTS
# ========================================================================

def test_create_org_ai_model_config(client, organization, cleanup_org_ai_model_configs):
    """Test creating a single AI Model Config at the organization level."""
    payload = make_config_payload()

    response = client.post(
        f"/organizations/{organization}/ai-model-configs",
        json=payload
    )

    # Register for cleanup IMMEDIATELY, before assertions
    if response.status_code == 200:
        cleanup_org_ai_model_configs.append(response.json()["id"])

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 200, f"Request failed: {response.text}"
    result = response.json()
    assert "id" in result
    assert_config_fields(result, payload)


def test_create_org_ai_model_config_missing_required_fields(client, organization):
    """Test that creating an AI Model Config with missing required fields returns 400."""
    # Omit all required fields
    response = client.post(
        f"/organizations/{organization}/ai-model-configs",
        json={}
    )

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 400, f"Expected 400 but got: {response.status_code}"


def test_get_all_org_ai_model_configs(client, organization, cleanup_org_ai_model_configs):
    """Test retrieving all AI Model Configs at the organization level."""
    payload = make_config_payload()

    create_response = client.post(
        f"/organizations/{organization}/ai-model-configs",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_ai_model_configs.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create AI Model Config: {create_response.text}"
    created_id = create_response.json()["id"]

    get_response = client.get(
        f"/organizations/{organization}/ai-model-configs"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get AI Model Configs: {get_response.text}"

    all_configs = get_response.json()
    assert isinstance(all_configs, list), "Expected response to be a list"

    config_ids = [cfg["id"] for cfg in all_configs]
    assert created_id in config_ids, f"Created config {created_id} not found in list"

    our_config = next((cfg for cfg in all_configs if cfg["id"] == created_id), None)
    assert our_config is not None, f"Could not find config with id {created_id}"
    assert_config_fields(our_config, payload)


def test_get_all_org_ai_model_configs_hides_archived_by_default(client, organization, cleanup_org_ai_model_configs):
    """Test that archived AI Model Configs are hidden by default when fetching all."""
    payload = make_config_payload()

    create_response = client.post(
        f"/organizations/{organization}/ai-model-configs",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_ai_model_configs.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create AI Model Config: {create_response.text}"
    created_id = create_response.json()["id"]

    # Archive the config
    archive_response = client.patch(
        f"/organizations/{organization}/ai-model-configs/{created_id}/archive",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive config: {archive_response.text}"

    # Fetch all (default: hideArchived=true)
    get_response = client.get(f"/organizations/{organization}/ai-model-configs")
    assert get_response.status_code == 200, f"Failed to get configs: {get_response.text}"

    config_ids = [cfg["id"] for cfg in get_response.json()]
    assert created_id not in config_ids, "Archived config should not appear in default listing"


def test_get_all_org_ai_model_configs_show_archived(client, organization, cleanup_org_ai_model_configs):
    """Test that archived AI Model Configs are visible when hideArchived=false."""
    payload = make_config_payload()

    create_response = client.post(
        f"/organizations/{organization}/ai-model-configs",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_ai_model_configs.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create AI Model Config: {create_response.text}"
    created_id = create_response.json()["id"]

    # Archive it
    archive_response = client.patch(
        f"/organizations/{organization}/ai-model-configs/{created_id}/archive",
        params={"archive": "true"}
    )
    assert archive_response.status_code == 200, f"Failed to archive config: {archive_response.text}"

    # Fetch all with hideArchived=false
    get_response = client.get(
        f"/organizations/{organization}/ai-model-configs",
        params={"hideArchived": "false"}
    )
    assert get_response.status_code == 200, f"Failed to get configs: {get_response.text}"

    config_ids = [cfg["id"] for cfg in get_response.json()]
    assert created_id in config_ids, "Archived config should appear when hideArchived=false"


def test_get_org_ai_model_config_by_id(client, organization, cleanup_org_ai_model_configs):
    """Test retrieving a single AI Model Config by ID at the organization level."""
    payload = make_config_payload()

    create_response = client.post(
        f"/organizations/{organization}/ai-model-configs",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_ai_model_configs.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create AI Model Config: {create_response.text}"
    created_id = create_response.json()["id"]

    get_response = client.get(
        f"/organizations/{organization}/ai-model-configs/{created_id}"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 200, f"Failed to get AI Model Config: {get_response.text}"

    result = get_response.json()
    assert result["id"] == created_id
    assert_config_fields(result, payload)


def test_get_org_ai_model_config_not_found(client, organization):
    """Test that fetching a non-existent AI Model Config returns 404."""
    get_response = client.get(
        f"/organizations/{organization}/ai-model-configs/999999999"
    )

    print(f"\nStatus Code: {get_response.status_code}")
    print(f"Response Body: {get_response.text}")

    assert get_response.status_code == 404, f"Expected 404 but got: {get_response.status_code}"


def test_update_org_ai_model_config(client, organization, cleanup_org_ai_model_configs):
    """Test updating an AI Model Config at the organization level."""
    create_payload = make_config_payload()

    create_response = client.post(
        f"/organizations/{organization}/ai-model-configs",
        json=create_payload
    )

    if create_response.status_code == 200:
        cleanup_org_ai_model_configs.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create AI Model Config: {create_response.text}"
    created_id = create_response.json()["id"]

    update_payload = make_config_payload(
        server_url="https://updated.example.com",
        model_name="gpt-4o-mini",
        requires_token=False,
        default=True
    )

    update_response = client.put(
        f"/organizations/{organization}/ai-model-configs/{created_id}",
        json=update_payload
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 200, f"Failed to update AI Model Config: {update_response.text}"

    result = update_response.json()
    assert result["id"] == created_id
    assert_config_fields(result, update_payload)


def test_update_org_ai_model_config_not_found(client, organization):
    """Test that updating a non-existent AI Model Config returns 404."""
    update_response = client.put(
        f"/organizations/{organization}/ai-model-configs/999999999",
        json=make_config_payload()
    )

    print(f"\nStatus Code: {update_response.status_code}")
    print(f"Response Body: {update_response.text}")

    assert update_response.status_code == 404, f"Expected 404 but got: {update_response.status_code}"


def test_archive_and_unarchive_org_ai_model_config(client, organization, cleanup_org_ai_model_configs):
    """Test archiving and unarchiving an AI Model Config at the organization level."""
    payload = make_config_payload()

    create_response = client.post(
        f"/organizations/{organization}/ai-model-configs",
        json=payload
    )

    if create_response.status_code == 200:
        cleanup_org_ai_model_configs.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create AI Model Config: {create_response.text}"
    created_id = create_response.json()["id"]

    # Archive
    archive_response = client.patch(
        f"/organizations/{organization}/ai-model-configs/{created_id}/archive",
        params={"archive": "true"}
    )

    print(f"\nArchive Status Code: {archive_response.status_code}")
    print(f"Archive Response Body: {archive_response.text}")

    assert archive_response.status_code == 200, f"Failed to archive AI Model Config: {archive_response.text}"

    # Verify archived (requires hideArchived=false to retrieve)
    get_archived_response = client.get(
        f"/organizations/{organization}/ai-model-configs/{created_id}",
        params={"hideArchived": "false"}
    )
    assert get_archived_response.status_code == 200, f"Failed to get archived config: {get_archived_response.text}"
    assert get_archived_response.json().get("isArchived") == True, "Config should be archived"

    # Unarchive
    unarchive_response = client.patch(
        f"/organizations/{organization}/ai-model-configs/{created_id}/archive",
        params={"archive": "false"}
    )

    print(f"\nUnarchive Status Code: {unarchive_response.status_code}")
    print(f"Unarchive Response Body: {unarchive_response.text}")

    assert unarchive_response.status_code == 200, f"Failed to unarchive AI Model Config: {unarchive_response.text}"

    # Verify unarchived and visible in the default listing
    get_unarchived_response = client.get(
        f"/organizations/{organization}/ai-model-configs/{created_id}"
    )
    assert get_unarchived_response.status_code == 200, f"Failed to get unarchived config: {get_unarchived_response.text}"
    unarchived_result = get_unarchived_response.json()
    assert unarchived_result.get("isArchived") == False or "isArchived" not in unarchived_result, \
        "Config should not be archived"


def test_archive_org_ai_model_config_not_found(client, organization):
    """Test that archiving a non-existent AI Model Config returns 404."""
    archive_response = client.patch(
        f"/organizations/{organization}/ai-model-configs/999999999/archive",
        params={"archive": "true"}
    )

    print(f"\nStatus Code: {archive_response.status_code}")
    print(f"Response Body: {archive_response.text}")

    assert archive_response.status_code == 404, f"Expected 404 but got: {archive_response.status_code}"


def test_delete_org_ai_model_config(client, organization, cleanup_org_ai_model_configs):
    """Test permanently deleting an AI Model Config at the organization level."""
    payload = make_config_payload()

    create_response = client.post(
        f"/organizations/{organization}/ai-model-configs",
        json=payload
    )

    # Register for cleanup in case deletion fails
    if create_response.status_code == 200:
        cleanup_org_ai_model_configs.append(create_response.json()["id"])

    assert create_response.status_code == 200, f"Failed to create AI Model Config: {create_response.text}"
    created_id = create_response.json()["id"]

    delete_response = client.delete(
        f"/organizations/{organization}/ai-model-configs/{created_id}"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 200, f"Failed to delete AI Model Config: {delete_response.text}"

    # Confirm it no longer appears in the full listing
    all_configs_response = client.get(f"/organizations/{organization}/ai-model-configs")
    assert all_configs_response.status_code == 200, f"Failed to get all configs: {all_configs_response.text}"

    config_ids = [cfg["id"] for cfg in all_configs_response.json()]
    assert created_id not in config_ids, \
        f"Deleted config {created_id} should not appear in list of {len(config_ids)} configs"
    print(f"Confirmed: Config {created_id} not in list of {len(config_ids)} configs")


def test_delete_org_ai_model_config_not_found(client, organization):
    """Test that deleting a non-existent AI Model Config returns 404."""
    delete_response = client.delete(
        f"/organizations/{organization}/ai-model-configs/999999999"
    )

    print(f"\nStatus Code: {delete_response.status_code}")
    print(f"Response Body: {delete_response.text}")

    assert delete_response.status_code == 404, f"Expected 404 but got: {delete_response.status_code}"