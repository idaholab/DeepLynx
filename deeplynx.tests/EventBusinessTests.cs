using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Context;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.EntityFrameworkCore;
using deeplynx.helpers;

namespace deeplynx.tests
{
    [Collection("Test Suite Collection")]
    public class EventBusinessTests : IntegrationTestBase
    {
        private EventBusiness _eventBusiness = null!;
        private INotificationBusiness _notificationBusiness = null!;
        private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
        private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
        private long pid;
        private long pid2;
        private DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        private long mockUserId;
        private long mockUser2Id;
        private long mockUser3Id;
        private long mockUser4Id;
        private long mockActionId;
        private long mockDataSourceId;
        private long mockDataSource2Id;
        private long mockOrganizationId;

        public EventBusinessTests(TestSuiteFixture fixture) : base(fixture)
        {
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
            _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
            _notificationBusiness =
                new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
            _eventBusiness = new EventBusiness(Context, _notificationBusiness);
        }

        #region GetAllEvents (Simplified - No Pagination)

        [Fact]
        public async Task GetAllEvents_Success_NoFilters()
        {
            // Act
            var result = await _eventBusiness.GetAllEvents(null, null);

            // Assert
            Assert.Equal(10, result.Count); // All events from both projects and org level
        }

        [Fact]
        public async Task GetAllEvents_Success_FilterByOrganizationId()
        {
            // Act
            var result = await _eventBusiness.GetAllEvents(mockOrganizationId, null);

            // Assert
            Assert.Equal(10, result.Count); // All events have the same organizationId
            Assert.All(result, e => Assert.Equal(mockOrganizationId, e.OrganizationId));
        }

        [Fact]
        public async Task GetAllEvents_Success_FilterByProjectId()
        {
            // Act
            var result = await _eventBusiness.GetAllEvents(null, pid);

            // Assert
            Assert.Equal(6, result.Count);
            Assert.All(result, e => Assert.Equal(pid, e.ProjectId));
        }

        [Fact]
        public async Task GetAllEvents_Success_FilterByProjectId2()
        {
            // Act
            var result = await _eventBusiness.GetAllEvents(null, pid2);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, e => Assert.Equal(pid2, e.ProjectId));
        }

        #endregion

        #region QueryAllEvents (Paginated with Filters)

        [Fact]
        public async Task QueryAllEvents_Success_DefaultPagination()
        {
            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, null, null);

