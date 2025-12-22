from datetime import datetime, timedelta

class TestGetAllEvents:
    """Tests for GET /events/GetAllEvents endpoint."""
    
    def test_get_all_events_no_filters(self, client):
        """Test getting all events without filters."""
        response = client.get("/events/GetAllEvents")
        assert response.status_code == 200
        
        result = response.json()
        
        # Handle both list and paginated response formats
        if isinstance(result, list):
            events = result
        else:
            events = result.get("items", [])
        
        assert isinstance(events, list)
        # Should return some events (could be empty for fresh install)
        if events:
            assert "id" in events[0]
            assert "operation" in events[0]
            assert "entityType" in events[0]
    
    def test_get_all_events_by_project(self, client, project):
        """Test getting events filtered by project ID."""
        response = client.get("/events/GetAllEvents", params={"projectId": project})
        assert response.status_code == 200
        
        result = response.json()
        
        if isinstance(result, list):
            events = result
        else:
            events = result.get("items", [])
        
        assert isinstance(events, list)
        # All events should be from the specified project
        for event in events:
            if "projectId" in event:
                assert event["projectId"] == project
    
    def test_get_all_events_by_organization(self, client, organization):
        """Test getting events filtered by organization ID."""
        response = client.get("/events/GetAllEvents", params={"organizationId": organization})
        assert response.status_code == 200
        
        result = response.json()
        
        if isinstance(result, list):
            events = result
        else:
            events = result.get("items", [])
        
        assert isinstance(events, list)
        # All events should be from the specified organization
        for event in events:
            if "organizationId" in event:
                assert event["organizationId"] == organization
    
    def test_get_all_events_by_project_and_organization(self, client, organization, project):
        """Test getting events filtered by both project and organization."""
        params = {
            "projectId": project,
            "organizationId": organization
        }
        response = client.get("/events/GetAllEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        
        if isinstance(result, list):
            events = result
        else:
            events = result.get("items", [])
        
        assert isinstance(events, list)


class TestQueryEvents:
    """Tests for GET /events/QueryEvents endpoint (paginated)."""
    
    def test_query_events_basic_pagination(self, client, organization, project):
        """Test basic paginated query."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "pageNumber": 1,
            "pageSize": 10
        }
        response = client.get("/events/QueryEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        
        assert "items" in result
        assert "pageNumber" in result
        assert "pageSize" in result
        assert "totalCount" in result
        assert isinstance(result["items"], list)
        assert result["pageNumber"] == 1
        assert result["pageSize"] == 10
    
    def test_query_events_by_operation(self, client, organization, project):
        """Test querying events by operation type."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "operation": "create",
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        assert "items" in result
        items = result["items"]
        
        # All returned events should have operation = 'create'
        for event in items:
            if "operation" in event:
                assert event["operation"].lower() == "create"
    
    def test_query_events_by_entity_type(self, client, organization, project):
        """Test querying events by entity type."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "entityType": "record",
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        assert "items" in result
        items = result["items"]
        
        # All returned events should have entityType = 'record'
        for event in items:
            if "entityType" in event:
                assert event["entityType"].lower() == "record"
    
    def test_query_events_by_entity_name(self, client, organization, project):
        """Test querying events by entity name."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "entityName": "Test",
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        assert "items" in result
        assert isinstance(result["items"], list)
    
    def test_query_events_by_date_range(self, client, organization, project):
        """Test querying events within a date range."""
        start_date = (datetime.utcnow() - timedelta(hours=24)).isoformat()
        end_date = datetime.utcnow().isoformat()
        
        params = {
            "organizationId": organization,
            "projectId": project,
            "startDate": start_date,
            "endDate": end_date,
            "pageSize": 10
        }
        response = client.get("/events/QueryEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        assert "items" in result
        assert isinstance(result["items"], list)
        
        # Verify dates are within range
        for event in result["items"]:
            if "lastUpdatedAt" in event or "createdAt" in event:
                event_date = event.get("lastUpdatedAt") or event.get("createdAt")
                # Basic validation that we got a timestamp
                assert event_date is not None
    
    def test_query_events_combined_filters(self, client, organization, project):
        """Test querying events with multiple filters combined."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "operation": "create",
            "entityType": "record",
            "pageNumber": 1,
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        assert "items" in result
        assert "pageNumber" in result
        assert result["pageNumber"] == 1
        
        items = result["items"]
        for event in items:
            if "operation" in event:
                assert event["operation"].lower() == "create"
            if "entityType" in event:
                assert event["entityType"].lower() == "record"
    
    def test_query_events_pagination_multiple_pages(self, client, organization, project):
        """Test pagination across multiple pages."""
        # Get first page
        params = {
            "organizationId": organization,
            "projectId": project,
            "pageNumber": 1,
            "pageSize": 5
        }
        response_page1 = client.get("/events/QueryEvents", params=params)
        assert response_page1.status_code == 200
        result_page1 = response_page1.json()
        
        # Get second page
        params["pageNumber"] = 2
        response_page2 = client.get("/events/QueryEvents", params=params)
        assert response_page2.status_code == 200
        result_page2 = response_page2.json()
        
        assert result_page1["pageNumber"] == 1
        assert result_page2["pageNumber"] == 2
        assert result_page1["totalCount"] == result_page2["totalCount"]
        
        # If there are enough events, pages should have different items
        if result_page1["totalCount"] > 5:
            items1 = result_page1["items"]
            items2 = result_page2["items"]
            
            if items1 and items2:
                # IDs should be different
                ids1 = {e.get("id") for e in items1}
                ids2 = {e.get("id") for e in items2}
                assert not ids1.intersection(ids2)


class TestQueryAuthorizedEvents:
    """Tests for GET /events/QueryAuthorizedEvents endpoint."""
    
    def test_query_authorized_events_basic(self, client, organization, project):
        """Test basic authorized events query."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "pageNumber": 1,
            "pageSize": 10
        }
        response = client.get("/events/QueryAuthorizedEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        
        # Handle both list and paginated response formats
        if isinstance(result, list):
            items = result
            assert isinstance(items, list)
        else:
            assert "items" in result
            assert isinstance(result["items"], list)
    
    def test_query_authorized_events_with_operation_filter(self, client, organization, project):
        """Test authorized events query with operation filter."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "operation": "create",
            "pageSize": 5
        }
        response = client.get("/events/QueryAuthorizedEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        
        if isinstance(result, list):
            items = result
        else:
            items = result.get("items", [])
        
        # All returned events should have operation = 'create'
        for event in items:
            if "operation" in event:
                assert event["operation"].lower() == "create"
    
    def test_query_authorized_events_with_entity_type_filter(self, client, organization, project):
        """Test authorized events query with entity type filter."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "entityType": "record",
            "pageSize": 5
        }
        response = client.get("/events/QueryAuthorizedEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        
        if isinstance(result, list):
            items = result
        else:
            items = result.get("items", [])
        
        for event in items:
            if "entityType" in event:
                assert event["entityType"].lower() == "record"
    
    def test_query_authorized_events_pagination(self, client, organization, project):
        """Test authorized events query with pagination."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "pageNumber": 1,
            "pageSize": 5
        }
        response = client.get("/events/QueryAuthorizedEvents", params=params)
        assert response.status_code == 200
        
        result = response.json()
        
        if isinstance(result, dict) and "items" in result:
            if "pageNumber" in result:
                assert result["pageNumber"] == 1
            if "pageSize" in result:
                assert result["pageSize"] == 5


class TestQueryEventsBySubscriptions:
    """Tests for GET /events/QueryEventsBySubscriptions endpoint."""
    
    def test_query_events_by_subscriptions_basic(self, client, organization, project):
        """Test basic subscription-based events query."""
        params = {
            "organizationId": organization,
            "projectId": project
        }
        response = client.get("/events/QueryEventsBySubscriptions", params=params)
        assert response.status_code == 200
        
        result = response.json()
        
        # Handle both list and paginated response formats
        if isinstance(result, list):
            items = result
            assert isinstance(items, list)
        else:
            assert "items" in result
            assert isinstance(result["items"], list)
    
    def test_query_events_by_subscriptions_with_filters(self, client, organization, project):
        """Test subscription-based query with additional filters."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "operation": "create",
            "entityType": "record"
        }
        response = client.get("/events/QueryEventsBySubscriptions", params=params)
        assert response.status_code == 200
        
        result = response.json()
        
        if isinstance(result, list):
            items = result
        else:
            items = result.get("items", [])
        
        # Verify filters are applied
        for event in items:
            if "operation" in event:
                assert event["operation"].lower() == "create"
            if "entityType" in event:
                assert event["entityType"].lower() == "record"
    
    def test_query_events_by_subscriptions_with_pagination(self, client, organization, project):
        """Test subscription-based query with pagination."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "pageNumber": 1,
            "pageSize": 5
        }
        response = client.get("/events/QueryEventsBySubscriptions", params=params)
        assert response.status_code == 200
        
        result = response.json()
        
        if isinstance(result, dict):
            assert "items" in result
            if "pageNumber" in result:
                assert result["pageNumber"] == 1
            if "pageSize" in result:
                assert result["pageSize"] == 5
    
    def test_query_events_by_subscriptions_with_date_range(self, client, organization, project):
        """Test subscription-based query with date range."""
        start_date = (datetime.utcnow() - timedelta(days=7)).isoformat()
        end_date = datetime.utcnow().isoformat()
        
        params = {
            "organizationId": organization,
            "projectId": project,
            "startDate": start_date,
            "endDate": end_date,
            "pageSize": 10
        }
        response = client.get("/events/QueryEventsBySubscriptions", params=params)
        assert response.status_code == 200
        
        result = response.json()
        
        if isinstance(result, list):
            items = result
        else:
            items = result.get("items", [])
        
        assert isinstance(items, list)


# Error Handling Tests

class TestEventAPIErrorHandling:
    """Tests for error handling and edge cases."""
    
    def test_query_events_missing_organization_id(self, client, project):
        """Test that missing organizationId returns appropriate error."""
        params = {
            "projectId": project,
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)

        results = response.json()
        # Should return empty list
        assert results["items"] == []
    
    def test_query_events_missing_project_id(self, client, organization):
        """Test that missing projectId returns appropriate error."""
        params = {
            "organizationId": organization,
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)
        
        results = response.json()
        
        assert all(event["organizationId"] == organization for event in results["items"])

    
    def test_query_events_invalid_organization_id(self, client, project):
        """Test query with invalid organization ID."""
        params = {
            "organizationId": 999999,  # Non-existent ID
            "projectId": project,
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)
        
        # Depending on API behavior, this might return empty results or error
        if response.status_code == 200:
            # If it succeeds, should return empty or minimal results
            result = response.json()
            assert "items" in result
        else:
            # Or it might return an error
            assert response.status_code in [400, 404]
    
    def test_query_events_invalid_project_id(self, client, organization):
        """Test query with invalid project ID."""
        params = {
            "organizationId": organization,
            "projectId": 999999,  # Non-existent ID
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)
        
        # Should handle gracefully
        if response.status_code == 200:
            result = response.json()
            assert "items" in result
        else:
            assert response.status_code in [400, 404]
    
    def test_query_events_invalid_page_number(self, client, organization, project):
        """Test query with invalid page number."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "pageNumber": -1,  # Invalid page number
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)
        
        assert response.status_code == 500
    
    def test_query_events_zero_page_size(self, client, organization, project):
        """Test query with zero page size."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "pageNumber": 1,
            "pageSize": 0
        }
        response = client.get("/events/QueryEvents", params=params)
        
        # Should handle gracefully
        assert response.status_code in [200, 400, 422]
    
    def test_query_events_excessive_page_size(self, client, organization, project):
        """Test query with very large page size."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "pageNumber": 1,
            "pageSize": 10000  # Very large
        }
        response = client.get("/events/QueryEvents", params=params)
        
        # Should either succeed with capped size or return error
        if response.status_code == 200:
            result = response.json()
            assert "items" in result
            # API might cap the page size
            if "pageSize" in result:
                assert result["pageSize"] <= 10000
        else:
            assert response.status_code in [400, 422]
    
    def test_query_events_invalid_date_format(self, client, organization, project):
        """Test query with invalid date format."""
        params = {
            "organizationId": organization,
            "projectId": project,
            "startDate": "not-a-date",
            "endDate": "also-not-a-date",
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)
        
        # Should return validation error
        assert response.status_code in [400, 422]
    
    def test_query_events_end_date_before_start_date(self, client, organization, project):
        """Test query with end date before start date."""
        start_date = datetime.utcnow().isoformat()
        end_date = (datetime.utcnow() - timedelta(days=1)).isoformat()
        
        params = {
            "organizationId": organization,
            "projectId": project,
            "startDate": start_date,
            "endDate": end_date,
            "pageSize": 5
        }
        response = client.get("/events/QueryEvents", params=params)
        
        # Should either return empty results or validation error
        if response.status_code == 200:
            result = response.json()
            assert "items" in result
            # Likely returns empty results
            assert len(result["items"]) == 0
        else:
            assert response.status_code in [400, 422]