﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests.Management
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Core;
    using Microsoft.Azure.ServiceBus.Management;
    using Xunit;

    public class ManagementClientTests : IDisposable
    {
        internal string ConnectionString = TestUtility.NamespaceConnectionString;
        ManagementClient client;

        public ManagementClientTests()
        {
            client = new ManagementClient(new ServiceBusConnectionStringBuilder(ConnectionString));
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task BasicQueueCrudTest()
        {
            var queueName = Guid.NewGuid().ToString("D").Substring(0, 8);

            var qd = new QueueDescription(queueName)
            {
                AutoDeleteOnIdle = TimeSpan.FromHours(1),
                DefaultMessageTimeToLive = TimeSpan.FromDays(2),
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(1),
                EnableBatchedOperations = true,
                EnableDeadLetteringOnMessageExpiration = true,
                EnablePartitioning = false,
                ForwardDeadLetteredMessagesTo = null,
                ForwardTo = null,
                LockDuration = TimeSpan.FromSeconds(45),
                MaxDeliveryCount = 8,
                MaxSizeInMB = 2048,
                RequiresDuplicateDetection = true,
                RequiresSession = true,
                UserMetadata = nameof(BasicQueueCrudTest)
            };

            qd.AuthorizationRules.Add(new SharedAccessAuthorizationRule(
                "allClaims",
                new[] { AccessRights.Manage, AccessRights.Send, AccessRights.Listen }));

            var finalQ = await client.CreateQueueAsync(qd);
            Assert.Equal(qd, finalQ);

            var getQ = await client.GetQueueAsync(qd.Path);
            Assert.Equal(qd, getQ);

            getQ.EnableBatchedOperations = false;
            getQ.MaxDeliveryCount = 9;
            getQ.AuthorizationRules.Clear();
            getQ.AuthorizationRules.Add(new SharedAccessAuthorizationRule(
                "noManage",
                new[] { AccessRights.Send, AccessRights.Listen }));

            var updatedQ = await client.UpdateQueueAsync(getQ);
            Assert.Equal(getQ, updatedQ);

            var exists = await client.QueueExistsAsync(queueName);
            Assert.True(exists);

            var queues = await client.GetQueuesAsync();
            Assert.True(queues.Count > 1);
            Assert.Contains(queues, e => e.Path.Equals(queueName, StringComparison.OrdinalIgnoreCase));

            await client.DeleteQueueAsync(updatedQ.Path);

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                    async () =>
                    {
                        await client.GetQueueAsync(qd.Path);
                    });

            exists = await client.QueueExistsAsync(queueName);
            Assert.False(exists);
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task BasicTopicCrudTest()
        {
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);

            var td = new TopicDescription(topicName)
            {
                AutoDeleteOnIdle = TimeSpan.FromHours(1),
                DefaultMessageTimeToLive = TimeSpan.FromDays(2),
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(1),
                EnableBatchedOperations = true,
                EnablePartitioning = false,
                MaxSizeInMB = 2048,
                RequiresDuplicateDetection = true,
                UserMetadata = nameof(BasicTopicCrudTest)
            };

            td.AuthorizationRules.Add(new SharedAccessAuthorizationRule(
               "allClaims",
               new[] { AccessRights.Manage, AccessRights.Send, AccessRights.Listen }));

            var createdT = await client.CreateTopicAsync(td);
            Assert.Equal(td, createdT);

            var getT = await client.GetTopicAsync(td.Path);
            Assert.Equal(td, getT);

            getT.EnableBatchedOperations = false;
            getT.DefaultMessageTimeToLive = TimeSpan.FromDays(3);

            var updatedT = await client.UpdateTopicAsync(getT);
            Assert.Equal(getT, updatedT);

            var exists = await client.TopicExistsAsync(topicName);
            Assert.True(exists);

            var topics = await client.GetTopicsAsync();
            Assert.True(topics.Count > 1);
            Assert.Contains(topics, e => e.Path.Equals(topicName, StringComparison.OrdinalIgnoreCase));

            await client.DeleteTopicAsync(updatedT.Path);

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                    async () =>
                    {
                        await client.GetTopicAsync(td.Path);
                    });

            exists = await client.TopicExistsAsync(topicName);
            Assert.False(exists);
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task BasicSubscriptionCrudTest()
        {
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);
            await client.CreateTopicAsync(topicName);

            var subscriptionName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var sd = new SubscriptionDescription(topicName, subscriptionName)
            {
                AutoDeleteOnIdle = TimeSpan.FromHours(1),
                DefaultMessageTimeToLive = TimeSpan.FromDays(2),
                EnableDeadLetteringOnMessageExpiration = true,
                EnableBatchedOperations = false,
                ForwardDeadLetteredMessagesTo = null,
                ForwardTo = null,
                LockDuration = TimeSpan.FromSeconds(45),
                MaxDeliveryCount = 8,
                RequiresSession = true,
                UserMetadata = nameof(BasicSubscriptionCrudTest)
            };

            var createdS = await client.CreateSubscriptionAsync(sd);
            Assert.Equal(sd, createdS);

            var getS = await client.GetSubscriptionAsync(sd.TopicPath, sd.SubscriptionName);
            Assert.Equal(sd, getS);

            getS.DefaultMessageTimeToLive = TimeSpan.FromDays(3);
            getS.MaxDeliveryCount = 9;

            var updatedS = await client.UpdateSubscriptionAsync(getS);
            Assert.Equal(getS, updatedS);

            var exists = await client.SubscriptionExistsAsync(topicName, subscriptionName);
            Assert.True(exists);

            var subs = await client.GetSubscriptionsAsync(topicName);
            Assert.Equal(1, subs.Count);

            await client.DeleteSubscriptionAsync(sd.TopicPath, sd.SubscriptionName);

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                    async () =>
                    {
                        await client.GetSubscriptionAsync(sd.TopicPath, sd.SubscriptionName);
                    });

            await client.DeleteTopicAsync(sd.TopicPath);

            exists = await client.SubscriptionExistsAsync(topicName, subscriptionName);
            Assert.False(exists);
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task BasicRulesCrudTest()
        {
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var subscriptionName = Guid.NewGuid().ToString("D").Substring(0, 8);

            await client.CreateTopicAsync(topicName);
            await client.CreateSubscriptionAsync(
                new SubscriptionDescription(topicName, subscriptionName),
                new RuleDescription("rule0", new FalseFilter()));

            var sqlFilter = new SqlFilter("stringValue = @stringParam AND intValue = @intParam AND longValue = @longParam AND dateValue = @dateParam");
            sqlFilter.Parameters.Add("@stringParam", "string");
            sqlFilter.Parameters.Add("@intParam", (int)1);
            sqlFilter.Parameters.Add("@longParam", (long)12);
            sqlFilter.Parameters.Add("@dateParam", DateTime.UtcNow);
            var rule1 = new RuleDescription()
            {
                Name = "rule1",
                Filter = sqlFilter,
                Action = new SqlRuleAction("SET a='b'")
            };
            await client.CreateRuleAsync(topicName, subscriptionName, rule1);

            var correlationFilter = new CorrelationFilter()
            {
                ContentType = "contentType",
                CorrelationId = "correlationId",
                Label = "label",
                MessageId = "messageId",
                ReplyTo = "replyTo",
                ReplyToSessionId = "replyToSessionId",
                SessionId = "sessionId",
                To = "to"
            };
            correlationFilter.Properties.Add("customKey", "customValue");
            var rule2 = new RuleDescription()
            {
                Name = "rule2",
                Filter = correlationFilter,
                Action = null
            };
            await client.CreateRuleAsync(topicName, subscriptionName, rule2);

            var rules = await client.GetRulesAsync(topicName, subscriptionName);
            Assert.True(rules.Count == 3);
            Assert.Equal("rule0", rules[0].Name);
            Assert.Equal(rule1, rules[1]);
            Assert.Equal(rule2, rules[2]);

            ((CorrelationFilter)rule2.Filter).CorrelationId = "correlationIdModified";
            var updatedRule2 = await client.UpdateRuleAsync(topicName, subscriptionName, rule2);
            Assert.Equal(rule2, updatedRule2);

            var defaultRule = await client.GetRuleAsync(topicName, subscriptionName, "rule0");
            Assert.NotNull(defaultRule);
            await client.DeleteRuleAsync(topicName, subscriptionName, "rule0");
            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                    async () =>
                    {
                        await client.GetRuleAsync(topicName, subscriptionName, "rule0");
                    });

            await client.DeleteTopicAsync(topicName);
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task GetQueueRuntimeInfoTest()
        {
            var queueName = Guid.NewGuid().ToString("D").Substring(0, 8);

            // Fixing Created Time
            var qd = await client.CreateQueueAsync(queueName);

            // Changing Last Updated Time
            qd.AutoDeleteOnIdle = TimeSpan.FromMinutes(100);
            var updatedQ = await client.UpdateQueueAsync(qd);

            // Populating 1 active message, 1 dead letter message and 1 scheduled message
            // Changing Last Accessed Time
            var qClient = new QueueClient(this.ConnectionString, queueName);
            await qClient.SendAsync(new Message() { MessageId = "1" });
            await qClient.SendAsync(new Message() { MessageId = "2" });
            await qClient.SendAsync(new Message() { MessageId = "3", ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddDays(1) });
            var msg = await qClient.InnerReceiver.ReceiveAsync();
            await qClient.DeadLetterAsync(msg.SystemProperties.LockToken);

            var runtimeInfo = await client.GetQueueRuntimeInfoAsync(queueName);

            Assert.Equal(queueName, runtimeInfo.Path);
            Assert.True(runtimeInfo.CreatedAt < runtimeInfo.UpdatedAt);
            Assert.True(runtimeInfo.UpdatedAt < runtimeInfo.AccessedAt);
            Assert.Equal(1, runtimeInfo.MessageCountDetails.ActiveMessageCount);
            Assert.Equal(1, runtimeInfo.MessageCountDetails.DeadLetterMessageCount);
            Assert.Equal(1, runtimeInfo.MessageCountDetails.ScheduledMessageCount);
            Assert.Equal(3, runtimeInfo.MessageCount);
            Assert.True(runtimeInfo.SizeInBytes > 0);

            await client.DeleteQueueAsync(queueName);
            await qClient.CloseAsync();
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task GetTopicAndSubscriptionRuntimeInfoTest()
        {
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var td = await client.CreateTopicAsync(topicName);

            // Changing Last Updated Time
            td.AutoDeleteOnIdle = TimeSpan.FromMinutes(100);
            var updatedT = await client.UpdateTopicAsync(td);

            var subscriptionName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var sd = await client.CreateSubscriptionAsync(topicName, subscriptionName);

            // Changing Last Updated Time for subscription
            sd.AutoDeleteOnIdle = TimeSpan.FromMinutes(100);
            var updatedS = await client.UpdateSubscriptionAsync(sd);

            // Populating 1 active message, 1 dead letter message and 1 scheduled message
            // Changing Last Accessed Time
            var sender = new MessageSender(this.ConnectionString, topicName);
            var receiver = new MessageReceiver(this.ConnectionString, EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName));
            await sender.SendAsync(new Message() { MessageId = "1" });
            await sender.SendAsync(new Message() { MessageId = "2" });
            await sender.SendAsync(new Message() { MessageId = "3", ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddDays(1) });
            var msg = await receiver.ReceiveAsync();
            await receiver.DeadLetterAsync(msg.SystemProperties.LockToken);

            var topicRI = await client.GetTopicRuntimeInfoAsync(topicName);
            var subscriptionRI = await client.GetSubscriptionRuntimeInfoAsync(topicName, subscriptionName);

            Assert.Equal(topicName, topicRI.Path);
            Assert.Equal(topicName, subscriptionRI.TopicPath);
            Assert.Equal(subscriptionName, subscriptionRI.SubscriptionName);

            Assert.True(topicRI.CreatedAt < topicRI.UpdatedAt);
            Assert.True(topicRI.UpdatedAt < topicRI.AccessedAt);
            Assert.True(subscriptionRI.CreatedAt < subscriptionRI.UpdatedAt);
            Assert.True(subscriptionRI.UpdatedAt < subscriptionRI.AccessedAt);
            Assert.True(topicRI.UpdatedAt < subscriptionRI.UpdatedAt);

            Assert.Equal(0, topicRI.MessageCountDetails.ActiveMessageCount);
            Assert.Equal(0, topicRI.MessageCountDetails.DeadLetterMessageCount);
            Assert.Equal(1, topicRI.MessageCountDetails.ScheduledMessageCount);
            Assert.Equal(1, subscriptionRI.MessageCountDetails.ActiveMessageCount);
            Assert.Equal(1, subscriptionRI.MessageCountDetails.DeadLetterMessageCount);
            Assert.Equal(0, subscriptionRI.MessageCountDetails.ScheduledMessageCount);
            Assert.Equal(2, subscriptionRI.MessageCount);
            Assert.Equal(1, topicRI.SubscriptionCount);
            Assert.True(topicRI.SizeInBytes > 0);

            await client.DeleteSubscriptionAsync(topicName, subscriptionName);
            await client.DeleteTopicAsync(topicName);
            await sender.CloseAsync();
            await receiver.CloseAsync();
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task MessagingEntityNotFoundExceptionTest()
        {
            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                async () =>
                {
                    await client.GetQueueAsync("NonExistingPath");
                });

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                async () =>
                {
                    await client.GetSubscriptionAsync("NonExistingTopic", "NonExistingPath");
                });

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                async () =>
                {
                    await client.UpdateQueueAsync(new QueueDescription("NonExistingPath"));
                });

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                async () =>
                {
                    await client.UpdateTopicAsync(new TopicDescription("NonExistingPath"));
                });

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                async () =>
                {
                    await client.UpdateSubscriptionAsync(new SubscriptionDescription("NonExistingTopic", "NonExistingPath"));
                });

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                async () =>
                {
                    await client.DeleteQueueAsync("NonExistingPath");
                });

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                async () =>
                {
                    await client.DeleteTopicAsync("NonExistingPath");
                });

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                async () =>
                {
                    await client.DeleteSubscriptionAsync("NonExistingTopic", "NonExistingPath");
                });

            var queueName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);
            await client.CreateQueueAsync(queueName);
            await client.CreateTopicAsync(topicName);

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                async () =>
                {
                    await client.GetQueueAsync(topicName);
                });

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                async () =>
                {
                    await client.GetTopicAsync(queueName);
                });

            // Cleanup
            await client.DeleteQueueAsync(queueName);
            await client.DeleteTopicAsync(topicName);
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task MessagingEntityAlreadyExistsExceptionTest()
        {
            var queueName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var subscriptionName = Guid.NewGuid().ToString("D").Substring(0, 8);
            await client.CreateQueueAsync(queueName);
            await client.CreateTopicAsync(topicName);
            await client.CreateSubscriptionAsync(topicName, subscriptionName);

            await Assert.ThrowsAsync<MessagingEntityAlreadyExistsException>(
                async () =>
                {
                    await client.CreateQueueAsync(queueName);
                });

            await Assert.ThrowsAsync<MessagingEntityAlreadyExistsException>(
                async () =>
                {
                    await client.CreateTopicAsync(topicName);
                });

            await Assert.ThrowsAsync<MessagingEntityAlreadyExistsException>(
                async () =>
                {
                    await client.CreateSubscriptionAsync(topicName, subscriptionName);
                });

            // Cleanup
            await client.DeleteQueueAsync(queueName);
            await client.DeleteTopicAsync(topicName);
        }

        public static IEnumerable<object[]> TestData_EntityNameValidationTest => new[]
        {
            new object[] {"qq@", true},
            new object[] {"qq/", true},
            new object[] {"/qq", true},
            new object[] {"qq\\", true},
            new object[] {"q/q", false},
            new object[] {"qq?", true},
            new object[] {"qq#", true},
        };
        [Theory]
        [MemberData(nameof(TestData_EntityNameValidationTest))]
        [DisplayTestMethodName]
        public void EntityNameValidationTest(string entityName, bool isPathSeparatorAllowed)
        {
            Assert.Throws<ArgumentException>(
                () =>
                {
                    if (isPathSeparatorAllowed)
                    {
                        EntityNameHelper.CheckValidQueueName(entityName);
                    }
                    else
                    {
                        EntityNameHelper.CheckValidSubscriptionName(entityName);
                    }
                });
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task ForwardingEntitySetupTest()
        {
            // queueName --Fwd to--> destinationName --fwd dlq to--> dqlDestinationName
            var queueName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var destinationName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var dlqDestinationName = Guid.NewGuid().ToString("D").Substring(0, 8);

            var dlqDestinationQ = await client.CreateQueueAsync(dlqDestinationName);
            var destinationQ = await client.CreateQueueAsync(
                new QueueDescription(destinationName)
                {
                    ForwardDeadLetteredMessagesTo = dlqDestinationName
                });

            var qd = new QueueDescription(queueName)
            {
                ForwardTo = destinationName
            };
            var baseQ = await client.CreateQueueAsync(qd);

            var sender = new MessageSender(this.ConnectionString, queueName);
            await sender.SendAsync(new Message() { MessageId = "mid" });

            var receiver = new MessageReceiver(this.ConnectionString, destinationName);
            var msg = await receiver.ReceiveAsync();
            Assert.NotNull(msg);
            Assert.Equal("mid", msg.MessageId);
            await receiver.DeadLetterAsync(msg.SystemProperties.LockToken);
            await receiver.CloseAsync();

            receiver = new MessageReceiver(this.ConnectionString, dlqDestinationName);
            msg = await receiver.ReceiveAsync();
            Assert.NotNull(msg);
            Assert.Equal("mid", msg.MessageId);
            await receiver.CompleteAsync(msg.SystemProperties.LockToken);
            await receiver.CloseAsync();

            await client.DeleteQueueAsync(queueName);
            await client.DeleteQueueAsync(destinationName);
        }

        [Fact]
        [DisplayTestMethodName]
        public void AuthRulesEqualityCheckTest()
        {
            var qd = new QueueDescription("a");
            var rule1 = new SharedAccessAuthorizationRule("sendListen", new List<AccessRights> { AccessRights.Listen, AccessRights.Send });
            var rule2 = new SharedAccessAuthorizationRule("manage", new List<AccessRights> { AccessRights.Listen, AccessRights.Send, AccessRights.Manage });
            qd.AuthorizationRules.Add(rule1);
            qd.AuthorizationRules.Add(rule2);

            var qd2 = new QueueDescription("a");
            qd2.AuthorizationRules.Add(new SharedAccessAuthorizationRule(rule2.KeyName, rule2.PrimaryKey, rule2.SecondaryKey, rule2.Rights));
            qd2.AuthorizationRules.Add(new SharedAccessAuthorizationRule(rule1.KeyName, rule1.PrimaryKey, rule1.SecondaryKey, rule1.Rights));

            Assert.Equal(qd, qd2);
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task SqlFilterParamsTest()
        {
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var subscriptionName = Guid.NewGuid().ToString("D").Substring(0, 8);
            await client.CreateTopicAsync(topicName);
            await client.CreateSubscriptionAsync(topicName, subscriptionName);

            SqlFilter sqlFilter = new SqlFilter(
                "PROPERTY(@propertyName) = @stringPropertyValue " +
                "AND PROPERTY(intProperty) = @intPropertyValue " +
                "AND PROPERTY(longProperty) = @longPropertyValue " +
                "AND PROPERTY(boolProperty) = @boolPropertyValue " +
                "AND PROPERTY(doubleProperty) = @doublePropertyValue ")
            {
                Parameters =
                   {
                        { "@propertyName", "MyProperty" },
                        { "@stringPropertyValue", "string" },
                        { "@intPropertyValue", 3 },
                        { "@longPropertyValue", 3L },
                        { "@boolPropertyValue", true },
                        { "@doublePropertyValue", (double)3.0 },
                   }
            };

            var rule = await client.CreateRuleAsync(topicName, subscriptionName, new RuleDescription("rule1", sqlFilter));
            Assert.Equal(sqlFilter, rule.Filter);

            await client.DeleteTopicAsync(topicName);
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task CorrelationFilterPropertiesTest()
        {
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var subscriptionName = Guid.NewGuid().ToString("D").Substring(0, 8);
            await client.CreateTopicAsync(topicName);
            await client.CreateSubscriptionAsync(topicName, subscriptionName);

            var sClient = new SubscriptionClient(ConnectionString, topicName, subscriptionName);
            var filter = new CorrelationFilter();
            filter.Properties.Add("stringKey", "stringVal");
            filter.Properties.Add("intKey", 5);
            filter.Properties.Add("dateTimeKey", DateTime.UtcNow);

            var rule = await client.CreateRuleAsync(topicName, subscriptionName, new RuleDescription("rule1", filter));
            Assert.True(filter.Properties.Count == 3);
            Assert.Equal(filter, rule.Filter);

            await client.DeleteTopicAsync(topicName);
        }

        public void Dispose()
        {
            client.CloseAsync().Wait();
        }
    }
}
