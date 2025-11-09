using Avalonia.Rendering;
using Moq;
using Companion.Services;
using Prism.Events;
using Serilog;

namespace OpenIPC.Companion.Tests.Services;

public class TestEvent : PubSubEvent<string>
{
}

[TestFixture]
public class 
EventSubscriptionServiceTests
{
    [SetUp]
    public void SetUp()
    {
        // Mocking the IEventAggregator and ILogger interfaces
        _mockEventAggregator = new Mock<IEventAggregator>();
        _mockLogger = new Mock<ILogger>();
        _testEvent = new TestEvent();

        // Mocking the ForContext method of the logger to return the mock logger itself
        _mockLogger.Setup(x => x.ForContext(It.IsAny<Type>())).Returns(_mockLogger.Object);

        // Setting up the EventAggregator mock to return the test event
        _mockEventAggregator
            .Setup(ea => ea.GetEvent<TestEvent>())
            .Returns(_testEvent);

        // Instantiate the EventSubscriptionService with the mocked dependencies
        _eventSubscriptionService = new EventSubscriptionService(_mockEventAggregator.Object, _mockLogger.Object);
    
        // Optionally: Verify that mocks are set up correctly in the setup (could be removed if unnecessary)
        //_mockEventAggregator.Verify(ea => ea.GetEvent<TestEvent>(), Times.Once); // Ensure the event was set up
    }


    private Mock<IEventAggregator> _mockEventAggregator;
    private Mock<ILogger> _mockLogger;
    private EventSubscriptionService _eventSubscriptionService;
    private TestEvent _testEvent;

    [Test]
    public void Subscribe_InvokesActionWhenEventIsPublished()
    {
        // Arrange
        string receivedPayload = null;

        // Subscribe to the event
        _eventSubscriptionService.Subscribe<TestEvent, string>(payload => 
        {
            receivedPayload = payload;
            Console.WriteLine($"Received Payload: {payload}"); // Debugging: print the received payload
        });

        // Debugging: Log that the subscription has been made
        _mockLogger.Verify(
            logger => logger.Verbose(It.Is<string>(msg => msg.Contains("Subscribed to event TestEvent"))),
            Times.Once);

        // Act: Publish the event through EventSubscriptionService to trigger logging and action
        _eventSubscriptionService.Publish<TestEvent, string>("Test Payload");

        // Wait briefly to allow subscription action to be invoked
        Task.Delay(300).Wait();  // Wait for 300 milliseconds (adjust as needed)

        // Debugging: print the received payload after the event is published
        Console.WriteLine($"After Publish: Received Payload: {receivedPayload}");

        // Assert: Check that the payload was correctly received
        Assert.AreEqual("Test Payload", receivedPayload);

        // Verify logger was called for publishing event
        _mockLogger.Verify(
            logger => logger.Verbose(It.Is<string>(msg => msg.Contains("Published event TestEvent with payload Test Payload"))),
            Times.Once);
    }



    [Test]
    public void Publish_TriggersSubscribers()
    {
        // Arrange
        string receivedPayload = null;
        _testEvent.Subscribe(payload => receivedPayload = payload);

        // Act
        _eventSubscriptionService.Publish<TestEvent, string>("Another Test Payload");

        // Assert
        Assert.AreEqual("Another Test Payload", receivedPayload);
        _mockLogger.Verify(
            logger => logger.Verbose(It.Is<string>(msg =>
                msg.Contains("Published event TestEvent with payload Another Test Payload"))), Times.Once);
    }

    [Test]
    public void Constructor_ThrowsArgumentNullException_IfEventAggregatorIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EventSubscriptionService(null, _mockLogger.Object));
    }

    [Test]
    public void Constructor_ThrowsArgumentNullException_IfLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EventSubscriptionService(_mockEventAggregator.Object, null));
    }
}