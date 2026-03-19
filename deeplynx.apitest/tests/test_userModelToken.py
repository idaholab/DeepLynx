"""
Tests for the UserModelToken endpoints (happy path / CRUD only).

Endpoints covered:
  GET    /users/{userId}/model-tokens
  GET    /users/{userId}/model-tokens/{userModelTokenId}
  POST   /users/{userId}/model-tokens
  PUT    /users/{userId}/model-tokens/{userModelTokenId}
  DELETE /users/{userId}/model-tokens/{userModelTokenId}
"""
import pytest


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _url(user_id, token_id=None):
    base = f"/users/{user_id}/model-tokens"
    return f"{base}/{token_id}" if token_id else base


def assert_token_fields(result, user_id, ai_model_config_id, token_value):
    assert result["userId"] == user_id
    assert result["aiModelConfigId"] == ai_model_config_id
    assert result["token"] == token_value


# ---------------------------------------------------------------------------
# Session-scoped AI Model Config (created once, shared across all tests)
# ---------------------------------------------------------------------------

@pytest.fixture(scope="session")
def ai_model_config(client, organization):
    """Create a real AI Model Config to use as a foreign key in token tests."""
    payload = {
        "server_url": "https://api.example.com",
        "model_type": "llm",
        "model_provider": "openai",
        "model_name": "gpt-4o",
        "requires_token": True,
        "default": False,
    }
    response = client.post(f"/organizations/{organization}/ai-model-configs", json=payload)
    assert response.status_code == 200, f"Failed to create AI Model Config fixture: {response.text}"
    config_id = response.json()["id"]

    yield config_id

    try:
        client.delete(f"/organizations/{organization}/ai-model-configs/{config_id}")
    except Exception:
        pass


# ---------------------------------------------------------------------------
# Function-scoped cleanup + convenience fixtures
# ---------------------------------------------------------------------------

@pytest.fixture
def cleanup_model_tokens(client, current_user_id):
    """Track and delete User Model Tokens created during a test."""
    created_ids = []
    yield created_ids
    for token_id in created_ids:
        try:
            client.delete(_url(current_user_id, token_id))
        except Exception:
            pass


@pytest.fixture
def existing_token(client, current_user_id, ai_model_config, cleanup_model_tokens):
    """Create a single token that already exists when the test starts."""
    resp = client.post(
        _url(current_user_id),
        json={"aiModelConfigId": ai_model_config, "token": "fixture-token-value"},
    )
    assert resp.status_code == 200, f"Failed to create token fixture: {resp.text}"
    body = resp.json()
    cleanup_model_tokens.append(body["id"])
    return body


# ---------------------------------------------------------------------------
# GET /users/{userId}/model-tokens
# ---------------------------------------------------------------------------

def test_get_all_user_model_tokens_returns_200(client, current_user_id):
    response = client.get(_url(current_user_id))
    assert response.status_code == 200
    assert isinstance(response.json(), list)


def test_get_all_user_model_tokens_includes_created_token(client, current_user_id, existing_token):
    response = client.get(_url(current_user_id))
    assert response.status_code == 200
    ids = [t["id"] for t in response.json()]
    assert existing_token["id"] in ids


def test_get_all_user_model_tokens_filter_by_ai_model_config_id(client, current_user_id, ai_model_config, existing_token):
    response = client.get(_url(current_user_id), params={"aiModelConfigId": ai_model_config})
    assert response.status_code == 200
    results = response.json()
    assert len(results) > 0
    assert all(t["aiModelConfigId"] == ai_model_config for t in results)


def test_get_all_user_model_tokens_filter_nonexistent_config_returns_empty(client, current_user_id):
    response = client.get(_url(current_user_id), params={"aiModelConfigId": 999999999})
    assert response.status_code == 200
    assert response.json() == []


# ---------------------------------------------------------------------------
# GET /users/{userId}/model-tokens/{userModelTokenId}
# ---------------------------------------------------------------------------

def test_get_user_model_token_by_id(client, current_user_id, existing_token):
    response = client.get(_url(current_user_id, existing_token["id"]))
    assert response.status_code == 200
    result = response.json()
    assert result["id"] == existing_token["id"]
    assert result["userId"] == current_user_id


