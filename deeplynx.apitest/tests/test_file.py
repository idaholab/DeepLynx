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
    
# ========== CHUNKED UPLOAD TESTS ==========

def create_large_temp_file(size_mb: int, temp_files: list) -> str:
    """Create a temporary binary file of specified size in MB."""
    temp_file = tempfile.NamedTemporaryFile(mode='wb', delete=False, suffix='.bin')
    
    # Write in 1MB chunks to avoid memory issues
    chunk_size = 1024 * 1024  # 1MB
    for _ in range(size_mb):
        temp_file.write(os.urandom(chunk_size))
    
    temp_file.close()
    temp_files.append(temp_file.name)
    return temp_file.name


def test_start_chunked_upload(client, organization, project, test_datasource_project):
    """Test starting a chunked upload session."""
    filename = "test-large-file.bin"
    file_size = 600 * 1024 * 1024  # 600MB
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/files/upload/start",
        json={"fileName": filename, "fileSize": file_size},
        params={"dataSourceId": test_datasource_project}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Start upload failed: {response.text}"
    
    result = response.json()
    assert "uploadId" in result, "Response should contain uploadId"
    assert "chunkSize" in result, "Response should contain chunkSize"
    assert "totalChunks" in result, "Response should contain totalChunks"
    assert result["chunkSize"] > 0, "Chunk size should be positive"
    assert result["totalChunks"] > 0, "Total chunks should be positive"
    
    upload_id = result["uploadId"]
    
    # Cleanup - cancel the upload session
    cleanup_response = client.delete(
        f"/organizations/{organization}/projects/{project}/files/upload/{upload_id}",
        params={"dataSourceId": test_datasource_project}
    )
    assert cleanup_response.status_code == 200


def test_upload_single_chunk(client, organization, project, test_datasource_project):
    """Test uploading a single chunk to an upload session."""
    filename = "test-chunk-upload.bin"
    file_size = 600 * 1024 * 1024  # 600MB
    
    # Start upload session
    start_response = client.post(
        f"/organizations/{organization}/projects/{project}/files/upload/start",
        json={"fileName": filename, "fileSize": file_size},
        params={"dataSourceId": test_datasource_project}
    )
    assert start_response.status_code == 200
    
    session = start_response.json()
    upload_id = session["uploadId"]
    chunk_size = session["chunkSize"]
    
    try:
        # Create a chunk of data (use smaller size for faster testing)
        test_chunk_size = min(chunk_size, 10 * 1024 * 1024)  # 10MB max for testing
        chunk_data = os.urandom(test_chunk_size)
        
        # Upload the chunk using requests directly (multipart form data)
        url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/upload/chunk"
        files = {'chunk': ('chunk_0', chunk_data, 'application/octet-stream')}
        data = {
            'uploadId': upload_id,
            'chunkNumber': '0'
        }
        headers = {"Authorization": f"Bearer {client.token}"}
        
        response = requests.post(
            url,
            files=files,
            data=data,
            params={"dataSourceId": test_datasource_project},
            headers=headers
        )
        
        print(f"\nChunk Upload Status Code: {response.status_code}")
        print(f"Chunk Upload Response Body: {response.text}")
        
        assert response.status_code == 200, f"Chunk upload failed: {response.text}"
        
        result = response.json()
        assert "ChunkUploadStatus" in result, "Response should contain ChunkUploadStatus"
        assert result["ChunkUploadStatus"] == "success", "Chunk upload should succeed"
        
    finally:
        # Cleanup - cancel the upload session
        client.delete(
            f"/organizations/{organization}/projects/{project}/files/upload/{upload_id}",
            params={"dataSourceId": test_datasource_project}
        )


