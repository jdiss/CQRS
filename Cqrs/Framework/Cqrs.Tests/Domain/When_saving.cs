﻿using System;
using System.Linq;
using Cqrs.Domain;
using Cqrs.Domain.Exception;
using Cqrs.Domain.Factories;
using Cqrs.Tests.Substitutes;
using NUnit.Framework;

namespace Cqrs.Tests.Domain
{
	[TestFixture]
	public class When_saving
	{
		private TestInMemoryEventStore _eventStore;
		private TestAggregateNoParameterLessConstructor _aggregate;
		private TestEventPublisher _eventPublisher;
		private IUnitOfWork _unitOfWork;
		private Repository _rep;

		[SetUp]
		public void Setup()
		{
			var aggregateFactory = new AggregateFactory();
			_eventStore = new TestInMemoryEventStore();
			_eventPublisher = new TestEventPublisher();
			_rep = new Repository(aggregateFactory, _eventStore, _eventPublisher);
			_unitOfWork = new UnitOfWork(_rep);

			_aggregate = new TestAggregateNoParameterLessConstructor(2);

		}

		[Test]
		public void Should_save_uncommited_changes()
		{
			_aggregate.DoSomething();
			_unitOfWork.Add(_aggregate);
			_unitOfWork.Commit();
			Assert.AreEqual(1, _eventStore.Events.Count);
		}

		[Test]
		public void Should_mark_commited_after_commit()
		{
			_aggregate.DoSomething();
			_unitOfWork.Add(_aggregate);
			_unitOfWork.Commit();
			Assert.AreEqual(0, _aggregate.GetUncommittedChanges().Count());
		}
		
		[Test]
		public void Should_publish_events()
		{
			_aggregate.DoSomething();
			_unitOfWork.Add(_aggregate);
			_unitOfWork.Commit();
			Assert.AreEqual(1, _eventPublisher.Published);
		}

		[Test]
		public void Should_add_new_aggregate()
		{
			var agg = new TestAggregateNoParameterLessConstructor(1);
			agg.DoSomething();
			_unitOfWork.Add(agg);
			_unitOfWork.Commit();
			Assert.AreEqual(1, _eventStore.Events.Count);
		}

		[Test]
		public void Should_set_date()
		{
			var agg = new TestAggregateNoParameterLessConstructor(1);
			agg.DoSomething();
			_unitOfWork.Add(agg);
			_unitOfWork.Commit();
			Assert.That(_eventStore.Events.First().TimeStamp, Is.InRange(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1)));
		}

		[Test]
		public void Should_set_version()
		{
			var agg = new TestAggregateNoParameterLessConstructor(1);
			agg.DoSomething();
			agg.DoSomething();
			_unitOfWork.Add(agg);
			_unitOfWork.Commit();
			Assert.That(_eventStore.Events.First().Version, Is.EqualTo(1));
			Assert.That(_eventStore.Events.Last().Version, Is.EqualTo(2));

		}

		[Test]
		public void Should_set_id()
		{
			var id = Guid.NewGuid();
			var agg = new TestAggregateNoParameterLessConstructor(1, id);
			agg.DoSomething();
			_unitOfWork.Add(agg);
			_unitOfWork.Commit();
			Assert.That(_eventStore.Events.First().Id, Is.EqualTo(id));
		}

		[Test]
		public void Should_clear_tracked_aggregates()
		{
			var agg = new TestAggregate(Guid.NewGuid());
			_unitOfWork.Add(agg);
			agg.DoSomething();
			_unitOfWork.Commit();
			_eventStore.Events.Clear();

			Assert.Throws<AggregateNotFoundException>(() => _unitOfWork.Get<TestAggregate>(agg.Id));
		}
	}
}