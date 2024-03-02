﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Cloudtoid.Interprocess.Tests;

public class QueueTests : IClassFixture<UniquePathFixture>
{
    private static readonly byte[] ByteArray1 = new byte[] { 100, };
    private static readonly byte[] ByteArray2 = new byte[] { 100, 110 };
    private static readonly byte[] ByteArray3 = new byte[] { 100, 110, 120 };
    private static readonly byte[] ByteArray50 = Enumerable.Range(1, 50).Select(i => (byte)i).ToArray();
    private readonly UniquePathFixture fixture;
    private readonly QueueFactory queueFactory;

    public QueueTests(
        UniquePathFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        var loggerFactory = new LoggerFactory();
        loggerFactory.AddProvider(new XunitLoggerProvider(testOutputHelper));
        queueFactory = new QueueFactory(loggerFactory);
    }

    [Fact]
    [TestBeforeAfter]
    public void Sample()
    {
        var message = new byte[] { 1, 2, 3 };
        var messageBuffer = new byte[3];
        CancellationToken cancellationToken = default;

        var factory = new QueueFactory();
        var options = new QueueOptions(
            queueName: "my-queue",
            capacity: 1024 * 1024);

        using var publisher = factory.CreatePublisher(options);
        publisher.TryEnqueue(message);

        options = new QueueOptions(
            queueName: "my-queue",
            capacity: 1024 * 1024);

        using var subscriber = factory.CreateSubscriber(options);
        subscriber.TryDequeue(messageBuffer, cancellationToken, out var msg);

        msg.ToArray().Should().BeEquivalentTo(message);
    }

    [Fact]
    [TestBeforeAfter]
    public void DependencyInjectionSample()
    {
        var message = new byte[] { 1, 2, 3 };
        var messageBuffer = new byte[3];
        CancellationToken cancellationToken = default;
        var services = new ServiceCollection();

        services
            .AddInterprocessQueue() // adding the queue related components
            .AddLogging(); // optionally, we can enable logging

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IQueueFactory>();

        var options = new QueueOptions(
            queueName: "my-queue",
            capacity: 1024 * 1024);

        using var publisher = factory.CreatePublisher(options);
        publisher.TryEnqueue(message);

        options = new QueueOptions(
            queueName: "my-queue",
            capacity: 1024 * 1024);

        using var subscriber = factory.CreateSubscriber(options);
        subscriber.TryDequeue(messageBuffer, cancellationToken, out var msg);

        msg.ToArray().Should().BeEquivalentTo(message);
    }

    [Fact]
    [TestBeforeAfter]
    public void CanEnqueueAndDequeue()
    {
        using var p = CreatePublisher(24);
        using var s = CreateSubscriber(24);

        p.TryEnqueue(ByteArray3).Should().BeTrue();
        var message = s.Dequeue(default);
        message.ToArray().Should().BeEquivalentTo(ByteArray3);

        p.TryEnqueue(ByteArray3).Should().BeTrue();
        message = s.Dequeue(default);
        message.ToArray().Should().BeEquivalentTo(ByteArray3);

        p.TryEnqueue(ByteArray2).Should().BeTrue();
        message = s.Dequeue(default);
        message.ToArray().Should().BeEquivalentTo(ByteArray2);

        p.TryEnqueue(ByteArray2).Should().BeTrue();
        message = s.Dequeue(new byte[5], default);
        message.ToArray().Should().BeEquivalentTo(ByteArray2);
    }

    [Fact]
    [TestBeforeAfter]
    public void CanEnqueueDequeueWrappedMessage()
    {
        using var p = CreatePublisher(128);
        using var s = CreateSubscriber(128);

        p.TryEnqueue(ByteArray50).Should().BeTrue();
        var message = s.Dequeue(default);
        message.ToArray().Should().BeEquivalentTo(ByteArray50);

        p.TryEnqueue(ByteArray50).Should().BeTrue();
        message = s.Dequeue(default);
        message.ToArray().Should().BeEquivalentTo(ByteArray50);

        p.TryEnqueue(ByteArray50).Should().BeTrue();
        message = s.Dequeue(default);
        message.ToArray().Should().BeEquivalentTo(ByteArray50);

        p.TryEnqueue(ByteArray50).Should().BeTrue();
        message = s.Dequeue(default);
        message.ToArray().Should().BeEquivalentTo(ByteArray50);
    }

