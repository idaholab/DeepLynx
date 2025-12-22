"""Tests for Metadata Parsing API endpoints."""

import pytest
import json
import tempfile
import os

# ========================================================================
# METADATA PARSING TESTS
# ========================================================================

def test_parse_metadata_from_raw_JSON(client, organization, project, test_datasource_project, origin_class, destination_class, test_relationship_project):
    """Test parsing metadata from raw JSON payload"""
    
    # Prepare metadata payload
    metadata_payload = {
        "classes": [
            {
                "name": "ParsedClass1",
                "description": "First parsed class from JSON",
                "uuid": "parsed-class-uuid-001"
            },
            {
                "name": "ParsedClass2",
                "description": "Second parsed class from JSON",
                "uuid": "parsed-class-uuid-002"
            }
        ],
        "relationships": [
            {
                "name": "ParsedRelationship1",
                "description": "First parsed relationship",
                "uuid": "parsed-rel-uuid-001",
                "origin_id": origin_class,
                "destination_id": destination_class
            }
        ],
        "tags": [
            {
                "name": "ParsedTag1"
            },
            {
                "name": "ParsedTag2"
            }
        ],
        "records": [
            {
                "name": "ParsedRecord1",
                "description": "First parsed record (origin)",
                "object_storage_id": None,
                "uri": None,
                "properties": {"key": "value"},
                "original_id": "parsed-record-001",
                "class_id": origin_class,
                "class_name": None,
                "file_type": None,
                "tags": ["ParsedTag1"],
                "sensitivity_labels": []
            },
            {
                "name": "ParsedRecord2",
                "description": "Second parsed record (destination)",
                "object_storage_id": None,
                "uri": None,
                "properties": {"status": "active"},
                "original_id": "parsed-record-002",
                "class_id": destination_class,
                "class_name": None,
                "file_type": None,
                "tags": ["ParsedTag2"],
                "sensitivity_labels": []
            }
        ],
        "edges": [
            {
                "origin_id": None,
                "destination_id": None,
                "relationship_id": test_relationship_project,
                "relationship_name": None,
                "origin_oid": "parsed-record-001",
                "destination_oid": "parsed-record-002"
            }
        ]
    }
    
    # Send POST request
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/metadata",
        json=metadata_payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to parse metadata from raw JSON: {response.text}"
    
    result = response.json()
    
    # Verify response structure (adjust based on actual API response)
    # The response might be a success message, parsed data summary, or the created resources
    assert result is not None, "Expected response body from metadata parsing"
    
    print(f"Metadata parsing result: {result}")


