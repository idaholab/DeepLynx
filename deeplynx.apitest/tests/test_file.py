"""
Tests for DeepLynx file operations.

Tests cover:
- File upload (creates file records)
- File metadata update
- File download
- File deletion
"""
import pytest
import io
import os
import tempfile
import requests


@pytest.fixture
def cleanup_file_records(client, organization, project):
    """Track and cleanup uploaded file records."""
    created_ids = []
    yield created_ids
    for record_id in created_ids:
        try:
            client.delete(f"/organizations/{organization}/projects/{project}/files/{record_id}")
        except:
            pass


@pytest.fixture
def temp_files():
    """Track and cleanup temporary files created during testing."""
    files = []
    yield files
    for file_path in files:
        try:
            if os.path.exists(file_path):
                os.remove(file_path)
        except:
            pass


def create_temp_file(filename: str, content: str, temp_files: list) -> str:
    """Create a temporary file for testing."""
    temp_file = tempfile.NamedTemporaryFile(mode='w', delete=False, suffix=f'_{filename}')
    temp_file.write(content)
    temp_file.close()
    temp_files.append(temp_file.name)
    return temp_file.name


def test_upload_file(client, organization, project, cleanup_file_records, temp_files):
    """Test uploading a file to DeepLynx."""
    filename = "test_upload.txt"
    content = "This is a test file for upload testing.\nLine 2 of test content."
    
    # Create temporary file
    temp_file_path = create_temp_file(filename, content, temp_files)
    
    # Upload file
    url = f"{client.base_url}/organizations/{organization}/projects/{project}/files"
    
    with open(temp_file_path, 'rb') as f:
        files = {'file': (filename, f, 'text/plain')}
        headers = {"Authorization": f"Bearer {client.token}"}
        
        response = requests.post(url, files=files, headers=headers)
    
    assert response.status_code == 200, f"File upload failed: {response.text}"
    
    result = response.json()
    assert "id" in result, "Response should contain file record ID"
    
    record_id = result["id"]
    cleanup_file_records.append(record_id)
    
    # Verify file record was created
    actual_name = result.get("name") or result.get("fileName")
    assert actual_name, "Should have a name field"
    assert actual_name == filename, f"Expected filename '{filename}', got '{actual_name}'"

def test_upload_file_with_datasource(client, organization, project, test_datasource_project, cleanup_file_records, temp_files):
    """Test uploading a file with a data source ID."""
    filename = "test_datasource.txt"
    content = "Test file with data source association."
    
    temp_file_path = create_temp_file(filename, content, temp_files)
    
    url = f"{client.base_url}/organizations/{organization}/projects/{project}/files"
    params = {"dataSourceId": test_datasource_project}
    
    with open(temp_file_path, 'rb') as f:
        files = {'file': (filename, f, 'text/plain')}
        headers = {"Authorization": f"Bearer {client.token}"}
        
        response = requests.post(url, params=params, files=files, headers=headers)
    
    assert response.status_code == 200, f"File upload with datasource failed: {response.text}"
    
    result = response.json()
    record_id = result["id"]
    cleanup_file_records.append(record_id)
    
    # Verify data source association
    assert result.get("dataSourceId") or result.get("data_source_id"), "Should have data source ID"


def test_upload_multiple_files(client, organization, project, cleanup_file_records, temp_files):
    """Test uploading multiple files."""
    test_files = [
        ("document1.txt", "First test document"),
        ("document2.txt", "Second test document"),
        ("data.csv", "Name,Value\nTest,123")
    ]
    
    url = f"{client.base_url}/organizations/{organization}/projects/{project}/files"
    
    for filename, content in test_files:
        temp_file_path = create_temp_file(filename, content, temp_files)
        
        with open(temp_file_path, 'rb') as f:
            files = {'file': (filename, f, 'text/plain')}
            headers = {"Authorization": f"Bearer {client.token}"}
            
            response = requests.post(url, files=files, headers=headers)
        
        assert response.status_code == 200, f"Failed to upload {filename}: {response.text}"
        
        result = response.json()
        cleanup_file_records.append(result["id"])


