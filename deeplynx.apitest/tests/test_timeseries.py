"""Tests for Timeseries API endpoints."""

import pytest
import requests
import io


def make_multipart_request(base_url, token, method, endpoint, files=None, data=None, params=None):
    """Helper for multipart/form-data requests since DeepLynxClient only handles JSON."""
    url = f"{base_url}{endpoint}"
    headers = {"Authorization": f"Bearer {token}"}

    if method.upper() == "POST":
        return requests.post(url, files=files, data=data, params=params, headers=headers)
    elif method.upper() == "PATCH":
        return requests.patch(url, files=files, data=data, params=params, headers=headers)
    else:
        raise ValueError(f"Unsupported method: {method}")


# ========================================================================
# QUERY TESTS
# ========================================================================

def get_table_name(upload_result, fallback_filename):
    """Extract table name from upload response.

    The table name is in the uri field as "duckdb://<tablename>".
    """
    uri = upload_result.get("uri")
    if uri and uri.startswith("duckdb://"):
        return uri[len("duckdb://"):]

    # Fallback to other fields if uri not present
    table_name = upload_result.get("tableName") or upload_result.get("name", fallback_filename)
    for ext in [".csv", ".parquet", ".tsv"]:
        if table_name.endswith(ext):
            table_name = table_name[:-len(ext)]
    return table_name


# ========================================================================
# OLAP QUERY TESTS
# ========================================================================

def upload_olap_csv(base_url, auth_token, organization, project, test_datasource_project, cleanup_records):
    """Upload a CSV file record for OLAP endpoint tests."""
    file_content = (
        b"timestamp,value,pressure\n"
        b"2024-01-01T00:00:00Z,100.0,10.0\n"
        b"2024-01-01T00:01:00Z,101.0,10.1\n"
        b"2024-01-01T00:02:00Z,102.0,10.2\n"
        b"2024-01-01T00:03:00Z,103.0,10.3\n"
        b"2024-01-01T00:04:00Z,104.0,10.4\n"
        b"2024-01-01T00:05:00Z,105.0,10.5\n"
    )
    files = {"file": ("pytest_olap_query.csv", io.BytesIO(file_content), "text/csv")}

    upload_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/files",
        files=files,
        params={"dataSourceId": test_datasource_project}
    )

    if upload_response.status_code != 200:
        pytest.skip(f"Upload failed, skipping OLAP query test: {upload_response.text}")

    upload_result = upload_response.json()
    record_id = upload_result.get("id") or upload_result.get("recordId")

    if not record_id:
        pytest.skip("Could not get record ID from upload response")

    cleanup_records.append(record_id)
    return record_id


def get_plot_columns(plot_result):
    """Get plot result columns regardless of JSON naming policy."""
    return plot_result.get("columns") or plot_result.get("Columns") or []


def get_plot_rows(plot_result):
    """Get plot result rows regardless of JSON naming policy."""
    return plot_result.get("data") or plot_result.get("Data") or []


def test_execute_olap_query_applies_request_dto_options(
        client, base_url, auth_token, organization, project, test_datasource_project, cleanup_records):
    """Test that OLAP query accepts the request DTO fields used by the visualizer."""
    record_id = upload_olap_csv(
        base_url, auth_token, organization, project, test_datasource_project, cleanup_records)

    response = client.post(
        f"/organizations/{organization}/projects/{project}/records/{record_id}/olap/query?viewName=data",
        json={"limit": 5, "columns": ["timestamp"]}
    )

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 200, f"OLAP query failed: {response.text}"

    result = response.json()
    assert get_plot_columns(result) == ["timestamp"]
    assert len(get_plot_rows(result)) == 5


def test_execute_olap_query_validation_error_returns_bad_request(client, organization, project):
    """Test that OLAP query request validation errors return 400 Bad Request."""
    response = client.post(
        f"/organizations/{organization}/projects/{project}/records/999999999/olap/query?viewName=data",
        json={"startRow": 10, "stopRow": 1}
    )

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    assert response.status_code == 400
    assert "Start row cannot be greater than stop row" in response.text