def test_complete_chunked_upload_small_file(client, organization, project, test_datasource_project, cleanup_file_records, temp_files):
    """Test complete chunked upload workflow with a small file (for faster testing)."""
    filename = "test-chunked-complete.bin"
    
    # Create a 20MB test file (will be split into chunks)
    temp_file_path = create_large_temp_file(20, temp_files)
    file_size = os.path.getsize(temp_file_path)
    
    # Start upload session
    start_response = client.post(
        f"/organizations/{organization}/projects/{project}/files/upload/start",
        json={"fileName": filename, "fileSize": file_size},
        params={"dataSourceId": test_datasource_project}
    )
    
    assert start_response.status_code == 200, f"Start upload failed: {start_response.text}"
    
    session = start_response.json()
    upload_id = session["uploadId"]
    chunk_size = session["chunkSize"]
    total_chunks = session["totalChunks"]
    
    print(f"\nUpload ID: {upload_id}")
    print(f"Chunk Size: {chunk_size}")
    print(f"Total Chunks: {total_chunks}")
    print(f"File Size: {file_size}")
    
    try:
        # Upload all chunks
        url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/upload/chunk"
        headers = {"Authorization": f"Bearer {client.token}"}
        
        with open(temp_file_path, 'rb') as f:
            for chunk_number in range(total_chunks):
                chunk_data = f.read(chunk_size)
                
                files = {'chunk': (f'chunk_{chunk_number}', chunk_data, 'application/octet-stream')}
                data = {
                    'uploadId': upload_id,
                    'chunkNumber': str(chunk_number)
                }
                
                response = requests.post(
                    url,
                    files=files,
                    data=data,
                    params={"dataSourceId": test_datasource_project},
                    headers=headers
                )
                
                assert response.status_code == 200, f"Chunk {chunk_number} upload failed: {response.text}"
                print(f"Uploaded chunk {chunk_number + 1}/{total_chunks}")
        
        # Complete the upload
        complete_response = client.post(
            f"/organizations/{organization}/projects/{project}/files/upload/complete",
            json={
                "uploadId": upload_id,
                "fileName": filename,
                "totalChunks": total_chunks
            },
            params={"dataSourceId": test_datasource_project}
        )
        
        print(f"\nComplete Status Code: {complete_response.status_code}")
        print(f"Complete Response Body: {complete_response.text}")
        
        assert complete_response.status_code == 200, f"Complete upload failed: {complete_response.text}"
        
        result = complete_response.json()
        record_id = result["id"]
        cleanup_file_records.append(record_id)
        
        # Verify file record
        assert result["name"] == filename, f"Expected filename '{filename}', got '{result['name']}'"
        assert "id" in result, "Response should contain file record ID"
        assert "uri" in result, "Response should contain file URI"
        
        # Verify chunked upload metadata
        properties = result.get("properties", {})
        assert properties.get("uploadedViaChunking") == True, "Should be marked as chunked upload"
        assert properties.get("originalUploadId") == upload_id, "Should contain original upload ID"
        
    except Exception as e:
        # If test fails, cleanup the upload session
        client.delete(
            f"/organizations/{organization}/projects/{project}/files/upload/{upload_id}",
            params={"dataSourceId": test_datasource_project}
        )
        raise e


def test_cancel_chunked_upload(client, organization, project, test_datasource_project):
    """Test canceling a chunked upload session."""
    filename = "test-cancel.bin"
    file_size = 600 * 1024 * 1024  # 600MB
    
    # Start upload session
    start_response = client.post(
        f"/organizations/{organization}/projects/{project}/files/upload/start",
        json={"fileName": filename, "fileSize": file_size},
        params={"dataSourceId": test_datasource_project}
    )
    
    assert start_response.status_code == 200
    upload_id = start_response.json()["uploadId"]
    
    # Cancel the upload
    cancel_response = client.delete(
        f"/organizations/{organization}/projects/{project}/files/upload/{upload_id}",
        params={"dataSourceId": test_datasource_project}
    )
    
    print(f"\nCancel Status Code: {cancel_response.status_code}")
    print(f"Cancel Response Body: {cancel_response.text}")
    
    assert cancel_response.status_code == 200, f"Cancel upload failed: {cancel_response.text}"
    
    result = cancel_response.json()
    assert "message" in result, "Response should contain message"
    assert upload_id in result["message"], "Message should reference the upload ID"