def test_parse_metadata_from_raw_JSON_minimal(client, organization, project, test_datasource_project):
    """Test parsing metadata with minimal/empty payload"""
    
    # Minimal metadata payload
    metadata_payload = {
        "classes": [],
        "relationships": [],
        "tags": [],
        "records": [],
        "edges": []
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/metadata",
        json=metadata_payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should succeed even with empty arrays
    assert response.status_code == 200, f"Failed to parse minimal metadata: {response.text}"
    
    result = response.json()
    assert result is not None
    print(f"Minimal metadata parsing result: {result}")


def test_parse_metadata_from_raw_JSON_classes_only(client, organization, project, test_datasource_project):
    """Test parsing metadata with only classes"""
    
    metadata_payload = {
        "classes": [
            {
                "name": "StandaloneClass1",
                "description": "A standalone class",
                "uuid": "standalone-class-001"
            }
        ],
        "relationships": [],
        "tags": [],
        "records": [],
        "edges": []
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/metadata",
        json=metadata_payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to parse metadata with classes only: {response.text}"
    
    result = response.json()
    assert result is not None
    print(f"Classes-only metadata parsing result: {result}")


def test_parse_metadata_from_raw_JSON_records_only(client, organization, project, test_datasource_project, origin_class):
    """Test parsing metadata with only records"""
    
    metadata_payload = {
        "classes": [],
        "relationships": [],
        "tags": [],
        "records": [
            {
                "name": "StandaloneRecord1",
                "description": "A standalone record",
                "object_storage_id": None,
                "uri": None,
                "properties": {"test": "value"},
                "original_id": "standalone-record-001",
                "class_id": origin_class,
                "class_name": None,
                "file_type": None,
                "tags": [],
                "sensitivity_labels": []
            }
        ],
        "edges": []
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/metadata",
        json=metadata_payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Failed to parse metadata with records only: {response.text}"
    
    result = response.json()
    assert result is not None
    print(f"Records-only metadata parsing result: {result}")


def test_parse_metadata_from_raw_JSON_invalid_datasource(client, organization, project):
    """Test parsing metadata with non-existent datasource"""
    
    fake_datasource_id = "00000000-0000-0000-0000-000000000000"
    
    metadata_payload = {
        "classes": [],
        "relationships": [],
        "tags": [],
        "records": [],
        "edges": []
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{fake_datasource_id}/metadata",
        json=metadata_payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should fail with 404 or 500 (depending on backend implementation)
    assert response.status_code in [404, 400, 500], \
        f"Expected 404, 400, or 500 for invalid datasource, got {response.status_code}: {response.text}"
    
    if response.status_code == 500:
        response_text = response.text.lower()
        assert "not found" in response_text or "does not exist" in response_text, \
            "500 error should be due to 'not found' condition"
        print("WARNING: API returns 500 for not found datasource (should be 404)")


def test_parse_metadata_from_a_JSON_file(client, organization, project, test_datasource_project, origin_class, destination_class, test_relationship_project):
    """Test parsing metadata from a JSON file upload"""
    
    # Create metadata payload
    metadata_data = {
        "classes": [
            {
                "name": "FileClass1",
                "description": "First class from file",
                "uuid": "file-class-uuid-001"
            },
            {
                "name": "FileClass2",
                "description": "Second class from file",
                "uuid": "file-class-uuid-002"
            }
        ],
        "relationships": [
            {
                "name": "FileRelationship1",
                "description": "Relationship from file",
                "uuid": "file-rel-uuid-001",
                "origin_id": origin_class,
                "destination_id": destination_class
            }
        ],
        "tags": [
            {
                "name": "FileTag1"
            }
        ],
        "records": [
            {
                "name": "FileRecord1",
                "description": "Origin record from file",
                "object_storage_id": None,
                "uri": None,
                "properties": {"source": "file"},
                "original_id": "file-record-001",
                "class_id": origin_class,
                "class_name": None,
                "file_type": None,
                "tags": ["FileTag1"],
                "sensitivity_labels": []
            },
            {
                "name": "FileRecord2",
                "description": "Destination record from file",
                "object_storage_id": None,
                "uri": None,
                "properties": {"source": "file"},
                "original_id": "file-record-002",
                "class_id": destination_class,
                "class_name": None,
                "file_type": None,
                "tags": ["FileTag1"],
                "sensitivity_labels": []
            }
        ],
        "edges": [
            {
                "origin_id": None,
                "destination_id": None,
                "relationship_id": test_relationship_project,
                "relationship_name": None,
                "origin_oid": "file-record-001",
                "destination_oid": "file-record-002"
            }
        ]
    }
    
    # Create a temporary JSON file
    with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False) as temp_file:
        json.dump(metadata_data, temp_file, indent=2)
        temp_file_path = temp_file.name
    
    try:
        # Read the file and prepare for upload
        with open(temp_file_path, 'rb') as file:
            files = {
                'file': ('metadata.json', file, 'application/json')
            }
            
            # Send POST request with file
            # Note: Using requests directly since our client wrapper might not handle files properly
            import requests
            url = f"{client.base_url}/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/metadata/file"
            response = requests.post(
                url,
                files=files,
                headers={"Authorization": f"Bearer {client.token}"}
            )
        
        print(f"\nStatus Code: {response.status_code}")
        print(f"Response Body: {response.text}")
        
        assert response.status_code == 200, f"Failed to parse metadata from JSON file: {response.text}"
        
        result = response.json()
        assert result is not None, "Expected response body from file metadata parsing"
        
        print(f"File metadata parsing result: {result}")
        
    finally:
        # Clean up temporary file
        if os.path.exists(temp_file_path):
            os.remove(temp_file_path)


def test_parse_metadata_from_a_JSON_file_minimal(client, organization, project, test_datasource_project):
    """Test parsing metadata from a minimal JSON file"""
    
    # Minimal metadata
    metadata_data = {
        "classes": [],
        "relationships": [],
        "tags": [],
        "records": [],
        "edges": []
    }
    
    # Create a temporary JSON file
    with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False) as temp_file:
        json.dump(metadata_data, temp_file, indent=2)
        temp_file_path = temp_file.name
    
    try:
        # Read the file and prepare for upload
        with open(temp_file_path, 'rb') as file:
            files = {
                'file': ('minimal_metadata.json', file, 'application/json')
            }
            
            import requests
            url = f"{client.base_url}/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/metadata/file"
            response = requests.post(
                url,
                files=files,
                headers={"Authorization": f"Bearer {client.token}"}
            )
        
        print(f"\nStatus Code: {response.status_code}")
        print(f"Response Body: {response.text}")
        
        assert response.status_code == 200, f"Failed to parse minimal metadata from file: {response.text}"
        
        result = response.json()
        assert result is not None
        print(f"Minimal file metadata parsing result: {result}")
        
    finally:
        if os.path.exists(temp_file_path):
            os.remove(temp_file_path)


def test_parse_metadata_from_invalid_JSON_file(client, organization, project, test_datasource_project):
    """Test parsing metadata from an invalid JSON file"""
    
    # Create a file with invalid JSON
    with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False) as temp_file:
        temp_file.write("{ this is not valid JSON }")
        temp_file_path = temp_file.name
    
    try:
        with open(temp_file_path, 'rb') as file:
            files = {
                'file': ('invalid.json', file, 'application/json')
            }
            
            import requests
            url = f"{client.base_url}/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/metadata/file"
            response = requests.post(
                url,
                files=files,
                headers={"Authorization": f"Bearer {client.token}"}
            )
        
        print(f"\nStatus Code: {response.status_code}")
        print(f"Response Body: {response.text}")
        
        # Should fail with 400 Bad Request or similar
        assert response.status_code in [400, 422, 500], \
            f"Expected 400, 422, or 500 for invalid JSON, got {response.status_code}: {response.text}"
        
        if response.status_code == 400 or response.status_code == 422:
            print("Correctly rejected invalid JSON")
        elif response.status_code == 500:
            print("WARNING: API returns 500 for invalid JSON (should be 400)")
        
    finally:
        if os.path.exists(temp_file_path):
            os.remove(temp_file_path)


def test_parse_metadata_from_non_JSON_file(client, organization, project, test_datasource_project):
    """Test uploading a non-JSON file to metadata endpoint"""
    
    # Create a text file instead of JSON
    with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', delete=False) as temp_file:
        temp_file.write("This is just plain text, not JSON")
        temp_file_path = temp_file.name
    
    try:
        with open(temp_file_path, 'rb') as file:
            files = {
                'file': ('metadata.txt', file, 'text/plain')
            }
            
            import requests
            url = f"{client.base_url}/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/metadata/file"
            response = requests.post(
                url,
                files=files,
                headers={"Authorization": f"Bearer {client.token}"}
            )
        
        print(f"\nStatus Code: {response.status_code}")
        print(f"Response Body: {response.text}")
        
        # Should fail with 400 or 415 (Unsupported Media Type)
        assert response.status_code in [400, 415, 422, 500], \
            f"Expected 400, 415, 422, or 500 for non-JSON file, got {response.status_code}: {response.text}"
        
        if response.status_code in [400, 415, 422]:
            print("Correctly rejected non-JSON file")
        elif response.status_code == 500:
            print("WARNING: API returns 500 for non-JSON file (should be 400/415)")
        
    finally:
        if os.path.exists(temp_file_path):
            os.remove(temp_file_path)


def test_parse_metadata_file_invalid_datasource(client, organization, project):
    """Test parsing metadata file with non-existent datasource"""
    
    fake_datasource_id = "00000000-0000-0000-0000-000000000000"
    
    metadata_data = {
        "classes": [],
        "relationships": [],
        "tags": [],
        "records": [],
        "edges": []
    }
    
    with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False) as temp_file:
        json.dump(metadata_data, temp_file, indent=2)
        temp_file_path = temp_file.name
    
    try:
        with open(temp_file_path, 'rb') as file:
            files = {
                'file': ('metadata.json', file, 'application/json')
            }
            
            import requests
            url = f"{client.base_url}/organizations/{organization}/projects/{project}/datasources/{fake_datasource_id}/metadata/file"
            response = requests.post(
                url,
                files=files,
                headers={"Authorization": f"Bearer {client.token}"}
            )
        
        print(f"\nStatus Code: {response.status_code}")
        print(f"Response Body: {response.text}")
        
        assert response.status_code in [404, 400, 500], \
            f"Expected 404, 400, or 500 for invalid datasource, got {response.status_code}: {response.text}"
        
        if response.status_code == 500:
            response_text = response.text.lower()
            assert "not found" in response_text or "does not exist" in response_text, \
                "500 error should be due to 'not found' condition"
            print("WARNING: API returns 500 for not found datasource (should be 404)")
        
    finally:
        if os.path.exists(temp_file_path):
            os.remove(temp_file_path)