def test_query_timeseries(client, base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test querying timeseries data with SQL."""
    # First upload a file to have data to query
    file_content = b"timestamp,value,sensor_id\n2024-01-01T00:00:00Z,100.5,sensor_1\n2024-01-01T00:01:00Z,101.2,sensor_1"
    files = {"file": ("test_query_data.csv", io.BytesIO(file_content), "text/csv")}
    
    upload_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files=files
    )
    
    if upload_response.status_code != 200:
        pytest.skip(f"Upload failed, skipping query test: {upload_response.text}")
    
    upload_result = upload_response.json()
    table_name = get_table_name(upload_result, "test_query_data")
    cleanup_timeseries.append(table_name)
    
    payload = {"query": f"SELECT * FROM \"{table_name}\" LIMIT 10"}
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/query?fileType=csv",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Query failed: {response.text}"


def test_query_timeseries_csv_output(client, base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test querying timeseries data with CSV file output."""
    file_content = b"timestamp,value\n2024-01-01T00:00:00Z,100.5"
    files = {"file": ("query_csv_test.csv", io.BytesIO(file_content), "text/csv")}
    
    upload_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files=files
    )
    
    if upload_response.status_code != 200:
        pytest.skip(f"Upload failed: {upload_response.text}")
    
    upload_result = upload_response.json()
    table_name = get_table_name(upload_result, "query_csv_test")
    cleanup_timeseries.append(table_name)
    
    payload = {"query": f"SELECT * FROM \"{table_name}\""}
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/query?fileType=csv",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Query with CSV output failed: {response.text}"


def test_query_timeseries_no_results(client, organization, project, test_datasource_project):
    """Test querying timeseries data that returns no results."""
    payload = {"query": "SELECT * FROM nonexistent_table_12345 LIMIT 10"}
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/query?fileType=csv",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should return 200 with NoResultsException message or error
    assert response.status_code in [200, 400, 404, 500], f"Unexpected status: {response.text}"


# ========================================================================
# UPLOAD TESTS
# ========================================================================


def test_upload_empty_file(base_url, auth_token, organization, project, test_datasource_project):
    """Test uploading an empty file."""
    files = {"file": ("empty.csv", io.BytesIO(b""), "text/csv")}

    response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files=files
    )

    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")

    # Should reject or handle gracefully
    assert response.status_code in [200, 400, 415, 500], f"Unexpected status: {response.text}"


# ========================================================================
# CHUNKED UPLOAD TESTS
# ========================================================================

def test_start_timeseries_upload(client, organization, project, test_datasource_project):
    """Test starting a chunked timeseries upload."""
    payload = {"fileName": "pytest_chunked_upload.csv"}
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload/start",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Start upload failed: {response.text}"
    
    result = response.json()
    assert "uploadId" in result or "UploadId" in result, "Response should contain uploadId"


def test_upload_timeseries_chunk(client, base_url, auth_token, organization, project, test_datasource_project):
    """Test uploading a chunk of timeseries data."""
    # First start an upload
    start_payload = {"fileName": "pytest_chunk_test.csv"}
    start_response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload/start",
        json=start_payload
    )
    
    assert start_response.status_code == 200, f"Start upload failed: {start_response.text}"
    upload_id = start_response.json().get("uploadId") or start_response.json().get("UploadId")
    
    # Upload a chunk
    chunk_content = b"timestamp,value\n2024-01-01T00:00:00Z,100.5"
    files = {"chunk": ("chunk_0.csv", io.BytesIO(chunk_content), "text/csv")}
    data = {"uploadId": upload_id, "chunkNumber": 0}
    
    response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload/chunk",
        files=files,
        data=data
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Chunk upload failed: {response.text}"
    
    result = response.json()
    assert "chunkUploadStatus" in result or "ChunkUploadStatus" in result, "Response should contain status"


def test_complete_timeseries_upload(client, base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test completing a chunked timeseries upload."""
    # Start upload
    start_payload = {"fileName": "pytest_complete_test.csv"}
    start_response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload/start",
        json=start_payload
    )
    assert start_response.status_code == 200, f"Start upload failed: {start_response.text}"
    upload_id = start_response.json().get("uploadId") or start_response.json().get("UploadId")
    
    # Upload chunk
    chunk_content = b"timestamp,value\n2024-01-01T00:00:00Z,100.5\n2024-01-01T00:01:00Z,101.2"
    files = {"chunk": ("chunk_0.csv", io.BytesIO(chunk_content), "text/csv")}
    data = {"uploadId": upload_id, "chunkNumber": 0}

    chunk_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload/chunk",
        files=files,
        data=data
    )
    assert chunk_response.status_code == 200, f"Chunk upload failed: {chunk_response.text}"
    
    # Complete upload
    complete_payload = {
        "uploadId": upload_id,
        "fileName": "pytest_complete_test.csv",
        "totalChunks": 1
    }
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload/complete",
        json=complete_payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Complete upload failed: {response.text}"
    
    result = response.json()
    if "TimeseriesUploadRecord" in result:
        record = result["TimeseriesUploadRecord"]
        table_name = get_table_name(record, "pytest_complete_test")
        if table_name:
            cleanup_timeseries.append(table_name)


def test_chunked_upload_full_workflow(client, base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test the complete chunked upload workflow: start -> chunks -> complete."""
    file_name = "pytest_full_workflow.csv"
    
    # Step 1: Start upload
    start_response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload/start",
        json={"fileName": file_name}
    )
    assert start_response.status_code == 200, f"Start failed: {start_response.text}"
    upload_id = start_response.json().get("uploadId") or start_response.json().get("UploadId")
    
    # Step 2: Upload multiple chunks
    chunks = [
        b"timestamp,value,sensor\n2024-01-01T00:00:00Z,100.0,s1",
        b"2024-01-01T00:01:00Z,101.0,s1\n2024-01-01T00:02:00Z,102.0,s1",
        b"2024-01-01T00:03:00Z,103.0,s1\n2024-01-01T00:04:00Z,104.0,s1"
    ]
    
    for i, chunk_data in enumerate(chunks):
        files = {"chunk": (f"chunk_{i}.csv", io.BytesIO(chunk_data), "text/csv")}
        data = {"uploadId": upload_id, "chunkNumber": i}

        chunk_response = make_multipart_request(
            base_url, auth_token, "POST",
            f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload/chunk",
            files=files,
            data=data
        )
        assert chunk_response.status_code == 200, f"Chunk {i} failed: {chunk_response.text}"
    
    # Step 3: Complete upload
    complete_response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload/complete",
        json={"uploadId": upload_id, "fileName": file_name, "totalChunks": len(chunks)}
    )
    
    print(f"\nComplete Status: {complete_response.status_code}")
    print(f"Complete Response: {complete_response.text}")
    
    assert complete_response.status_code == 200, f"Complete failed: {complete_response.text}"


