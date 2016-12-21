﻿using System;
using Autofac;
using NUnit.Framework;

namespace SequencedAggregate.Tests.Acceptance
{
    [TestFixture]
    public class TransactionTests
    {
        private IContainer _container;

        private readonly string _connectionString = Environment.GetEnvironmentVariables().Contains("APPVEYOR")
                ? @"Server=(local)\SQL2014;Initial Catalog=SequencedAggregate;User ID=sa;Password=Password12!"
                : @"Data Source=SE-UTV28172; Initial Catalog=SequencedAggregate; Integrated Security=True";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var bldr = new ContainerBuilder();

            // TODO: Check .config connection strings...
            // TODO: The Create table stuff in configuration

            var module = SequencedAggregateConfiguration
                .Create()
                .WithEventSourceConnectionString(_connectionString)
                .WithViewRepositoryConnectionString(_connectionString)
                .GetModule<TransactionEventBase>();

            bldr.RegisterModule(module);

            _container = bldr.Build();
        }

        [Test]
        public void Commit_WhenThereIsAProjectionBuilderThatCrashes_WriteToEventStreamNotCommitted()
        {
            // Arrange
            string id = Guid.NewGuid().ToString();

            var aggregates = _container.Resolve<IAggregateRepository<TransactionEventBase>>();

            var transaction = aggregates.Read<TransactionAggregate>(id);
            transaction.CreateTransaction(id);

            try
            {
                // Act
                aggregates.Commit(transaction);
            }
            catch (Exception)
            {
                // Assert
                var transactionFromDb = aggregates.Read<TransactionAggregate>(id);
                Assert.That(string.IsNullOrEmpty(transactionFromDb.Id));
            }
        }
    }

    public class TransactionEventBase
    {
        // Nothing here...
    }

    public class TransactionCreated : TransactionEventBase
    {
        public string Id { get; set; }
    }

    public class TransactionAggregate : AggregateBase<TransactionEventBase>
    {
        private string _id;

        public override string Id => _id;

        public TransactionAggregate()
        {
            RegisterTransition<TransactionCreated>(Handler);
        }

        public void CreateTransaction(string id)
        {
            RaiseEvent(new TransactionCreated
            {
                Id = id
            });
        }

        private void Handler(TransactionCreated e)
        {
            _id = e.Id;
        }
    }

    public class TransactionProjectionBuilder : ProjectionBuilderBase<TransactionEventBase, TransactionView>
    {
        public TransactionProjectionBuilder()
        {
            RegisterHandler<TransactionCreated>(Handler);
        }

        private TransactionView Handler(TransactionCreated e, TransactionView view)
        {
            throw new Exception("Nasty stuff - We need to make sure that the Domain stays in sync. Let's hope the Transactions are configured!");
        }
    }

    public class TransactionView
    {
        public string Id { get; set; }
    }
}