            // Assert
            Assert.Equal(10, result.TotalCount);
            Assert.Equal(10, result.Items.Count);
            Assert.Equal(1, result.PageNumber);
            Assert.Equal(500, result.PageSize); // Default page size
        }

        [Fact]
        public async Task QueryAllEvents_Success_FilterByProjectId()
        {
            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, pid, null);

            // Assert
            Assert.Equal(6, result.TotalCount);
            Assert.All(result.Items, e => Assert.Equal(pid, e.ProjectId));
        }

        [Fact]
        public async Task QueryAllEvents_Success_FilterByLastUpdatedBy()
        {
            // Arrange
            var queryDto = new EventsQueryRequestDto { LastUpdatedBy = mockUserId };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, null, queryDto);

            // Assert
            Assert.Equal(3, result.TotalCount);
            Assert.All(result.Items, e => Assert.Equal(mockUserId, e.LastUpdatedBy));
        }

        [Fact]
        public async Task QueryAllEvents_Success_FilterByOperation()
        {
            // Arrange
            var queryDto = new EventsQueryRequestDto { Operation = "create" };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, pid, queryDto);

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Items, e => Assert.Equal("create", e.Operation));
        }

        [Fact]
        public async Task QueryAllEvents_Success_FilterByEntityType()
        {
            // Arrange
            var queryDto = new EventsQueryRequestDto { EntityType = "edge" };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, null, queryDto);

            // Assert
            Assert.Equal(8, result.TotalCount);
            Assert.All(result.Items, e => Assert.Equal("edge", e.EntityType));
        }

        [Fact]
        public async Task QueryAllEvents_Success_FilterByEntityName()
        {
            // Arrange
            var queryDto = new EventsQueryRequestDto { EntityName = "TestEntity" };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, pid, queryDto);

            // Assert
            Assert.All(result.Items, e => Assert.Contains("TestEntity", e.EntityName));
        }

        [Fact]
        public async Task QueryAllEvents_Success_FilterByDataSourceName()
        {
            // Arrange
            var queryDto = new EventsQueryRequestDto { DataSourceName = "DataSource1" };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, null, queryDto);

            // Assert
            Assert.Equal(8, result.TotalCount); // 1 event in pid with DataSource1
            Assert.All(result.Items, e => Assert.Equal("DataSource1", e.DataSourceName));
        }

        [Fact]
        public async Task QueryAllEvents_Success_FilterByStartDate()
        {
            // Arrange
            var futureDate = now.AddHours(1);
            var queryDto = new EventsQueryRequestDto { StartDate = futureDate };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, pid, queryDto);

            // Assert
            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        [Fact]
        public async Task QueryAllEvents_Success_FilterByEndDate()
        {
            // Arrange
            var pastDate = now.AddHours(-1);
            var queryDto = new EventsQueryRequestDto { EndDate = pastDate };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, pid, queryDto);

            // Assert
            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        [Fact]
        public async Task QueryAllEvents_Success_FilterByDateRange()
        {
            // Arrange
            var startDate = now.AddHours(-1);
            var endDate = now.AddHours(1);
            var queryDto = new EventsQueryRequestDto
            {
                StartDate = startDate,
                EndDate = endDate
            };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, null, queryDto);

            // Assert
            Assert.Equal(10, result.TotalCount);
            Assert.All(result.Items, e =>
            {
                Assert.True(e.LastUpdatedAt >= startDate);
                Assert.True(e.LastUpdatedAt <= endDate);
            });
        }

        [Fact]
        public async Task QueryAllEvents_Success_MultipleFilters_WithProjectId()
        {
            // Arrange
            var queryDto = new EventsQueryRequestDto
            {
                Operation = "delete",
                EntityType = "edge"
            };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, pid, queryDto);

            // Assert
            Assert.Single(result.Items);
            Assert.Equal(pid, result.Items[0].ProjectId);
            Assert.Equal("delete", result.Items[0].Operation);
            Assert.Equal("edge", result.Items[0].EntityType);
            Assert.Equal(2, result.Items[0].EntityId);
        }

        [Fact]
        public async Task QueryAllEvents_Success_CustomPageSize()
        {
            // Arrange
            var queryDto = new EventsQueryRequestDto
            {
                PageNumber = 1,
                PageSize = 3
            };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, null, queryDto);

            // Assert
            Assert.Equal(10, result.TotalCount);
            Assert.Equal(3, result.Items.Count);
            Assert.Equal(1, result.PageNumber);
            Assert.Equal(3, result.PageSize);
        }

        [Fact]
        public async Task QueryAllEvents_Success_SecondPage()
        {
            // Arrange
            var queryDto = new EventsQueryRequestDto
            {
                PageNumber = 2,
                PageSize = 3
            };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, pid, queryDto);

            // Assert
            Assert.Equal(6, result.TotalCount);
            Assert.Equal(3, result.Items.Count);
            Assert.Equal(2, result.PageNumber);
            Assert.Equal(3, result.PageSize);
        }

        [Fact]
        public async Task QueryAllEvents_Success_WithFiltersAndPagination()
        {
            // Arrange
            var queryDto = new EventsQueryRequestDto
            {
                EntityType = "edge",
                PageNumber = 1,
                PageSize = 2
            };

            // Act
            var result = await _eventBusiness.QueryAllEvents(mockOrganizationId, pid, queryDto);

            // Assert
            Assert.Equal(4, result.TotalCount); // 4 edge events in pid
            Assert.Equal(2, result.Items.Count);
            Assert.All(result.Items, e =>
            {
                Assert.Equal(pid, e.ProjectId);
                Assert.Equal("edge", e.EntityType);
            });
        }

        #endregion

        #region QueryAuthorizedEvents

        [Fact]
        public async Task QueryAuthorizedEvents_Success_WithOrganizationId()
        {
            // Arrange - Add project membership for mockUserId to pid
            var projectMember = new ProjectMember
            {
                ProjectId = pid,
                UserId = mockUserId,
                RoleId = null
            };
            Context.ProjectMembers.Add(projectMember);
            await Context.SaveChangesAsync();

            // Act
            var result = await _eventBusiness.QueryAuthorizedEvents(mockUserId, mockOrganizationId, Array.Empty<long>(), null);

            // Assert
            Assert.Equal(10, result.TotalCount);
            Assert.Equal(10, result.Items.Count);
        }

        [Fact]
        public async Task QueryAuthorizedEvents_Success_WithOrganizationAndProjectId()
        {
            // Arrange - Add project membership
            var projectMember = new ProjectMember
            {
                ProjectId = pid,
                UserId = mockUserId,
                RoleId = null
            };
            Context.ProjectMembers.Add(projectMember);
            await Context.SaveChangesAsync();

            // Act
            var result = await _eventBusiness.QueryAuthorizedEvents(mockUserId, mockOrganizationId, new[] { pid }, null);

            // Assert
            Assert.Equal(6, result.TotalCount);
            Assert.All(result.Items, e => Assert.Equal(pid, e.ProjectId));
        }

        [Fact]
        public async Task QueryAuthorizedEvents_Success_MultipleProjects()
        {
            // Arrange - Add project membership for mockUserId to both projects
            var projectMembers = new List<ProjectMember>
            {
                new ProjectMember { ProjectId = pid, UserId = mockUserId, RoleId = null },
                new ProjectMember { ProjectId = pid2, UserId = mockUserId, RoleId = null }
            };
            Context.ProjectMembers.AddRange(projectMembers);
            await Context.SaveChangesAsync();

            // Act
            var result = await _eventBusiness.QueryAuthorizedEvents(mockUserId, mockOrganizationId, Array.Empty<long>(), null);

            // Assert
            Assert.Equal(10, result.TotalCount); // All events from both projects
        }

        [Fact]
        public async Task QueryAuthorizedEvents_Success_WithPagination()
        {
            // Arrange
            var projectMembers = new List<ProjectMember>
            {
                new ProjectMember { ProjectId = pid, UserId = mockUserId, RoleId = null },
                new ProjectMember { ProjectId = pid2, UserId = mockUserId, RoleId = null }
            };
            Context.ProjectMembers.AddRange(projectMembers);
            await Context.SaveChangesAsync();

            var queryDto = new EventsQueryRequestDto { PageNumber = 1, PageSize = 3 };

            // Act
            var result = await _eventBusiness.QueryAuthorizedEvents(mockUserId, mockOrganizationId, Array.Empty<long>(), queryDto);

            // Assert
            Assert.Equal(10, result.TotalCount);
            Assert.Equal(3, result.Items.Count);
        }

        [Fact]
        public async Task QueryAuthorizedEvents_Success_WithFilters()
        {
            // Arrange
            var projectMember = new ProjectMember { ProjectId = pid, UserId = mockUserId, RoleId = null };
            Context.ProjectMembers.Add(projectMember);
            await Context.SaveChangesAsync();

            var queryDto = new EventsQueryRequestDto { Operation = "create" };

            // Act
            var result = await _eventBusiness.QueryAuthorizedEvents(mockUserId, mockOrganizationId, Array.Empty<long>(), queryDto);

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Items, e => Assert.Equal("create", e.Operation));
        }

        [Fact]
        public async Task QueryAuthorizedEvents_Success_FilterByProjectName()
        {
            // Arrange
            var projectMembers = new List<ProjectMember>
            {
                new ProjectMember { ProjectId = pid, UserId = mockUserId, RoleId = null },
                new ProjectMember { ProjectId = pid2, UserId = mockUserId, RoleId = null }
            };
            Context.ProjectMembers.AddRange(projectMembers);
            await Context.SaveChangesAsync();

            var queryDto = new EventsQueryRequestDto { ProjectName = "Project 2" };

            // Act
            var result = await _eventBusiness.QueryAuthorizedEvents(mockUserId, mockOrganizationId, Array.Empty<long>(), queryDto);

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Items, e => Assert.Equal("Project 2", e.ProjectName));
        }
        #endregion

        #region QueryEventsBySubscriptions Tests

        [Fact]
        public async Task QueryEventsBySubscriptions_Success_ProjectLevel()
        {
            // Arrange
            var subscription = new Subscription
            {
                UserId = mockUserId,
                OrganizationId = mockOrganizationId,
                ProjectId = pid,
                EntityId = null,
                EntityType = null,
                DataSourceId = null,
                Operation = null,
                ActionId = mockActionId
            };
            Context.Subscriptions.Add(subscription);
            await Context.SaveChangesAsync();

            // Act
            var result = await _eventBusiness.QueryEventsBySubscriptions(mockUserId, mockOrganizationId, pid, null);

            // Assert
            Assert.Equal(6, result.TotalCount);
        }

        [Fact]
        public async Task QueryEventsBySubscriptions_Success_WithPagination()
        {
            // Arrange
            var subscription = new Subscription
            {
                UserId = mockUserId,
                OrganizationId = mockOrganizationId,
                ProjectId = pid,
                EntityId = null,
                EntityType = null,
                DataSourceId = null,
                Operation = null,
                ActionId = mockActionId
            };
            Context.Subscriptions.Add(subscription);
            await Context.SaveChangesAsync();

            var queryDto = new EventsQueryRequestDto { PageNumber = 1, PageSize = 3 };

            // Act
            var result = await _eventBusiness.QueryEventsBySubscriptions(mockUserId, mockOrganizationId, pid, queryDto);

            // Assert
            Assert.Equal(6, result.TotalCount);
            Assert.Equal(3, result.Items.Count);
        }

        [Fact]
        public async Task QueryEventsBySubscriptions_Success_WithFilters()
        {
            // Arrange
            var subscription = new Subscription
            {
                UserId = mockUserId,
                OrganizationId = mockOrganizationId,
                ProjectId = pid,
                EntityId = null,
                EntityType = null,
                DataSourceId = null,
                Operation = null,
                ActionId = mockActionId
            };
            Context.Subscriptions.Add(subscription);
            await Context.SaveChangesAsync();

            var queryDto = new EventsQueryRequestDto { Operation = "create" };

            // Act
            var result = await _eventBusiness.QueryEventsBySubscriptions(mockUserId, mockOrganizationId, pid, queryDto);

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Items, e => Assert.Equal("create", e.Operation));
        }

        [Fact]
        public async Task QueryEventsBySubscriptions_Success_MatchingSpecificEntity()
        {
            // Arrange
            var subscription = new Subscription
            {
                UserId = mockUserId,
                OrganizationId = mockOrganizationId,
                ProjectId = pid,
                EntityId = 1,
                EntityType = "edge",
                DataSourceId = mockDataSourceId,
                Operation = "create",
                ActionId = mockActionId
            };
            Context.Subscriptions.Add(subscription);
            await Context.SaveChangesAsync();

            // Act
            var result = await _eventBusiness.QueryEventsBySubscriptions(mockUserId, mockOrganizationId, pid, null);

            // Assert
            Assert.Single(result.Items);
            Assert.Equal("create", result.Items[0].Operation);
            Assert.Equal("edge", result.Items[0].EntityType);
            Assert.Equal(1, result.Items[0].EntityId);
        }

        [Fact]
        public async Task QueryEventsBySubscriptions_Success_MatchingByOperation()
        {
            // Arrange
            var subscriptions = new List<Subscription>
            {
                new Subscription
                {
                    UserId = mockUserId,
                    OrganizationId = mockOrganizationId,
                    ProjectId = pid,
                    EntityId = null,
                    EntityType = null,
                    DataSourceId = null,
                    Operation = "delete",
                    ActionId = mockActionId
                }
            };
            Context.Subscriptions.AddRange(subscriptions);
            await Context.SaveChangesAsync();

            // Act
            var result = await _eventBusiness.QueryEventsBySubscriptions(mockUserId, mockOrganizationId, pid, null);

            // Assert
            Assert.Equal(3, result.TotalCount); // 3 delete events in pid
            Assert.All(result.Items, e => Assert.Equal("delete", e.Operation));
        }

        [Fact]
        public async Task QueryEventsBySubscriptions_ReturnsEmpty_NoSubscriptions()
        {
            // Arrange
            var queryDto = new EventsQueryRequestDto();

            // Act
            var result = await _eventBusiness.QueryEventsBySubscriptions(mockUserId, mockOrganizationId, pid, queryDto);

            // Assert
            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        #endregion

        #region CreateEvent Tests

        [Fact]
        public async Task CreateEvent_Success_WithOrganizationId()
        {
            // Arrange
            var dto = new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "metadata",
                EntityId = 1,
                DataSourceId = null,
                Properties = "{}",
                LastUpdatedBy = mockUserId
            };

            // Act
            var result = await _eventBusiness.CreateEvent(mockUserId, mockOrganizationId, null, dto);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(0, result.Id);
            Assert.Equal(mockOrganizationId, result.OrganizationId);
            Assert.Null(result.ProjectId);
            Assert.Equal(dto.Operation, result.Operation);
            Assert.Equal(dto.EntityType, result.EntityType);
            Assert.Equal("{\"Count\":1}", result.Properties);
        }

        [Fact]
        public async Task CreateEvent_Success_WithProjectId()
        {
            // Arrange
            var dto = new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "metadata",
                EntityId = 1,
                DataSourceId = null,
                Properties = "{}",
                LastUpdatedBy = mockUserId
            };

            // Act
            var result = await _eventBusiness.CreateEvent(mockUserId, mockOrganizationId, pid, dto);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(0, result.Id);
            Assert.Equal(pid, result.ProjectId);
            Assert.Equal(mockOrganizationId, result.OrganizationId);
            Assert.Equal("{\"Count\":1}", result.Properties);
        }

        [Fact]
        public async Task CreateEvent_Fails_IfBadEntityType()
        {
            // Arrange
            var dto = new CreateEventRequestDto
            {
                Operation = "create",
                EntityType = "BadType",
                EntityId = 1,
                DataSourceId = null,
                Properties = "{}",
                LastUpdatedBy = mockUserId
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _eventBusiness.CreateEvent(mockUserId, mockOrganizationId, null, dto));
        }

        [Fact]
        public async Task CreateEvent_Fails_IfBadOperationType()
        {
            // Arrange
            var dto = new CreateEventRequestDto
            {
                Operation = "BadType",
                EntityType = "metadata",
                EntityId = 1,
                DataSourceId = null,
                Properties = "{}",
                LastUpdatedBy = mockUserId
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _eventBusiness.CreateEvent(mockUserId, mockOrganizationId, null, dto));
        }

        #endregion

        #region LastUpdatedBy Tests

        [Fact]
        public async Task CreateEvent_Success_StoresLastUpdatedByUserId()
        {
            // Arrange
            var testEvent = new Event
            {
                ProjectId = pid,
                OrganizationId = mockOrganizationId,
                Operation = "create",
                EntityType = "test",
                EntityId = 999,
                DataSourceId = mockDataSourceId,
                Properties = "{}",
                LastUpdatedBy = mockUserId,
                LastUpdatedAt = now
            };

            // Act
            Context.Events.Add(testEvent);
            await Context.SaveChangesAsync();

            // Assert
            var savedEvent = await Context.Events.FindAsync(testEvent.Id);
            Assert.NotNull(savedEvent);
            Assert.Equal(mockUserId, savedEvent.LastUpdatedBy);
        }

        [Fact]
        public async Task CreateEvent_Success_NavigationPropertyLoadsUser()
        {
            // Arrange
            var testEvent = new Event
            {
                ProjectId = pid,
                OrganizationId = mockOrganizationId,
                Operation = "create",
                EntityType = "test",
                EntityId = 998,
                DataSourceId = mockDataSourceId,
                Properties = "{}",
                LastUpdatedBy = mockUserId,
                LastUpdatedAt = now
            };

            Context.Events.Add(testEvent);
            await Context.SaveChangesAsync();

            // Act
            var eventWithUser = await Context.Events
                .Include(e => e.LastUpdatedByUser)
                .FirstAsync(e => e.Id == testEvent.Id);

            // Assert
            Assert.NotNull(eventWithUser.LastUpdatedByUser);
            Assert.Equal("user1", eventWithUser.LastUpdatedByUser.Name);
            Assert.Equal("test@gmail.com", eventWithUser.LastUpdatedByUser.Email);
            Assert.Equal(mockUserId, eventWithUser.LastUpdatedBy);
        }

        [Fact]
        public async Task CreateEvent_Success_WithNullLastUpdatedBy()
        {
            // Arrange
            var testEvent = new Event
            {
                ProjectId = pid,
                OrganizationId = mockOrganizationId,
                Operation = "create",
                EntityType = "test",
                EntityId = 997,
                DataSourceId = mockDataSourceId,
                Properties = "{}",
                LastUpdatedBy = null,
                LastUpdatedAt = now
            };

            // Act
            Context.Events.Add(testEvent);
            await Context.SaveChangesAsync();

            // Assert
            var savedEvent = await Context.Events.FindAsync(testEvent.Id);
            Assert.NotNull(savedEvent);
            Assert.Null(savedEvent.LastUpdatedBy);

            var eventWithUser = await Context.Events
                .Include(e => e.LastUpdatedByUser)
                .FirstAsync(e => e.Id == testEvent.Id);

            Assert.Null(eventWithUser.LastUpdatedByUser);
        }

        [Fact]
        public async Task UpdateEvent_Success_UpdatesLastUpdatedByUserId()
        {
            // Arrange
            var testEvent = new Event
            {
                ProjectId = pid,
                OrganizationId = mockOrganizationId,
                Operation = "create",
                EntityType = "test",
                EntityId = 996,
                DataSourceId = mockDataSourceId,
                Properties = "{}",
                LastUpdatedBy = null,
                LastUpdatedAt = now
            };
            Context.Events.Add(testEvent);
            await Context.SaveChangesAsync();

            // Act
            testEvent.LastUpdatedBy = mockUser2Id;
            testEvent.Operation = "update";
            testEvent.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            Context.Events.Update(testEvent);
            await Context.SaveChangesAsync();

            // Assert
            var updatedEvent = await Context.Events
                .Include(e => e.LastUpdatedByUser)
                .FirstAsync(e => e.Id == testEvent.Id);

            Assert.Equal(mockUser2Id, updatedEvent.LastUpdatedBy);
            Assert.NotNull(updatedEvent.LastUpdatedByUser);
            Assert.Equal("user2", updatedEvent.LastUpdatedByUser.Name);
            Assert.Equal("update", updatedEvent.Operation);
        }

        #endregion

        protected override async Task SeedTestDataAsync()
        {
            await base.SeedTestDataAsync();

            var users = new List<User>
            {
                new User { Name = "user1", Email = "test@gmail.com" },
                new User { Name = "user2", Email = "test2@gmail.com" },
                new User { Name = "user3", Email = "test3@gmail.com" },
                new User { Name = "user4", Email = "test4@gmail.com" },
            };
            Context.Users.AddRange(users);
            await Context.SaveChangesAsync();
            mockUserId = users[0].Id;
            mockUser2Id = users[1].Id;
            mockUser3Id = users[2].Id;
            mockUser4Id = users[3].Id;

            var organization = new Organization
            {
                Name = "Organization1",
            };
            Context.Organizations.Add(organization);
            await Context.SaveChangesAsync();
            mockOrganizationId = organization.Id;

            var projects = new List<Project>
            {
                new Project
                {
                    Name = "Project 1", LastUpdatedBy = mockUserId, OrganizationId = mockOrganizationId,
                    LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                },
                new Project
                {
                    Name = "Project 2", LastUpdatedBy = mockUserId, OrganizationId = mockOrganizationId,
                    LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                },
            };
            Context.Projects.AddRange(projects);
            await Context.SaveChangesAsync();
            pid = projects[0].Id;
            pid2 = projects[1].Id;

            var action = new deeplynx.datalayer.Models.Action
            {
                Name = "Action1",
                OrganizationId = organization.Id,
                ProjectId = pid,
                LastUpdatedBy = mockUserId,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            Context.Actions.Add(action);
            await Context.SaveChangesAsync();
            mockActionId = action.Id;

            var dataSources = new List<DataSource>
            {
                new DataSource
                {
                    Name = "DataSource1",
                    OrganizationId = mockOrganizationId,
                    ProjectId = pid,
                    LastUpdatedBy = mockUserId,
                    LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                },
                new DataSource
                {
                    Name = "DataSource2",
                    OrganizationId = mockOrganizationId,
                    ProjectId = pid2,
                    LastUpdatedBy = mockUserId,
                    LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                }
            };

            Context.DataSources.AddRange(dataSources);
            await Context.SaveChangesAsync();
            mockDataSourceId = dataSources[0].Id;
            mockDataSource2Id = dataSources[1].Id;

            var organizationUser = new OrganizationUser
            {
                OrganizationId = mockOrganizationId,
                UserId = mockUserId,
                IsOrgAdmin = true
            };
            Context.OrganizationUsers.Add(organizationUser);
            await Context.SaveChangesAsync();

            var events = new List<Event>
            {
                // Events with 1st Project
                new Event
                {
                    ProjectId = pid,
                    OrganizationId = mockOrganizationId,
                    Operation = "create",
                    EntityType = "edge",
                    EntityId = 1,
                    DataSourceId = mockDataSourceId,
                    Properties = "{}",
                    LastUpdatedBy = mockUserId,
                    LastUpdatedAt = now
                },
                new Event
                {
                    ProjectId = pid,
                    OrganizationId = mockOrganizationId,
                    Operation = "create",
                    EntityType = "edge",
                    EntityId = 2,
                    DataSourceId = mockDataSourceId,
                    Properties = "{}",
                    LastUpdatedBy = mockUser2Id,
                    LastUpdatedAt = now
                },
                new Event
                {
                    ProjectId = pid,
                    OrganizationId = mockOrganizationId,
                    Operation = "delete",
                    EntityType = "class",
                    EntityId = 3,
                    DataSourceId = mockDataSourceId,
                    Properties = "{}",
                    LastUpdatedBy = mockUser2Id,
                    LastUpdatedAt = now
                },
                new Event
                {
                    ProjectId = pid,
                    OrganizationId = mockOrganizationId,
                    Operation = "delete",
                    EntityType = "class",
                    EntityId = 4,
                    DataSourceId = mockDataSourceId,
                    Properties = "{}",
                    LastUpdatedBy = mockUserId,
                    LastUpdatedAt = now
                },
                new Event
                {
                    ProjectId = pid,
                    OrganizationId = mockOrganizationId,
                    Operation = "delete",
                    EntityType = "edge",
                    EntityId = 2,
                    DataSourceId = mockDataSourceId,
                    Properties = "{}",
                    LastUpdatedBy = mockUserId,
                    LastUpdatedAt = now
                },
                new Event
                {
                    ProjectId = pid,
                    OrganizationId = mockOrganizationId,
                    Operation = "update",
                    EntityType = "edge",
                    EntityId = 5,
                    DataSourceId = mockDataSourceId,
                    Properties = "{}",
                    LastUpdatedBy = mockUser2Id,
                    LastUpdatedAt = now
                },
                // Events with 2nd Project
                new Event
                {
                    ProjectId = pid2,
                    OrganizationId = mockOrganizationId,
                    Operation = "update",
                    EntityType = "edge",
                    EntityId = 3,
                    DataSourceId = null,
                    Properties = "{}",
                    LastUpdatedBy = mockUser3Id,
                    LastUpdatedAt = now
                },
                new Event
                {
                    ProjectId = pid2,
                    OrganizationId = mockOrganizationId,
                    Operation = "delete",
                    EntityType = "edge",
                    EntityId = 4,
                    DataSourceId = mockDataSourceId,
                    Properties = "{}",
                    LastUpdatedBy = mockUser4Id,
                    LastUpdatedAt = now
                },
                // org level events
                new Event
                {
                    ProjectId = null,
                    OrganizationId = mockOrganizationId,
                    Operation = "update",
                    EntityType = "edge",
                    EntityId = 3,
                    DataSourceId = null,
                    Properties = "{}",
                    LastUpdatedBy = mockUser3Id,
                    LastUpdatedAt = now
                },
                new Event
                {
                    ProjectId = null,
                    OrganizationId = mockOrganizationId,
                    Operation = "delete",
                    EntityType = "edge",
                    EntityId = 4,
                    DataSourceId = mockDataSourceId,
                    Properties = "{}",
                    LastUpdatedBy = mockUser4Id,
                    LastUpdatedAt = now
                }
            };

            Context.Events.AddRange(events);
            await Context.SaveChangesAsync();
        }
    }
}