def test_chunked_upload_with_missing_chunks(client, organization, project, test_datasource_project, temp_files):
    """Test that completing upload with missing chunks fails appropriately."""
    filename = "test-missing-chunks.bin"
    
    # Create a 20MB test file
    temp_file_path = create_large_temp_file(20, temp_files)
    file_size = os.path.getsize(temp_file_path)
    
    # Start upload session
    start_response = client.post(
        f"/organizations/{organization}/projects/{project}/files/upload/start",
        json={"fileName": filename, "fileSize": file_size},
        params={"dataSourceId": test_datasource_project}
    )
    
    assert start_response.status_code == 200
    
    session = start_response.json()
    upload_id = session["uploadId"]
    chunk_size = session["chunkSize"]
    total_chunks = session["totalChunks"]
    
    try:
        # Only upload the first chunk (intentionally skip the rest)
        url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/upload/chunk"
        headers = {"Authorization": f"Bearer {client.token}"}
        
        with open(temp_file_path, 'rb') as f:
            chunk_data = f.read(chunk_size)
            
            files = {'chunk': ('chunk_0', chunk_data, 'application/octet-stream')}
            data = {
                'uploadId': upload_id,
                'chunkNumber': '0'
            }
            
            response = requests.post(
                url,
                files=files,
                data=data,
                params={"dataSourceId": test_datasource_project},
                headers=headers
            )
            
            assert response.status_code == 200
        
        # Try to complete with missing chunks
        complete_response = client.post(
            f"/organizations/{organization}/projects/{project}/files/upload/complete",
            json={
                "uploadId": upload_id,
                "fileName": filename,
                "totalChunks": total_chunks
            },
            params={"dataSourceId": test_datasource_project}
        )
        
        print(f"\nComplete Status Code: {complete_response.status_code}")
        print(f"Complete Response Body: {complete_response.text}")
        
        # Should fail because chunks are missing
        assert complete_response.status_code in [400, 500], "Should fail with missing chunks"
        
    finally:
        # Cleanup - cancel the upload session
        client.delete(
            f"/organizations/{organization}/projects/{project}/files/upload/{upload_id}",
            params={"dataSourceId": test_datasource_project}
        )


