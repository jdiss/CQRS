﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="cdmdotnet Limited">
// // 	Copyright cdmdotnet Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Cqrs.Authentication;
using Cqrs.Bus;
using Cqrs.Configuration;
using Cqrs.Events;
using cdmdotnet.Logging;
using Cqrs.Messages;
using Microsoft.ServiceBus.Messaging;

namespace Cqrs.Azure.ServiceBus
{
	public class AzureEventBusReceiver<TAuthenticationToken>
		: AzureEventBus<TAuthenticationToken>
		, IEventHandlerRegistrar
		, IEventReceiver<TAuthenticationToken>
	{
		protected static RouteManager Routes { get; private set; }

		static AzureEventBusReceiver()
		{
			Routes = new RouteManager();
		}

		public AzureEventBusReceiver(IConfigurationManager configurationManager, IMessageSerialiser<TAuthenticationToken> messageSerialiser, IAuthenticationTokenHelper<TAuthenticationToken> authenticationTokenHelper, ICorrelationIdHelper correlationIdHelper, ILogger logger)
			: base(configurationManager, messageSerialiser, authenticationTokenHelper, correlationIdHelper, logger, false)
		{
		}

		public void Start()
		{
			InstantiateReceiving();

			// Configure the callback options
			OnMessageOptions options = new OnMessageOptions
			{
				AutoComplete = false,
				AutoRenewTimeout = TimeSpan.FromMinutes(1)
			};

			// Callback to handle received messages
			ServiceBusReceiver.OnMessage(ReceiveEvent, options);
		}

		/// <summary>
		/// Register an event or command handler that will listen and respond to events or commands.
		/// </summary>
		public virtual void RegisterHandler<TMessage>(Action<TMessage> handler, Type targetedType)
			where TMessage : IMessage
		{
			Routes.RegisterHandler(handler, targetedType);
		}

		/// <summary>
		/// Register an event or command handler that will listen and respond to events or commands.
		/// </summary>
		public void RegisterHandler<TMessage>(Action<TMessage> handler)
			where TMessage : IMessage
		{
			RegisterHandler(handler, null);
		}

		protected virtual void ReceiveEvent(BrokeredMessage message)
		{
			try
			{
				Logger.LogDebug(string.Format("An event message arrived with the id '{0}'.", message.MessageId));
				string messageBody = message.GetBody<string>();
				IEvent<TAuthenticationToken> @event = MessageSerialiser.DeserialiseEvent(messageBody);

				CorrelationIdHelper.SetCorrelationId(@event.CorrelationId);
				Logger.LogInfo(string.Format("An event message arrived with the id '{0}' was of type {1}.", message.MessageId, @event.GetType().FullName));

				ReceiveEvent(@event);

				// Remove message from queue
				message.Complete();
				Logger.LogDebug(string.Format("An event message arrived and was processed with the id '{0}'.", message.MessageId));
			}
			catch (Exception exception)
			{
				// Indicates a problem, unlock message in queue
				Logger.LogError(string.Format("An event message arrived with the id '{0}' but failed to be process.", message.MessageId), exception: exception);
				message.Abandon();
			}
		}

		public virtual void ReceiveEvent(IEvent<TAuthenticationToken> @event)
		{
			switch (@event.Framework)
			{
				case FrameworkType.Akka:
					Logger.LogInfo(string.Format("An event arrived of the type '{0}' but was marked as coming from the '{1}' framework, so it was dropped.", @event.GetType().FullName, @event.Framework));
					return;
			}

			CorrelationIdHelper.SetCorrelationId(@event.CorrelationId);
			AuthenticationTokenHelper.SetAuthenticationToken(@event.AuthenticationToken);

			Type eventType = @event.GetType();
			bool isRequired;
			if (!ConfigurationManager.TryGetSetting(string.Format("{0}.IsRequired", eventType.FullName), out isRequired))
				isRequired = true;

			IEnumerable<Action<IMessage>> handlers = Routes.GetHandlers(@event, isRequired).Select(x => x.Delegate);
			// This check doesn't require an isRequired check as there will be an exception raised above and handled below.
			if (!handlers.Any())
				Logger.LogDebug(string.Format("The event handler for '{0}' is not required.", eventType.FullName));

			foreach (Action<IMessage> handler in handlers)
				handler(@event);
		}
	}
}