def test_update_file(client, organization, project, cleanup_file_records, temp_files):
    """Test updating a file (replacing its content)."""
    # First, upload a file
    original_filename = "original.txt"
    original_content = "Original file content."
    temp_file_path = create_temp_file(original_filename, original_content, temp_files)
    
    url = f"{client.base_url}/organizations/{organization}/projects/{project}/files"
    
    with open(temp_file_path, 'rb') as f:
        files = {'file': (original_filename, f, 'text/plain')}
        headers = {"Authorization": f"Bearer {client.token}"}
        response = requests.post(url, files=files, headers=headers)
    
    assert response.status_code == 200, f"Initial upload failed: {response.text}"
    
    record_id = response.json()["id"]
    cleanup_file_records.append(record_id)
    
    # Now update the file with new content
    updated_filename = "updated.txt"
    updated_content = "This is the UPDATED file content.\nModified for testing."
    updated_temp_path = create_temp_file(updated_filename, updated_content, temp_files)
    
    update_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/{record_id}"
    
    with open(updated_temp_path, 'rb') as f:
        files = {'file': (updated_filename, f, 'text/plain')}
        headers = {"Authorization": f"Bearer {client.token}"}
        response = requests.put(update_url, files=files, headers=headers)
    
    assert response.status_code == 200, f"File update failed: {response.text}"
    
    result = response.json()
    assert result["id"] == record_id, "Record ID should remain the same after update"
    assert "lastUpdatedAt" in result or "last_updated_at" in result or "updatedAt" in result, "Should have update timestamp"


def test_download_file(client, organization, project, cleanup_file_records, temp_files):
    """Test downloading a file from DeepLynx."""
    # Upload a file first
    filename = "download_test.txt"
    original_content = "This is the original content to download.\nSecond line of content."
    temp_file_path = create_temp_file(filename, original_content, temp_files)
    
    upload_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files"
    
    with open(temp_file_path, 'rb') as f:
        files = {'file': (filename, f, 'text/plain')}
        headers = {"Authorization": f"Bearer {client.token}"}
        response = requests.post(upload_url, files=files, headers=headers)
    
    assert response.status_code == 200, f"File upload failed: {response.text}"
    
    record_id = response.json()["id"]
    cleanup_file_records.append(record_id)
    
    # Download the file
    download_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/{record_id}"
    headers = {"Authorization": f"Bearer {client.token}"}
    
    response = requests.get(download_url, headers=headers, stream=True)
    
    assert response.status_code == 200, f"File download failed: {response.text}"
    
    # Verify content
    downloaded_content = response.content.decode('utf-8')
    assert downloaded_content == original_content, "Downloaded content doesn't match original"
    
    # Check headers
    assert "Content-Disposition" in response.headers or "content-disposition" in response.headers, "Should have Content-Disposition header"


def test_download_file_check_headers(client, organization, project, cleanup_file_records, temp_files):
    """Test that download includes proper headers (filename, content-type)."""
    filename = "header_test.txt"
    content = "Testing headers."
    temp_file_path = create_temp_file(filename, content, temp_files)
    
    upload_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files"
    
    with open(temp_file_path, 'rb') as f:
        files = {'file': (filename, f, 'text/plain')}
        headers = {"Authorization": f"Bearer {client.token}"}
        response = requests.post(upload_url, files=files, headers=headers)
    
    record_id = response.json()["id"]
    cleanup_file_records.append(record_id)
    
    # Download and check headers
    download_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/{record_id}"
    headers = {"Authorization": f"Bearer {client.token}"}
    response = requests.get(download_url, headers=headers)
    
    assert response.status_code == 200
    
    # Check Content-Disposition header contains filename
    content_disposition = response.headers.get('Content-Disposition', '').lower()
    assert 'filename' in content_disposition, "Content-Disposition should contain filename"
    
    # Check Content-Type
    content_type = response.headers.get('Content-Type', '')
    assert content_type, "Should have Content-Type header"


def test_download_updated_file(client, organization, project, cleanup_file_records, temp_files):
    """Test that downloading an updated file returns the new content."""
    # Upload original file
    original_filename = "original_download.txt"
    original_content = "Original content for download test."
    temp_file_path = create_temp_file(original_filename, original_content, temp_files)
    
    upload_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files"
    
    with open(temp_file_path, 'rb') as f:
        files = {'file': (original_filename, f, 'text/plain')}
        headers = {"Authorization": f"Bearer {client.token}"}
        response = requests.post(upload_url, files=files, headers=headers)
    
    record_id = response.json()["id"]
    cleanup_file_records.append(record_id)
    
    # Update the file
    updated_filename = "updated_download.txt"
    updated_content = "UPDATED content for download verification."
    updated_temp_path = create_temp_file(updated_filename, updated_content, temp_files)
    
    update_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/{record_id}"
    
    with open(updated_temp_path, 'rb') as f:
        files = {'file': (updated_filename, f, 'text/plain')}
        headers = {"Authorization": f"Bearer {client.token}"}
        response = requests.put(update_url, files=files, headers=headers)
    
    assert response.status_code == 200
    
    # Download and verify we get the updated content
    download_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/{record_id}"
    headers = {"Authorization": f"Bearer {client.token}"}
    response = requests.get(download_url, headers=headers)
    
    assert response.status_code == 200
    downloaded_content = response.content.decode('utf-8')
    assert downloaded_content == updated_content, "Should download updated content, not original"
    assert downloaded_content != original_content, "Content should have changed"


