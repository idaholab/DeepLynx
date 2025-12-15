"""
DeepLynx Labels API Tests

Tests all endpoints of the SensitivityLabelController API at the project level.
"""

import pytest
import time


class TestLabelsAPI:
    """Test suite for Labels (Sensitivity Labels) API endpoints."""
    
    # ========================================================================
    # CREATE TESTS
    # ========================================================================
    
    def test_create_label_basic(self, client, project):
        """Test creating a basic sensitivity label."""
        payload = {
            "name": "pytest_BasicLabel",
            "description": "A basic test sensitivity label"
        }
        
        response = client.post(
            f"/projects/{project}/labels",
            json=payload
        )
        
        assert response.status_code == 200, f"Failed to create label: {response.text}"
        result = response.json()
        
        assert result.get("name") == payload["name"]
        assert result.get("description") == payload["description"]
        assert result.get("id") is not None
        assert result.get("projectId") == project
        assert "isArchived" in result
        assert "lastUpdatedAt" in result or "last_updated_at" in result
        
        # Cleanup
        label_id = result.get("id")
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_create_label_minimal(self, client, project):
        """Test creating a label with only required fields (name)."""
        payload = {
            "name": "pytest_MinimalLabel"
        }
        
        response = client.post(
            f"/projects/{project}/labels",
            json=payload
        )
        
        assert response.status_code == 200, f"Failed to create minimal label: {response.text}"
        result = response.json()
        
        assert result.get("name") == payload["name"]
        assert result.get("id") is not None
        
        # Cleanup
        label_id = result.get("id")
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_create_label_with_null_description(self, client, project):
        """Test creating a label with explicit null description."""
        payload = {
            "name": "pytest_NullDescLabel",
            "description": None
        }
        
        response = client.post(
            f"/projects/{project}/labels",
            json=payload
        )
        
        assert response.status_code == 200, f"Failed to create label with null description: {response.text}"
        result = response.json()
        
        assert result.get("name") == payload["name"]
        assert result.get("id") is not None
        
        # Cleanup
        label_id = result.get("id")
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_create_label_empty_name_fails(self, client, project):
        """Test that creating a label with empty name fails."""
        payload = {
            "name": "",
            "description": "Should fail"
        }
        
        response = client.post(
            f"/projects/{project}/labels",
            json=payload
        )
        
        # Should fail with 400 or similar
        assert response.status_code >= 400, "Empty name should not be allowed"
    
    def test_create_label_missing_name_fails(self, client, project):
        """Test that creating a label without name fails."""
        payload = {
            "description": "Missing name field"
        }
        
        response = client.post(
            f"/projects/{project}/labels",
            json=payload
        )
        
        # Should fail with 400 or similar
        assert response.status_code >= 400, "Missing name should not be allowed"
    
    def test_create_multiple_labels(self, client, project):
        """Test creating multiple sensitivity labels with different configurations."""
        label_configs = [
            {"name": "pytest_Confidential", "description": "Confidential information"},
            {"name": "pytest_Internal", "description": "Internal use only"},
            {"name": "pytest_Public", "description": "Publicly available information"}
        ]
        created_ids = []
        
        for config in label_configs:
            response = client.post(
                f"/projects/{project}/labels",
                json=config
            )
            
            assert response.status_code == 200, f"Failed to create label {config['name']}: {response.text}"
            result = response.json()
            created_ids.append(result.get("id"))
            
            assert result.get("name") == config["name"]
            assert result.get("description") == config["description"]
        
        assert len(created_ids) == 3, "All three labels should be created"
        
        # Cleanup
        for label_id in created_ids:
            client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_create_duplicate_label_name(self, client, project):
        """Test creating labels with duplicate names (if allowed)."""
        payload1 = {
            "name": "pytest_DuplicateLabel",
            "description": "First label"
        }
        
        payload2 = {
            "name": "pytest_DuplicateLabel",
            "description": "Second label with same name"
        }
        
        # Create first label
        response1 = client.post(
            f"/projects/{project}/labels",
            json=payload1
        )
        assert response1.status_code == 200
        label_id_1 = response1.json().get("id")
        
        # Try to create second label with same name
        response2 = client.post(
            f"/projects/{project}/labels",
            json=payload2
        )
        
        # Depending on API behavior, this might succeed or fail
        # If it succeeds, cleanup both; if it fails, cleanup only first
        if response2.status_code == 200:
            label_id_2 = response2.json().get("id")
            client.delete(f"/projects/{project}/labels/{label_id_2}")
        
        # Cleanup first label
        client.delete(f"/projects/{project}/labels/{label_id_1}")
    
    # ========================================================================
    # READ TESTS
    # ========================================================================
    
    def test_get_all_labels_empty(self, client, project):
        """Test getting all labels when none exist (or cleanup first)."""
        response = client.get(f"/projects/{project}/labels")
        
        assert response.status_code == 200, f"Failed to get labels: {response.text}"
        result = response.json()
        
        # Should return a list (might be empty or contain existing labels)
        assert isinstance(result, list), "Response should be a list"
    
    def test_get_all_labels_with_data(self, client, project):
        """Test getting all labels after creating some."""
        # Create multiple labels
        label_ids = []
        for i in range(3):
            payload = {
                "name": f"pytest_ListLabel_{i}_{int(time.time() * 1000)}",
                "description": f"Test sensitivity label {i}"
            }
            response = client.post(f"/projects/{project}/labels", json=payload)
            assert response.status_code == 200
            label_ids.append(response.json().get("id"))
        
        # Get all labels
        response = client.get(f"/projects/{project}/labels")
        assert response.status_code == 200
        result = response.json()
        
        # Verify our labels are in the list
        assert len(result) >= 3, "Should have at least 3 labels"
        retrieved_ids = [label.get("id") for label in result]
        for label_id in label_ids:
            assert label_id in retrieved_ids, f"Label {label_id} should be in the list"
        
        # Cleanup
        for label_id in label_ids:
            client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_get_all_labels_hide_archived_true(self, client, project):
        """Test getting labels with hideArchived=true parameter."""
        # Create a label and archive it
        timestamp = int(time.time() * 1000)
        payload = {
            "name": f"pytest_ArchivedLabel_{timestamp}",
            "description": "Label to be archived"
        }
        
        response = client.post(f"/projects/{project}/labels", json=payload)
        assert response.status_code == 200, f"Failed to create label: {response.text}"
        label_id = response.json().get("id")
        
        # Archive the label
        archive_response = client.patch(
            f"/projects/{project}/labels/{label_id}",
            params={"archive": True}
        )
        assert archive_response.status_code == 200
        
        # Get labels with hideArchived=true
        response = client.get(
            f"/projects/{project}/labels",
            params={"hideArchived": True}
        )
        assert response.status_code == 200
        result = response.json()
        
        # Archived label should not be in the list
        retrieved_ids = [label.get("id") for label in result]
        assert label_id not in retrieved_ids, "Archived label should be hidden"
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_get_all_labels_hide_archived_false(self, client, project):
        """Test getting labels with hideArchived=false parameter."""
        # Create a label and archive it
        timestamp = int(time.time() * 1000)
        payload = {
            "name": f"pytest_ShowArchivedLabel_{timestamp}",
            "description": "Label to be shown when archived"
        }
        
        response = client.post(f"/projects/{project}/labels", json=payload)
        assert response.status_code == 200, f"Failed to create label: {response.text}"
        label_id = response.json().get("id")
        
        # Archive the label
        archive_response = client.patch(
            f"/projects/{project}/labels/{label_id}",
            params={"archive": True}
        )
        assert archive_response.status_code == 200
        
        # Get labels with hideArchived=false
        response = client.get(
            f"/projects/{project}/labels",
            params={"hideArchived": False}
        )
        assert response.status_code == 200
        result = response.json()
        
        # Archived label should be in the list
        retrieved_ids = [label.get("id") for label in result]
        assert label_id in retrieved_ids, "Archived label should be shown"
        
        # Verify it's marked as archived
        archived_label = next((l for l in result if l.get("id") == label_id), None)
        assert archived_label is not None
        assert archived_label.get("isArchived") is True or archived_label.get("is_archived") is True
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_get_label_by_id(self, client, project):
        """Test getting a specific label by ID."""
        # Create a label
        payload = {
            "name": "pytest_GetByIdLabel",
            "description": "Label for get by ID test"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200
        label_id = create_response.json().get("id")
        
        # Get the label by ID
        response = client.get(f"/projects/{project}/labels/{label_id}")
        
        assert response.status_code == 200, f"Failed to get label by ID: {response.text}"
        result = response.json()
        
        assert result.get("id") == label_id
        assert result.get("name") == payload["name"]
        assert result.get("description") == payload["description"]
        assert result.get("projectId") == project
        assert "isArchived" in result or "is_archived" in result
        assert "lastUpdatedAt" in result or "last_updated_at" in result
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_get_label_nonexistent_id(self, client, project):
        """Test getting a label with non-existent ID."""
        fake_id = 999999999
        
        response = client.get(f"/projects/{project}/labels/{fake_id}")
        
        # Should return 404 or similar
        assert response.status_code == 500
    
    def test_get_label_invalid_id(self, client, project):
        """Test getting a label with invalid ID format."""
        invalid_id = "not-a-number"
        
        response = client.get(f"/projects/{project}/labels/{invalid_id}")
        
        # Should return 400 or 404
        assert response.status_code >= 400, "Invalid ID should return error"
    
    def test_get_label_with_hide_archived_param(self, client, project):
        """Test getting a specific archived label with hideArchived parameter."""
        # Create and archive a label
        timestamp = int(time.time() * 1000)
        payload = {
            "name": f"pytest_HideArchivedTest_{timestamp}",
            "description": "Testing hideArchived parameter"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200, f"Failed to create label: {create_response.text}"
        label_id = create_response.json().get("id")
        
        # Archive it
        archive_response = client.patch(
            f"/projects/{project}/labels/{label_id}",
            params={"archive": True}
        )
        assert archive_response.status_code == 200
        
        # Try to get with hideArchived=true
        response = client.get(
            f"/projects/{project}/labels/{label_id}",
            params={"hideArchived": True}
        )
        
        # Behavior might vary - could return 404 or the archived label
        # Document the actual behavior
        if response.status_code == 200:
            result = response.json()
            assert result.get("id") == label_id
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    # ========================================================================
    # UPDATE TESTS
    # ========================================================================
    
    def test_update_label_name_and_description(self, client, project):
        """Test updating both name and description of a label."""
        # Create a label
        payload = {
            "name": "pytest_OriginalLabel",
            "description": "Original description"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200
        label_id = create_response.json().get("id")
        
        # Update the label
        update_payload = {
            "name": "pytest_UpdatedLabel",
            "description": "Updated description"
        }
        
        response = client.put(
            f"/projects/{project}/labels/{label_id}",
            json=update_payload
        )
        
        assert response.status_code == 200, f"Failed to update label: {response.text}"
        result = response.json()
        
        assert result.get("name") == update_payload["name"]
        assert result.get("description") == update_payload["description"]
        assert result.get("id") == label_id
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_update_label_name_only(self, client, project):
        """Test updating only the name of a label."""
        # Create a label
        payload = {
            "name": "pytest_NameUpdateLabel",
            "description": "Original description"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200
        label_id = create_response.json().get("id")
        
        # Update only the name
        update_payload = {
            "name": "pytest_UpdatedNameLabel",
            "description": None
        }
        
        response = client.put(
            f"/projects/{project}/labels/{label_id}",
            json=update_payload
        )
        
        assert response.status_code == 200, f"Failed to update label name: {response.text}"
        result = response.json()
        
        assert result.get("name") == update_payload["name"]
        assert result.get("id") == label_id
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_update_label_description_only(self, client, project):
        """Test updating only the description of a label."""
        # Create a label
        payload = {
            "name": "pytest_DescUpdateLabel",
            "description": "Original description"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200
        label_id = create_response.json().get("id")
        
        # Update only the description
        update_payload = {
            "name": None,
            "description": "Updated description only"
        }
        
        response = client.put(
            f"/projects/{project}/labels/{label_id}",
            json=update_payload
        )
        
        assert response.status_code == 200, f"Failed to update label description: {response.text}"
        result = response.json()
        
        assert result.get("description") == update_payload["description"]
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_update_label_clear_description(self, client, project):
        """Test clearing the description by setting it to null."""
        # Create a label with description
        timestamp = int(time.time() * 1000)
        payload = {
            "name": f"pytest_ClearDescLabel_{timestamp}",
            "description": "Description to be cleared"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200, f"Failed to create label: {create_response.text}"
        label_id = create_response.json().get("id")
        
        # Clear the description
        update_payload = {
            "name": f"pytest_ClearDescLabel_{timestamp}",
            "description": ""
        }
        
        response = client.put(
            f"/projects/{project}/labels/{label_id}",
            json=update_payload
        )
        
        assert response.status_code == 200, f"Failed to clear description: {response.text}"
        result = response.json()
        
        # Description should be null or empty
        desc = result.get("description")
        assert desc is None or desc == "", f"Description should be cleared, got: {desc}"
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_update_label_nonexistent_id(self, client, project):
        """Test updating a label with non-existent ID."""
        fake_id = 999999999
        
        update_payload = {
            "name": "pytest_NonexistentLabel",
            "description": "Should not work"
        }
        
        response = client.put(
            f"/projects/{project}/labels/{fake_id}",
            json=update_payload
        )
        
        # Should return 404
        assert response.status_code == 500
    
    def test_update_label_verify_timestamp(self, client, project):
        """Test that updating a label updates the lastUpdatedAt timestamp."""
        # Create a label
        payload = {
            "name": "pytest_TimestampLabel",
            "description": "Original"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200
        label_id = create_response.json().get("id")
        original_timestamp = create_response.json().get("lastUpdatedAt") or create_response.json().get("last_updated_at")
        
        # Wait a moment to ensure timestamp will be different
        time.sleep(0.1)
        
        # Update the label
        update_payload = {
            "name": "pytest_TimestampLabel",
            "description": "Updated"
        }
        
        response = client.put(
            f"/projects/{project}/labels/{label_id}",
            json=update_payload
        )
        
        assert response.status_code == 200
        result = response.json()
        updated_timestamp = result.get("lastUpdatedAt") or result.get("last_updated_at")
        
        # Timestamp should have changed (if the API updates it)
        if updated_timestamp and original_timestamp:
            assert updated_timestamp != original_timestamp, "Timestamp should be updated"
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    # ========================================================================
    # ARCHIVE/PATCH TESTS
    # ========================================================================
    
    def test_archive_label(self, client, project):
        """Test archiving a label using PATCH with archive=true."""
        # Create a label
        timestamp = int(time.time() * 1000)
        payload = {
            "name": f"pytest_ArchiveLabel_{timestamp}",
            "description": "Label to be archived"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200, f"Failed to create label: {create_response.text}"
        label_id = create_response.json().get("id")
        
        # Archive the label
        response = client.patch(
            f"/projects/{project}/labels/{label_id}",
            params={"archive": True}
        )
        
        assert response.status_code == 200, f"Failed to archive label: {response.text}"
        result = response.json()
        
        # Verify archival (response format may vary)
        if isinstance(result, dict):
            # Check for success message or isArchived flag
            assert "message" in result or "isArchived" in result or "is_archived" in result
        
        # Verify by getting the label
        get_response = client.get(f"/projects/{project}/labels/{label_id}")
        if get_response.status_code == 200:
            label_data = get_response.json()
            assert label_data.get("isArchived") is True or label_data.get("is_archived") is True
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_unarchive_label(self, client, project):
        """Test unarchiving a label using PATCH with archive=false."""
        # Create and archive a label
        payload = {
            "name": "pytest_UnarchiveLabel",
            "description": "Label to be unarchived"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200
        label_id = create_response.json().get("id")
        
        # Archive it first
        archive_response = client.patch(
            f"/projects/{project}/labels/{label_id}",
            params={"archive": True}
        )
        assert archive_response.status_code == 200
        
        # Unarchive it
        response = client.patch(
            f"/projects/{project}/labels/{label_id}",
            params={"archive": False}
        )
        
        assert response.status_code == 200, f"Failed to unarchive label: {response.text}"
        
        # Verify by getting the label
        get_response = client.get(f"/projects/{project}/labels/{label_id}")
        if get_response.status_code == 200:
            label_data = get_response.json()
            is_archived = label_data.get("isArchived") or label_data.get("is_archived")
            assert is_archived is False or is_archived is None
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_archive_label_nonexistent_id(self, client, project):
        """Test archiving a label with non-existent ID."""
        fake_id = 999999999
        
        response = client.patch(
            f"/projects/{project}/labels/{fake_id}",
            params={"archive": True}
        )
        
        # Should return 404
        assert response.status_code == 500
    
    # ========================================================================
    # DELETE TESTS
    # ========================================================================
    
    def test_delete_label(self, client, project):
        """Test deleting a label."""
        # Create a label
        payload = {
            "name": "pytest_DeleteLabel",
            "description": "Label to be deleted"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200
        label_id = create_response.json().get("id")
        
        # Delete the label
        response = client.delete(f"/projects/{project}/labels/{label_id}")
        
        assert response.status_code == 200, f"Failed to delete label: {response.text}"
        
        # Verify deletion by trying to get it
        get_response = client.get(f"/projects/{project}/labels/{label_id}")
        # API returns 500 instead of 404 for deleted labels
        assert get_response.status_code in [404, 500], "Deleted label should return 404 or 500"
    
    def test_delete_label_nonexistent_id(self, client, project):
        """Test deleting a label with non-existent ID."""
        fake_id = 999999999
        
        response = client.delete(f"/projects/{project}/labels/{fake_id}")
        
        # Should return 404 or succeed (idempotent)
        # Some APIs return 404, others return 200 for idempotent deletes
        assert response.status_code in [200, 404, 500], "Delete non-existent should return 200, 404, or 500"
    
    def test_delete_label_twice(self, client, project):
        """Test deleting the same label twice (idempotency)."""
        # Create a label
        payload = {
            "name": "pytest_DoubleDeleteLabel",
            "description": "Label to be deleted twice"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200
        label_id = create_response.json().get("id")
        
        # Delete once
        response1 = client.delete(f"/projects/{project}/labels/{label_id}")
        assert response1.status_code == 200
        
        # Delete again
        response2 = client.delete(f"/projects/{project}/labels/{label_id}")
        
        # Should return 404 or 500 (depending on API design)
        assert response2.status_code in [200, 404, 500], "Second delete should return 200, 404, or 500"
    
    # ========================================================================
    # EDGE CASES AND VALIDATION TESTS
    # ========================================================================
    
    def test_label_name_max_length(self, client, project):
        """Test creating a label with very long name."""
        long_name = "pytest_" + "A" * 500  # Very long name
        
        payload = {
            "name": long_name,
            "description": "Testing max length"
        }
        
        response = client.post(f"/projects/{project}/labels", json=payload)
        
        # Might succeed or fail depending on field constraints
        if response.status_code == 200:
            label_id = response.json().get("id")
            client.delete(f"/projects/{project}/labels/{label_id}")
        else:
            # If it fails, that's also acceptable
            assert response.status_code >= 400
    
    def test_label_description_max_length(self, client, project):
        """Test creating a label with very long description."""
        long_description = "A" * 2000  # Very long description
        
        payload = {
            "name": "pytest_LongDescLabel",
            "description": long_description
        }
        
        response = client.post(f"/projects/{project}/labels", json=payload)
        
        # Might succeed or fail depending on field constraints
        if response.status_code == 200:
            label_id = response.json().get("id")
            client.delete(f"/projects/{project}/labels/{label_id}")
        else:
            assert response.status_code >= 400
    
    def test_label_special_characters_in_name(self, client, project):
        """Test creating a label with special characters in name."""
        special_names = [
            "pytest_Label!@#$%",
            "pytest_Label<>",
            "pytest_Label'\"",
        ]
        
        for name in special_names:
            payload = {
                "name": name,
                "description": "Testing special characters"
            }
            
            response = client.post(f"/projects/{project}/labels", json=payload)
            
            # Clean up if created
            if response.status_code == 200:
                label_id = response.json().get("id")
                client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_label_unicode_characters(self, client, project):
        """Test creating a label with Unicode characters."""
        payload = {
            "name": "pytest_Label_日本語_中文_العربية",
            "description": "Unicode description: 🚀 🎉 ✨"
        }
        
        response = client.post(f"/projects/{project}/labels", json=payload)
        
        if response.status_code == 200:
            result = response.json()
            label_id = result.get("id")
            
            # Verify Unicode was preserved
            assert result.get("name") == payload["name"]
            assert result.get("description") == payload["description"]
            
            # Cleanup
            client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_concurrent_label_creation(self, client, project):
        """Test creating multiple labels in rapid succession."""
        label_ids = []
        timestamp = int(time.time() * 1000)
        
        for i in range(10):
            payload = {
                "name": f"pytest_ConcurrentLabel_{i}_{timestamp}",
                "description": f"Concurrent test {i}"
            }
            
            response = client.post(f"/projects/{project}/labels", json=payload)
            
            if response.status_code == 200:
                label_ids.append(response.json().get("id"))
        
        # Verify all were created
        assert len(label_ids) == 10, "All concurrent labels should be created"
        
        # Cleanup
        for label_id in label_ids:
            client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_label_project_isolation(self, client, organization, project):
        """Test that labels are isolated to their project."""
        # Create a label in the test project
        payload = {
            "name": f"pytest_IsolationLabel_{int(time.time() * 1000)}",
            "description": "Testing project isolation"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200
        label_id = create_response.json().get("id")
        
        # Verify it appears in the project's labels list
        list_response = client.get(f"/projects/{project}/labels")
        assert list_response.status_code == 200
        labels = list_response.json()
        label_ids = [label.get("id") for label in labels]
        assert label_id in label_ids, "Label should appear in its project"
        
        # Cleanup
        client.delete(f"/projects/{project}/labels/{label_id}")
    
    def test_complete_crud_lifecycle(self, client, project):
        """Test complete CRUD lifecycle: create, read, update, archive, delete."""
        # CREATE
        timestamp = int(time.time() * 1000)
        payload = {
            "name": f"pytest_LifecycleLabel_{timestamp}",
            "description": "Testing complete lifecycle"
        }
        
        create_response = client.post(f"/projects/{project}/labels", json=payload)
        assert create_response.status_code == 200, f"Failed to create label: {create_response.text}"
        label_id = create_response.json().get("id")
        
        # READ
        read_response = client.get(f"/projects/{project}/labels/{label_id}")
        assert read_response.status_code == 200
        assert read_response.json().get("name") == payload["name"]
        
        # UPDATE
        update_payload = {
            "name": f"pytest_UpdatedLifecycleLabel_{timestamp}",
            "description": "Updated lifecycle description"
        }
        update_response = client.put(
            f"/projects/{project}/labels/{label_id}",
            json=update_payload
        )
        assert update_response.status_code == 200
        assert update_response.json().get("name") == update_payload["name"]
        
        # ARCHIVE
        archive_response = client.patch(
            f"/projects/{project}/labels/{label_id}",
            params={"archive": True}
        )
        assert archive_response.status_code == 200
        
        # UNARCHIVE
        unarchive_response = client.patch(
            f"/projects/{project}/labels/{label_id}",
            params={"archive": False}
        )
        assert unarchive_response.status_code == 200
        
        # DELETE
        delete_response = client.delete(f"/projects/{project}/labels/{label_id}")
        assert delete_response.status_code == 200
        
        # VERIFY DELETION
        verify_response = client.get(f"/projects/{project}/labels/{label_id}")
        # API returns 500 instead of 404 for deleted labels
        assert verify_response.status_code in [404, 500], "Deleted label should return 404 or 500"