def test_chunked_upload_with_invalid_upload_id(client, organization, project, test_datasource_project):
    """Test that uploading a chunk with invalid upload ID fails."""
    chunk_data = os.urandom(1024 * 1024)  # 1MB
    
    url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/upload/chunk"
    files = {'chunk': ('chunk_0', chunk_data, 'application/octet-stream')}
    data = {
        'uploadId': 'invalid-upload-id-12345',
        'chunkNumber': '0'
    }
    headers = {"Authorization": f"Bearer {client.token}"}
    
    response = requests.post(
        url,
        files=files,
        data=data,
        params={"dataSourceId": test_datasource_project},
        headers=headers
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should fail with 400 or 500 for invalid upload ID
    assert response.status_code in [400, 500], "Should fail with invalid upload ID"


def test_download_chunked_uploaded_file(client, organization, project, test_datasource_project, cleanup_file_records, temp_files):
    """Test that a file uploaded via chunking can be downloaded correctly."""
    filename = "test-download-chunked.bin"
    
    # Create a 20MB test file with random data
    temp_file_path = create_large_temp_file(20, temp_files)
    file_size = os.path.getsize(temp_file_path)
    
    # Read original content for comparison
    with open(temp_file_path, 'rb') as f:
        original_content = f.read()
    
    # Start upload session
    start_response = client.post(
        f"/organizations/{organization}/projects/{project}/files/upload/start",
        json={"fileName": filename, "fileSize": file_size},
        params={"dataSourceId": test_datasource_project}
    )
    
    assert start_response.status_code == 200
    
    session = start_response.json()
    upload_id = session["uploadId"]
    chunk_size = session["chunkSize"]
    total_chunks = session["totalChunks"]
    
    try:
        # Upload all chunks
        url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/upload/chunk"
        headers = {"Authorization": f"Bearer {client.token}"}
        
        with open(temp_file_path, 'rb') as f:
            for chunk_number in range(total_chunks):
                chunk_data = f.read(chunk_size)
                
                files = {'chunk': (f'chunk_{chunk_number}', chunk_data, 'application/octet-stream')}
                data = {
                    'uploadId': upload_id,
                    'chunkNumber': str(chunk_number)
                }
                
                response = requests.post(
                    url,
                    files=files,
                    data=data,
                    params={"dataSourceId": test_datasource_project},
                    headers=headers
                )
                
                assert response.status_code == 200
        
        # Complete the upload
        complete_response = client.post(
            f"/organizations/{organization}/projects/{project}/files/upload/complete",
            json={
                "uploadId": upload_id,
                "fileName": filename,
                "totalChunks": total_chunks
            },
            params={"dataSourceId": test_datasource_project}
        )
        
        assert complete_response.status_code == 200
        
        record_id = complete_response.json()["id"]
        cleanup_file_records.append(record_id)
        
        # Download the file
        download_url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/{record_id}"
        headers = {"Authorization": f"Bearer {client.token}"}
        download_response = requests.get(download_url, headers=headers, stream=True)
        
        assert download_response.status_code == 200, f"Download failed: {download_response.text}"
        
        # Verify downloaded content matches original
        downloaded_content = download_response.content
        assert len(downloaded_content) == len(original_content), "Downloaded file size mismatch"
        assert downloaded_content == original_content, "Downloaded content doesn't match original"
        
    except Exception as e:
        # If test fails, cleanup the upload session
        client.delete(
            f"/organizations/{organization}/projects/{project}/files/upload/{upload_id}",
            params={"dataSourceId": test_datasource_project}
        )
        raise e


def test_chunked_upload_chunk_ordering(client, organization, project, test_datasource_project, cleanup_file_records, temp_files):
    """Test that chunks can be uploaded in non-sequential order (tests backend robustness)."""
    filename = "test-chunk-order.bin"
    
    # Create a small test file
    temp_file_path = create_large_temp_file(15, temp_files)  # 15MB
    file_size = os.path.getsize(temp_file_path)
    
    # Start upload session
    start_response = client.post(
        f"/organizations/{organization}/projects/{project}/files/upload/start",
        json={"fileName": filename, "fileSize": file_size},
        params={"dataSourceId": test_datasource_project}
    )
    
    assert start_response.status_code == 200
    
    session = start_response.json()
    upload_id = session["uploadId"]
    chunk_size = session["chunkSize"]
    total_chunks = session["totalChunks"]
    
    try:
        # Read all chunks first
        chunks_data = []
        with open(temp_file_path, 'rb') as f:
            for _ in range(total_chunks):
                chunk_data = f.read(chunk_size)
                chunks_data.append(chunk_data)
        
        # Upload chunks in reverse order
        url = f"{client.base_url}/organizations/{organization}/projects/{project}/files/upload/chunk"
        headers = {"Authorization": f"Bearer {client.token}"}
        
        for chunk_number in reversed(range(total_chunks)):
            files = {'chunk': (f'chunk_{chunk_number}', chunks_data[chunk_number], 'application/octet-stream')}
            data = {
                'uploadId': upload_id,
                'chunkNumber': str(chunk_number)
            }
            
            response = requests.post(
                url,
                files=files,
                data=data,
                params={"dataSourceId": test_datasource_project},
                headers=headers
            )
            
            assert response.status_code == 200, f"Chunk {chunk_number} upload failed"
            print(f"Uploaded chunk {chunk_number} (reverse order)")
        
        # Complete the upload
        complete_response = client.post(
            f"/organizations/{organization}/projects/{project}/files/upload/complete",
            json={
                "uploadId": upload_id,
                "fileName": filename,
                "totalChunks": total_chunks
            },
            params={"dataSourceId": test_datasource_project}
        )
        
        assert complete_response.status_code == 200, "Complete should succeed even with non-sequential upload"
        
        record_id = complete_response.json()["id"]
        cleanup_file_records.append(record_id)
        
    except Exception as e:
        client.delete(
            f"/organizations/{organization}/projects/{project}/files/upload/{upload_id}",
            params={"dataSourceId": test_datasource_project}
        )
        raise e