# ========================================================================
# APPEND TESTS
# ========================================================================

def test_append_timeseries_table(base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test appending data to an existing timeseries table."""
    # First create a table via upload
    initial_content = b"timestamp,value\n2024-01-01T00:00:00Z,100.0"
    upload_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files={"file": ("pytest_append_test.csv", io.BytesIO(initial_content), "text/csv")}
    )
    
    if upload_response.status_code != 200:
        pytest.skip(f"Initial upload failed: {upload_response.text}")
    
    result = upload_response.json()
    table_name = get_table_name(result, "pytest_append_test")
    cleanup_timeseries.append(table_name)
    
    # Append additional data
    append_content = b"timestamp,value\n2024-01-01T00:01:00Z,101.0\n2024-01-01T00:02:00Z,102.0"
    
    response = make_multipart_request(
        base_url, auth_token, "PATCH",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/append",
        files={"file": ("append_data.csv", io.BytesIO(append_content), "text/csv")},
        params={"tableName": table_name}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Append failed: {response.text}"


# ========================================================================
# INTERPOLATE TESTS
# ========================================================================

def test_interpolate_rows(client, base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test getting every nth row from timeseries data."""
    # Upload test data with many rows
    content = b"timestamp,value\n"
    for i in range(100):
        content += f"2024-01-01T00:{i:02d}:00Z,{100+i}.0\n".encode()
    
    upload_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files={"file": ("pytest_interpolate.csv", io.BytesIO(content), "text/csv")}
    )
    
    if upload_response.status_code != 200:
        pytest.skip(f"Upload failed: {upload_response.text}")
    
    result = upload_response.json()
    table_name = get_table_name(result, "pytest_interpolate")
    cleanup_timeseries.append(table_name)
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/interpolate",
        params={"tableName": table_name, "rowNumber": "10", "fileType": "csv"}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Interpolate failed: {response.text}"


# ========================================================================
# EXPORT TESTS
# ========================================================================

def test_export_timeseries_table(client, base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test exporting a timeseries table to CSV format."""
    content = b"timestamp,value\n2024-01-01T00:00:00Z,100.0\n2024-01-01T00:01:00Z,101.0"
    upload_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files={"file": ("pytest_export.csv", io.BytesIO(content), "text/csv")}
    )
    
    if upload_response.status_code != 200:
        pytest.skip(f"Upload failed: {upload_response.text}")
    
    result = upload_response.json()
    table_name = get_table_name(result, "pytest_export")
    cleanup_timeseries.append(table_name)
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/export",
        params={"tableName": table_name, "fileType": "csv"}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Export to CSV failed: {response.text}"


def test_export_timeseries_table_unsupported_filetype(client, base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test that exporting with unsupported file type returns an error."""
    content = b"timestamp,value\n2024-01-01T00:00:00Z,100.0"
    upload_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files={"file": ("pytest_export_unsupported.csv", io.BytesIO(content), "text/csv")}
    )
    
    if upload_response.status_code != 200:
        pytest.skip(f"Upload failed: {upload_response.text}")
    
    result = upload_response.json()
    table_name = get_table_name(result, "pytest_export_unsupported")
    cleanup_timeseries.append(table_name)
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/export",
        params={"tableName": table_name, "fileType": "json"}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # JSON is not supported, should return error
    assert response.status_code == 500, f"Expected error for unsupported file type: {response.text}"


# ========================================================================
# PLOT DATA TESTS
# ========================================================================