def test_delete_file(client, organization, project, temp_files):
    """Test deleting a file from DeepLynx."""
    # Upload a file first
    filename = "delete_test.txt"
    content = "File to be deleted."
    temp_file_path = create_temp_file(filename, content, temp_files)
    
    upload_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files"
    
    with open(temp_file_path, 'rb') as f:
        files = {'file': (filename, f, 'text/plain')}
        headers = {"Authorization": f"Bearer {client.token}"}
        response = requests.post(upload_url, files=files, headers=headers)
    
    assert response.status_code == 200
    record_id = response.json()["id"]
    
    # Delete the file
    delete_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/{record_id}"
    response = client.delete(f"/organizations/{organization}/projects/{project}/files/{record_id}")
    
    assert response.status_code == 200, f"File deletion failed: {response.text}"
    
    # Verify file no longer exists by trying to download
    download_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/{record_id}"
    headers = {"Authorization": f"Bearer {client.token}"}
    response = requests.get(download_url, headers=headers)
    
    # Should return 404 or 500 with "not found" message
    assert response.status_code in [404, 500], "File should not exist after deletion"


def test_delete_nonexistent_file(client, organization, project):
    """Test deleting a file record that doesn't exist."""
    fake_record_id = 999999999
    
    response = client.delete(f"/organizations/{organization}/projects/{project}/files/{fake_record_id}")
    
    # Should return 404 or 500 for non-existent file
    assert response.status_code in [404, 500], "Should return error for non-existent file"


def test_upload_binary_file(client, organization, project, cleanup_file_records, temp_files):
    """Test uploading a binary file."""
    filename = "binary_test.bin"
    binary_content = bytes([0xFF, 0xD8, 0xFF, 0xE0] + list(range(256)))
    
    # Create binary temp file
    temp_file = tempfile.NamedTemporaryFile(mode='wb', delete=False, suffix=f'_{filename}')
    temp_file.write(binary_content)
    temp_file.close()
    temp_files.append(temp_file.name)
    
    url = f"{client.base_url}/organizations/{organization}/projects/{project}/files"
    
    with open(temp_file.name, 'rb') as f:
        files = {'file': (filename, f, 'application/octet-stream')}
        headers = {"Authorization": f"Bearer {client.token}"}
        response = requests.post(url, files=files, headers=headers)
    
    assert response.status_code == 200, f"Binary file upload failed: {response.text}"
    
    record_id = response.json()["id"]
    cleanup_file_records.append(record_id)
    
    # Download and verify binary content
    download_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/{record_id}"
    headers = {"Authorization": f"Bearer {client.token}"}
    response = requests.get(download_url, headers=headers)
    
    assert response.status_code == 200
    assert response.content == binary_content, "Binary content mismatch"


def test_upload_csv_file(client, organization, project, cleanup_file_records, temp_files):
    """Test uploading a CSV file."""
    filename = "test_data.csv"
    content = "Name,Age,City\nAlice,30,New York\nBob,25,San Francisco\nCharlie,35,Boston"
    
    temp_file_path = create_temp_file(filename, content, temp_files)
    
    url = f"{client.base_url}/organizations/{organization}/projects/{project}/files"
    
    with open(temp_file_path, 'rb') as f:
        files = {'file': (filename, f, 'text/csv')}
        headers = {"Authorization": f"Bearer {client.token}"}
        response = requests.post(url, files=files, headers=headers)
    
    assert response.status_code == 200, f"CSV file upload failed: {response.text}"
    
    record_id = response.json()["id"]
    cleanup_file_records.append(record_id)
    
    # Download and verify CSV content
    download_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/{record_id}"
    headers = {"Authorization": f"Bearer {client.token}"}
    response = requests.get(download_url, headers=headers)
    
    assert response.status_code == 200
    assert response.content.decode('utf-8') == content, "CSV content mismatch"