def test_parse_metadata_with_edges_same_origin_destination_fails(client, organization, project, test_datasource_project, origin_class, test_relationship_project):
    """Test that parsing metadata with edge having same origin and destination fails with validation error"""
    
    metadata_payload = {
        "classes": [],
        "relationships": [],
        "tags": [],
        "records": [
            {
                "name": "SameRecord",
                "description": "A record used as both origin and destination",
                "object_storage_id": None,
                "uri": None,
                "properties": {},
                "original_id": "same-record-001",
                "class_id": origin_class,
                "class_name": None,
                "file_type": None,
                "tags": [],
                "sensitivity_labels": []
            }
        ],
        "edges": [
            {
                "origin_id": None,
                "destination_id": None,
                "relationship_id": test_relationship_project,
                "relationship_name": None,
                "origin_oid": "same-record-001",
                "destination_oid": "same-record-001"  # Same as origin - should fail
            }
        ]
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/metadata",
        json=metadata_payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should fail with validation error
    assert response.status_code in [400, 422, 500], \
        f"Expected 400, 422, or 500 for same origin/destination, got {response.status_code}: {response.text}"
    
    response_text = response.text.lower()
    assert "destination and origin" in response_text or "cannot be the same" in response_text or "validation" in response_text, \
        "Error message should mention validation issue with origin/destination"
    
    if response.status_code == 500:
        print("WARNING: API returns 500 for validation error (should be 400/422)")