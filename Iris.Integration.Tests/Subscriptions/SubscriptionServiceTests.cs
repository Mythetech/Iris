using System;
using System.Threading.Tasks;
using FluentAssertions;
using Iris.Api.Infrastructure;
using Iris.Api.Infrastructure.Account;
using Iris.Api.Services.Subscriptions;
using Iris.Api.Services.Subscriptions.Domain;
using Iris.Api.Services.User;
using Iris.Integration.Tests.Infrastructure;
using MassTransit.Mediator;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace Iris.Integration.Tests.Subscriptions
{
    public class SubscriptionServiceTests : IClassFixture<IrisWebApplicationFactory>, IAsyncLifetime
    {
        private PostgreSqlContainer _container = default!;
        private IrisCloudDbContext _db = default!;
        private readonly IrisWebApplicationFactory _factory;
        private IAccountContext _context;
        private ILogger<SubscriptionService> _logger;
        private UserManager<AppUser> _userManager;
        private IScopedMediator _mediator;

        private SubscriptionService _service;

        public SubscriptionServiceTests(IrisWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact(DisplayName = "HasActiveSubscriptionAsync returns true if the status is active")]
        public async Task HasActiveSubscriptionAsync_ReturnsTrue_IfStatusIsActive()
        {
            // Arrange
            var subscription = new Subscription
            {
                Email = "test@example.com",
                Status = "active",
                TenantId = _context.TenantId,
                UserId = _context.UserId,
            };

            await _db.Subscriptions.AddAsync(subscription);
            await _db.SaveChangesAsync();

            // Act
            var hasActiveSubscription = await _service.HasActiveSubscriptionAsync();

            // Assert
            hasActiveSubscription.Should().BeTrue();
        }

        [Fact(DisplayName = "HasActiveSubscriptionAsync returns false if the status is anything else")]
        public async Task HasActiveSubscriptionAsync_ReturnsFalse_IfStatusIsNotActive()
        {
            // Arrange
            var subscription = new Subscription
            {
                Email = "test@example.com",
                Status = "🍇",
                TenantId = _context.TenantId,
                UserId = _context.UserId,
            };

            await _db.Subscriptions.AddAsync(subscription);
            await _db.SaveChangesAsync();

            // Act
            var hasActiveSubscription = await _service.HasActiveSubscriptionAsync();

            // Assert
            hasActiveSubscription.Should().BeFalse();
        }

        [Fact(DisplayName = "GetSubscriptionAsync returns the correct subscription")]
        public async Task GetSubscriptionAsync_ReturnsCorrectSubscription()
        {
            // Arrange
            var subscription = new Subscription
            {
                Email = "test@example.com",
                Status = "active",
                TenantId = _context.TenantId,
                UserId = _context.UserId
            };

            await _db.Subscriptions.AddAsync(subscription);
            await _db.SaveChangesAsync();

            // Act
            var retrievedSubscription = await _service.GetSubscriptionAsync();

            // Assert
            retrievedSubscription.Should().BeEquivalentTo(subscription);
        }

        [Fact(DisplayName = "UpsertSubscriptionAsync updates the status of an existing subscription")]
        public async Task UpsertSubscriptionAsync_UpdatesStatusOfExistingSubscription()
        {
            // Arrange
            var subscription = new Subscription
            {
                Email = "test@example.com",
                Status = "active",
                TenantId = _context.TenantId,
                UserId = _context.UserId,
            };

            await _db.Subscriptions.AddAsync(subscription);
            await _db.SaveChangesAsync();

            var newStatus = "expired";

            // Act
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IrisCloudDbContext>();
            var service = new SubscriptionService(db, _context, _logger, _userManager, _mediator);
            await service.UpsertSubscriptionAsync(newStatus);

            // Assert
            using var verifyScope = _factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IrisCloudDbContext>();
            var verifySubscription = await verifyDb.Subscriptions.FirstOrDefaultAsync(x => x.TenantId == _context.TenantId && x.UserId == _context.UserId);

            verifySubscription.Should().NotBeNull();
            verifySubscription.Status.Should().Be(newStatus);
        }

        [Fact(DisplayName = "UpsertSubscriptionAsync creates a new subscription if none exists")]
        public async Task UpsertSubscriptionAsync_CreatesNewSubscriptionIfNoneExists()
        {
            // Arrange
            var newStatus = "active";

            // Act
            await _service.UpsertSubscriptionAsync(newStatus);

            // Assert
            var db = IrisPostgresDbFactory.CreateDb(_container);

            var subscription = await db.Subscriptions.FirstOrDefaultAsync(x => x.TenantId == _context.TenantId && x.UserId == _context.UserId);
            subscription.Should().NotBeNull();
            subscription.Status.Should().Be(newStatus);
        }

        [Fact(DisplayName = "ActivateTrialSubscription creates a new trial subscription if none exists")]
        public async Task ActivateTrialSubscription_CreatesNewTrialSubscription_IfNoneExists()
        {
            // Arrange
            var expiresOn = DateTime.UtcNow.AddDays(7);

            // Act
            await _service.ActivateTrialSubscription(expiresOn);

            await _db.SaveChangesAsync();

            // Assert
            var subscription = await _db.Subscriptions.FirstAsync(x => x.TenantId == _context.TenantId && x.UserId == _context.UserId);
            subscription.Should().NotBeNull();
            subscription.Status.Should().Be("active");
        }

        [Fact(DisplayName = "ActivateTrialSubscription updates an existing subscription to a trial")]
        public async Task ActivateTrialSubscription_UpdatesExistingSubscription_ToTrial()
        {
            // Arrange
            var subscription = new Subscription
            {
                Email = "test@example.com",
                Status = "active",
                TenantId = _context.TenantId,
                UserId = _context.UserId
            };

            await _db.Subscriptions.AddAsync(subscription);
            await _db.SaveChangesAsync();

            var expiresOn = DateTime.UtcNow.AddDays(7);

            // Act
            await _service.ActivateTrialSubscription(expiresOn);

            var verifySubscription = await _service.GetSubscriptionAsync();

            // Assert
            verifySubscription.Status.Should().Be("active");
            verifySubscription.TrialSubscription.Should().BeTrue();
        }

        [Fact(DisplayName = "DeactivateSubscription deactivates an active subscription")]
        public async Task DeactivateSubscription_DeactivatesActiveSubscription()
        {
            // Arrange
            var subscription = new Subscription
            {
                Email = "test@example.com",
                Status = "active",
                TenantId = _context.TenantId,
                UserId = _context.UserId,
            };

            await _db.Subscriptions.AddAsync(subscription);
            await _db.SaveChangesAsync();

            // Act
            await _service.DeactivateSubscription();
            var verifySubscription = await _service.GetSubscriptionAsync();

            // Assert
            verifySubscription.IsActive.Should().BeFalse();
        }

        [Fact(DisplayName = "Finds subscription from email in anonymous context")]
        public async Task CanFind_Subscription_FromEmail()
        {
            // Arrange
            var email = "newEmail@example.com";
            var subscription = new Subscription
            {
                Email = email,
                Status = "active",
                TenantId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
            };

            await _db.Subscriptions.AddAsync(subscription);
            await _db.SaveChangesAsync();

            // Act
            var verifySubscription = await _service.GetSubscriptionByEmailAsync(email);

            // Assert
            verifySubscription.IsActive.Should().BeTrue();
        }

        [Fact(DisplayName = "UpsertSubscriptionAsync throws exception when email is null and operation is anonymous")]
        public async Task UpsertSubscriptionAsync_ThrowsException_WhenEmailIsNull_AndOperationIsAnonymous()
        {
            // Arrange
            var status = "active";
            _context.Anonymous = true;

            // Act
            Func<Task> act = async () => await _service.UpsertSubscriptionAsync(status, null);

            // Assert
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("Email is required*");
        }
        
        [Fact(DisplayName = "UpsertSubscriptionAsync applies TenantId and UserId when created in an anonymous context")]
        public async Task UpsertSubscriptionAsync_AppliesTenantAndUserIds_WhenCreatedInAnonymousContext()
        {
            // Arrange
            var email = "user@example.com";
            var user = new AppUser { Email = email, UserName = email };

            // Register the user and assign tenant and user IDs
            var result = await _userManager.CreateAsync(user);
            result.Succeeded.Should().BeTrue();

            // Set the tenant and user context for testing
            var registeredUser = await _userManager.FindByEmailAsync(email);
            _context.TenantId = Guid.Empty;
            _context.UserId = Guid.Empty;

            // Upsert the subscription in an anonymous context
            _context.Anonymous = true; // Set the context as anonymous
            var newStatus = "active";
            var service = new SubscriptionService(_db, _context, _logger, _userManager, _mediator);

            // Act
            await service.UpsertSubscriptionAsync(newStatus, email);

            // Retrieve the subscription from the database to verify IDs were set correctly
            var verifySubscription = await _db.Subscriptions
                .FirstOrDefaultAsync(x => x.Email == email);

            // Assert
            verifySubscription.Should().NotBeNull();
            verifySubscription.TenantId.Should().Be(registeredUser.TenantId);
            verifySubscription.UserId.Should().Be(registeredUser.UserId);
            verifySubscription.Status.Should().Be(newStatus);
        }


        public Task DisposeAsync()
        {
            return _container.DisposeAsync().AsTask();
        }

        public async Task InitializeAsync()
        {
            _container = await IrisPostgresDbFactory.Create();
            _db = IrisPostgresDbFactory.CreateDb(_container);

            await _db.Database.MigrateAsync();

            var context = _context = _factory.Services.GetRequiredService<IAccountContext>();
            var logger = _logger = _factory.Services.GetRequiredService<ILogger<SubscriptionService>>();
            var userManager = _userManager = _factory.Services.GetRequiredService<UserManager<AppUser>>();
            var mediator = _mediator = _factory.Services.GetRequiredService<IScopedMediator>();

            _context.TenantId = Guid.NewGuid();
            _context.UserId = Guid.NewGuid();

            _service = new SubscriptionService(_db, context, logger, userManager, mediator);
        }
    }
}