def test_get_user_model_token_by_id_contains_expected_fields(client, current_user_id, existing_token):
    response = client.get(_url(current_user_id, existing_token["id"]))
    assert response.status_code == 200
    body = response.json()
    for field in ("id", "userId", "aiModelConfigId", "token", "lastUpdatedAt"):
        assert field in body, f"Missing expected field: {field}"


def test_get_user_model_token_by_id_not_found(client, current_user_id):
    response = client.get(_url(current_user_id, 999999999))
    assert response.status_code == 404


# ---------------------------------------------------------------------------
# POST /users/{userId}/model-tokens
# ---------------------------------------------------------------------------

def test_create_user_model_token_returns_200(client, current_user_id, ai_model_config, cleanup_model_tokens):
    response = client.post(
        _url(current_user_id),
        json={"aiModelConfigId": ai_model_config, "token": "new-token-value"},
    )
    if response.status_code == 200:
        cleanup_model_tokens.append(response.json()["id"])
    assert response.status_code == 200
    assert "id" in response.json()


def test_create_user_model_token_reflects_submitted_values(client, current_user_id, ai_model_config, cleanup_model_tokens):
    token_value = "my-secret-api-key"
    response = client.post(
        _url(current_user_id),
        json={"aiModelConfigId": ai_model_config, "token": token_value},
    )
    if response.status_code == 200:
        cleanup_model_tokens.append(response.json()["id"])
    assert response.status_code == 200
    assert_token_fields(response.json(), current_user_id, ai_model_config, token_value)


def test_create_user_model_token_appears_in_list(client, current_user_id, ai_model_config, cleanup_model_tokens):
    response = client.post(
        _url(current_user_id),
        json={"aiModelConfigId": ai_model_config, "token": "list-check-token"},
    )
    if response.status_code == 200:
        cleanup_model_tokens.append(response.json()["id"])
    assert response.status_code == 200
    new_id = response.json()["id"]

    list_response = client.get(_url(current_user_id))
    assert list_response.status_code == 200
    assert new_id in [t["id"] for t in list_response.json()]


# ---------------------------------------------------------------------------
# PUT /users/{userId}/model-tokens/{userModelTokenId}
# ---------------------------------------------------------------------------

def test_update_user_model_token_value(client, current_user_id, existing_token):
    new_value = "updated-token-value"
    response = client.put(
        _url(current_user_id, existing_token["id"]),
        json={"token": new_value},
    )
    assert response.status_code == 200
    assert response.json()["token"] == new_value


def test_update_user_model_token_returns_correct_id_and_user(client, current_user_id, existing_token):
    response = client.put(
        _url(current_user_id, existing_token["id"]),
        json={"token": "another-updated-value"},
    )
    assert response.status_code == 200
    result = response.json()
    assert result["id"] == existing_token["id"]
    assert result["userId"] == current_user_id


def test_update_user_model_token_not_found(client, current_user_id):
    response = client.put(
        _url(current_user_id, 999999999),
        json={"token": "irrelevant"},
    )
    assert response.status_code == 404


# ---------------------------------------------------------------------------
# DELETE /users/{userId}/model-tokens/{userModelTokenId}
# ---------------------------------------------------------------------------

def test_delete_user_model_token_returns_200(client, current_user_id, ai_model_config, cleanup_model_tokens):
    create_resp = client.post(
        _url(current_user_id),
        json={"aiModelConfigId": ai_model_config, "token": "to-be-deleted"},
    )
    assert create_resp.status_code == 200
    token_id = create_resp.json()["id"]
    cleanup_model_tokens.append(token_id)  # fallback if delete fails

    delete_resp = client.delete(_url(current_user_id, token_id))
    assert delete_resp.status_code == 200


def test_delete_user_model_token_no_longer_retrievable(client, current_user_id, ai_model_config):
    create_resp = client.post(
        _url(current_user_id),
        json={"aiModelConfigId": ai_model_config, "token": "delete-then-get"},
    )
    assert create_resp.status_code == 200
    token_id = create_resp.json()["id"]

    client.delete(_url(current_user_id, token_id))

    get_resp = client.get(_url(current_user_id, token_id))
    assert get_resp.status_code == 404


def test_delete_user_model_token_not_found(client, current_user_id):
    response = client.delete(_url(current_user_id, 999999999))
    assert response.status_code == 404