def test_get_plot_data(client, base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test retrieving plot data for visualization."""
    content = b"timestamp,value\n"
    for i in range(50):
        content += f"2024-01-01T00:{i:02d}:00Z,{100+i}.0\n".encode()
    
    upload_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files={"file": ("pytest_plot.csv", io.BytesIO(content), "text/csv")}
    )
    
    if upload_response.status_code != 200:
        pytest.skip(f"Upload failed: {upload_response.text}")
    
    result = upload_response.json()
    record_id = result.get("id") or result.get("recordId")
    
    if not record_id:
        pytest.skip("Could not get record ID from upload response")
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/plot",
        params={"recordId": record_id, "limit": 100, "rowStride": 1}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Get plot data failed: {response.text}"


def test_get_plot_data_with_stride(client, base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test retrieving downsampled plot data using row stride."""
    content = b"timestamp,value\n"
    for i in range(100):
        content += f"2024-01-01T{i//60:02d}:{i%60:02d}:00Z,{100+i}.0\n".encode()
    
    upload_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files={"file": ("pytest_plot_stride.csv", io.BytesIO(content), "text/csv")}
    )
    
    if upload_response.status_code != 200:
        pytest.skip(f"Upload failed: {upload_response.text}")
    
    result = upload_response.json()
    record_id = result.get("id") or result.get("recordId")
    
    if not record_id:
        pytest.skip("Could not get record ID from upload response")
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/plot",
        params={"recordId": record_id, "limit": 50, "rowStride": 5}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Get plot data with stride failed: {response.text}"


def test_get_plot_data_invalid_record(client, organization, project, test_datasource_project):
    """Test retrieving plot data for a non-existent record."""
    response = client.get(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/plot",
        params={"recordId": 999999999, "limit": 100, "rowStride": 1}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 404, 500], f"Expected error for invalid record: {response.text}"


# ========================================================================
# LATEST ROW TESTS
# ========================================================================

def test_get_latest_row(client, base_url, auth_token, organization, project, test_datasource_project, cleanup_timeseries):
    """Test retrieving the most recent row from timeseries data."""
    content = b"timestamp,value\n2024-01-01T00:00:00Z,100.0\n2024-01-01T00:01:00Z,101.0\n2024-01-01T00:02:00Z,102.0"
    
    upload_response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files={"file": ("pytest_latest.csv", io.BytesIO(content), "text/csv")}
    )
    
    if upload_response.status_code != 200:
        pytest.skip(f"Upload failed: {upload_response.text}")
    
    result = upload_response.json()
    record_id = result.get("id") or result.get("recordId")
    
    if not record_id:
        pytest.skip("Could not get record ID from upload response")
    
    response = client.get(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/latest",
        params={"recordId": record_id}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code == 200, f"Get latest row failed: {response.text}"


def test_get_latest_row_invalid_record(client, organization, project, test_datasource_project):
    """Test retrieving latest row for a non-existent record."""
    response = client.get(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/latest",
        params={"recordId": 999999999}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    assert response.status_code in [400, 404, 500], f"Expected error for invalid record: {response.text}"


# ========================================================================
# ERROR HANDLING TESTS
# ========================================================================

def test_upload_invalid_file_format(base_url, auth_token, organization, project, test_datasource_project):
    """Test uploading an invalid file format."""
    invalid_content = b"this is not valid csv or any structured format {{{{{"
    
    response = make_multipart_request(
        base_url, auth_token, "POST",
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/upload",
        files={"file": ("invalid.txt", io.BytesIO(invalid_content), "text/plain")}
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Should either reject or handle gracefully
    assert response.status_code in [200, 400, 415, 500], f"Unexpected status: {response.text}"


def test_query_with_invalid_sql(client, organization, project, test_datasource_project):
    """Test querying with malformed SQL.

    Note: The query endpoint is async - it returns 200 with a job record
    even for invalid SQL. The actual error occurs during processing.
    """
    payload = {"query": "SELECT * FROM WHERE INVALID SYNTAX !!!"}
    
    response = client.post(
        f"/organizations/{organization}/projects/{project}/datasources/{test_datasource_project}/timeseries/query?fileType=csv",
        json=payload
    )
    
    print(f"\nStatus Code: {response.status_code}")
    print(f"Response Body: {response.text}")
    
    # Query endpoint is async - returns 200 with a job record
    # Invalid SQL fails during async processing, not at request time
    assert response.status_code == 200, f"Unexpected status: {response.text}"

    result = response.json()
    # Should have created a record with "in progress" status
    assert "id" in result, "Response should contain a record id"

    # The query is stored in properties
    if "properties" in result:
        import json
        props = json.loads(result["properties"]) if isinstance(result["properties"], str) else result["properties"]
        assert props.get("status") == "in progress", "Query should be queued for processing"