    [Fact]
    [TestBeforeAfter]
    public void CannotEnqueuePastCapacity()
    {
        using var p = CreatePublisher(24);

        p.TryEnqueue(ByteArray3).Should().BeTrue();
        p.TryEnqueue(ByteArray1).Should().BeFalse();
    }

    [Fact]
    [TestBeforeAfter]
    public void DisposeShouldNotThrow()
    {
        var p = CreatePublisher(24);
        p.TryEnqueue(ByteArray3).Should().BeTrue();

        using var s = CreateSubscriber(24);
        p.Dispose();

        s.Dequeue(default);
    }

    [Fact]
    [TestBeforeAfter]
    public void CannotReadAfterProducerIsDisposed()
    {
        var p = CreatePublisher(24);
        p.TryEnqueue(ByteArray3).Should().BeTrue();
        using (var s = CreateSubscriber(24))
            p.Dispose();

        using (CreatePublisher(24))
        using (var s = CreateSubscriber(24))
        {
            s.TryDequeue(default, out var message).Should().BeFalse();
        }
    }

    [Theory]
    [Repeat(10)]
    [TestBeforeAfter]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The extra argument is needed by the Repeat attribute.")]
    [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "The extra argument is needed by the Repeat attribute.")]
    public async Task CanDisposeQueueAsync(int i)
    {
        using (var s = CreateSubscriber(1024))
        {
            _ = Task.Run(() => s.Dequeue(default));
            await Task.Delay(200);
        }
    }

    [Fact]
    [TestBeforeAfter]
    public void CanCircleBuffer()
    {
        using var p = CreatePublisher(1024);
        using var s = CreateSubscriber(1024);

        var message = Enumerable.Range(100, 66).Select(i => (byte)i).ToArray();

        for (var i = 0; i < 20000; i++)
        {
            p.TryEnqueue(message).Should().BeTrue();
            var result = s.Dequeue(default);
            result.ToArray().Should().BeEquivalentTo(message);
        }
    }

    [Fact]
    [TestBeforeAfter]
    public void CanRejectLargeMessages()
    {
        using (var p = CreatePublisher(24))
        using (var s = CreateSubscriber(24))
        {
            p.TryEnqueue(ByteArray3).Should().BeTrue();
            var message = s.Dequeue(default);
            message.ToArray().Should().BeEquivalentTo(ByteArray3);

            p.TryEnqueue(ByteArray3).Should().BeTrue();

            // This should fail because the queue is out of capacity
            p.TryEnqueue(ByteArray3).Should().BeFalse();

            message = s.Dequeue(default);
            message.ToArray().Should().BeEquivalentTo(ByteArray3);

            p.TryEnqueue(ByteArray3).Should().BeTrue();
            p.TryEnqueue(ByteArray3).Should().BeFalse();
        }

        using (var p = CreatePublisher(32))
        {
            p.TryEnqueue(ByteArray3).Should().BeTrue();
            p.TryEnqueue(ByteArray3).Should().BeTrue();
            p.TryEnqueue(ByteArray3).Should().BeFalse();
        }

        using (var p = CreatePublisher(32))
            p.TryEnqueue(ByteArray50).Should().BeFalse(); // failed here
    }

    private IPublisher CreatePublisher(long capacity)
        => queueFactory.CreatePublisher(
            new QueueOptions("qn", fixture.Path, capacity));

    private ISubscriber CreateSubscriber(long capacity)
        => queueFactory.CreateSubscriber(
            new QueueOptions("qn", fixture.Path, capacity));
}